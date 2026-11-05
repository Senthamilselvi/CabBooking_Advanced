using CabBooking_Advanced.Data;
using CabBooking_Advanced.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
namespace CabBooking_Advanced.Services
{
    public class DispatchService
    {
        private const int MaxOffer = 5;
        private static readonly TimeSpan OfferLifetime = TimeSpan.FromMinutes(2);
        private readonly CabDbContext _dB;
        private readonly DriverLocationService _locations;
        public DispatchService(CabDbContext dB, DriverLocationService locations)
        {
            _dB = dB;
            _locations = locations;
        }
        public async Task DispatchAsync(Ride ride, CancellationToken ct)
        {
            // 1. Who is near the pickup point? (Redis, nearest first)
            var nearBy = await _locations.FindNearByAsync(ride.PickupLat, ride.PickupLng, RadiusKm: 3, max: 20);
            if (nearBy.Count == 0) return;

            // 2. Of those, who is really Available? (SQL is the truth)
            var available = (await _dB.Drivers
                .Where(d => nearBy.Contains(d.Id) && d.Status == DriverStatus.Available)
                .Select(d => d.Id).ToListAsync(ct)).ToHashSet();

            var alreadyOffered = (await _dB.RideOffers
                .Where(o => o.rideId == ride.Id)
                .Select(o => o.DriverId).ToListAsync(ct)).ToHashSet();

            var chosen = nearBy.
                Where(id => available.Contains(id) && !alreadyOffered.Contains(id))
                .Take(MaxOffer)
                .ToList();
            if (chosen.Count == 0) return;
            //// 4. Create the offers
            var expiresAt = DateTime.UtcNow + OfferLifetime;
            foreach (var id in chosen)
            {
                _dB.RideOffers.Add(new RideOffer
                {
                    Id = Guid.NewGuid(),
                    rideId = ride.Id,
                    DriverId = id,
                    Status = OfferStatus.Pending,
                    ExpiresAt = expiresAt
                });
                await _dB.SaveChangesAsync(ct);
            }
        }

    }
}
