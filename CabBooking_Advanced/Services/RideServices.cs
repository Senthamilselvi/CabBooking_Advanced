using CabBooking_Advanced.Data;
using CabBooking_Advanced.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CabBooking_Advanced.Services
{
    public class RideServices
    {
        private readonly CabDbContext _dB;
        public RideServices(CabDbContext dB)
        {
            _dB = dB;
        }
        // Returns the ride, plus true if newly created / false if it was a repeat request.
        public async Task<(Ride ride, bool created)> CreateAsync(Guid riderId, double pickupLat, double pickupLng, string idempontencyKey, CancellationToken ct = default)
        {
            // A retry of an earlier request? Return the same ride.
            var existing = await _dB.Rides.AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == idempontencyKey, ct);
            if (existing is not null)
                return (existing, false);

            var ride = new Ride { Id = Guid.NewGuid(), RiderId = riderId,PickupLat = pickupLat, PickupLng = pickupLng, Status = RideStatus.Requested, IdempotencyKey = idempontencyKey };
            _dB.Rides.Add(ride);
            try
            {
                await _dB.SaveChangesAsync(ct);
                return (ride, true);
            }
            catch(DbUpdateException)
            {
                // Two identical requests arrived at the same moment. The unique index
                // let only one in, so load the winner and return it.
                _dB.ChangeTracker.Clear();
                var Winner = await _dB.Rides.AsNoTracking()
              .FirstOrDefaultAsync(r => r.IdempotencyKey == idempontencyKey, ct);
                if (Winner is not null)
                    return (Winner, false);
                throw;
            }
        }


    }
}
