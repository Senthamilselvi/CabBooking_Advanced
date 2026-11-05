using CabBooking_Advanced.Data;
using CabBooking_Advanced.Models;
using CabBooking_Advanced.Services;
using Microsoft.EntityFrameworkCore;
namespace CabBooking_Advanced.Workers
{
    public class DispatchWorker:BackgroundService
    {
        private readonly IServiceScopeFactory _Scope;
        private readonly ILogger<DispatchWorker> _log;
        public DispatchWorker(IServiceScopeFactory Scope, ILogger<DispatchWorker> log)
        {
            _Scope = Scope;
            _log = log;        
        }
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _Scope.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CabDbContext>();
                var dispatch = scope.ServiceProvider.GetRequiredService<DispatchService>();
                var now = DateTime.UtcNow;

                // 1. Expire offers whose 15 seconds ran out
                await db.RideOffers
                    .Where(r => r.Status == OfferStatus.Pending && r.ExpiresAt < now)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, OfferStatus.Expired), ct);

                // 2. Give up on rides waiting more than 3 minutes
                await db.Rides
                    .Where(r => r.Status == RideStatus.Requested && r.CreatedAt < now.AddMinutes(-3))
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RideStatus.NoDriversFound), ct);

                // 3. Rides that are waiting and have no live offer: find drivers
                var waiting = await db.Rides.AsNoTracking()
                .Where(r => r.Status == RideStatus.Requested && !db.RideOffers.Any(o => o.rideId == r.Id && o.Status == OfferStatus.Pending))
                .ToListAsync(ct);
              
                foreach (var ride in waiting)
                    await dispatch.DispatchAsync(ride, ct);

            }
            catch
            {
                _log.LogError("Dispatch Not Found");
            }
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }

    
    }
}
