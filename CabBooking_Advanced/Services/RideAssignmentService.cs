using CabBooking_Advanced.Data;
using CabBooking_Advanced.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CabBooking_Advanced.Services
{
    public class RideAssignmentService
    {
        public enum AcceptResult
        {
            Accepted,
            AlreadyYours,
            RideTaken,
            DriverBusy,
            NoValidOffer,

        };
        private readonly CabDbContext _dB;
        public RideAssignmentService(CabDbContext dB)
        {
            _dB = dB;
        }
        public async Task<AcceptResult> AcceptRideAsync(Guid rideId, Guid driverId, CancellationToken ct = default)
        {          
            await using var tx = await _dB.Database.BeginTransactionAsync(ct);

            // STEP 1: claim the ride. Check + write in ONE atomic statement.
            // Only the first caller can match "Status == Requested".
            var now = DateTime.UtcNow;
            int driverRows = await _dB.Drivers
                .Where(r => r.Id == driverId && r.Status == DriverStatus.Available)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, DriverStatus.OnTrip)
                .SetProperty(r => r.CurrentRideId, rideId), ct);

            if (driverRows == 0)
            {
                await tx.RollbackAsync(ct);
                //    // We lost (or the ride doesn't exist). Work out why, for a helpful response.
                var currentRide = await _dB.Drivers.AsNoTracking()
                    .Where(r => r.Id == driverId)
                    .Select(r => r.CurrentRideId)
                    .FirstOrDefaultAsync(ct);
                return currentRide == rideId ? AcceptResult.AlreadyYours : AcceptResult.DriverBusy;
            }

                int offerRows = await _dB.RideOffers
                    .Where(o => o.rideId == rideId &&  o.DriverId == driverId &&  o.Status == OfferStatus.Pending && o.ExpiresAt > now)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OfferStatus.Accepted), ct);

            if(offerRows ==0)
            { 
                await tx.RollbackAsync(ct);
            return AcceptResult.NoValidOffer;
            }
            int riderRows = await _dB.Rides
                .Where(r => r.Id == rideId && r.Status == RideStatus.Requested)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RideStatus.Assigned)
                .SetProperty(r=>r.DriverId,driverId)
                .SetProperty(r=>r.AssignedAt,now),ct);

            if (riderRows == 0)
            {
                await tx.RollbackAsync(ct);
                return AcceptResult.RideTaken;
            }
            await tx.CommitAsync(ct);

            await _dB.RideOffers
                .Where(r => r.rideId == rideId && r.Status == OfferStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, OfferStatus.Expired), ct);
            return AcceptResult.Accepted;
        }
        public async Task<bool> CompleteRideAsync(Guid rideId, CancellationToken ct=default)
        {
            await using var tx =await _dB.Database.BeginTransactionAsync(ct);
            int rideRows = await _dB.Rides
                 .Where(r => r.Id == rideId && r.Status == RideStatus.Assigned)
                 .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RideStatus.Completed), ct);
            if (rideRows == 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }
            int driverRows = await _dB.Drivers
                .Where(d => d.CurrentRideId==rideId)
                .ExecuteUpdateAsync(s => s.SetProperty(d=>d.Status,DriverStatus.Available)
                .SetProperty(d=>d.CurrentRideId, (Guid?)null),ct);
            await tx.CommitAsync(ct);
            return true;
        }

    }
}
