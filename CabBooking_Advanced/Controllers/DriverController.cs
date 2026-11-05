using CabBooking_Advanced.Controllers;
using CabBooking_Advanced.Data;
using CabBooking_Advanced.Models;
using CabBooking_Advanced.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CabBooking_Advanced.Controllers
{
    public record CreateDriverRequest(string Name);
    public record LocationRequest(double Lat, double Lng);
    public record StatusRequest(DriverStatus Status);
    [ApiController]
    [Route("/drivers")]
    public class DriverController : ControllerBase
    {
        private readonly CabDbContext _dB;
        private readonly DriverLocationService _location;
        public DriverController(CabDbContext dB, DriverLocationService location)
        {
            _dB = dB;
            _location = location;
        }
        // Demo helper: create a driver (starts Offline).
        [HttpPost]
        public async Task<IActionResult> Create(CreateDriverRequest req)
        {
            var driver = new Driver { Id = Guid.NewGuid(), Name = req.Name, Status = DriverStatus.OffLine };
            _dB.Drivers.Add(driver);
            await _dB.SaveChangesAsync();
            return Created($"/drivers/{driver.Id}", driver);        
        }
        // Driver app calls this every ~4 seconds. Goes to Redis only, never SQL.
        [HttpPut("{id:guid}/location")]
        public async Task<IActionResult>UpdateLocation(Guid id, LocationRequest req)
        {
            await _location.UpdateAsync(id, req.Lat, req.Lng);
            return NoContent();
        }
        // Go online (1) or offline (0). A driver on a trip cannot change status.
        [HttpPut("id:guid/status")]
        public async Task<IActionResult> SetStatus(Guid id, StatusRequest req)
        {
            if (req.Status == DriverStatus.OnTrip)
                return BadRequest(new { error = "OnTrip is set automatically when a ride is accepted" });
            int rows = await _dB.Drivers
                .Where(d => d.Id == id && d.Status != DriverStatus.OnTrip)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, req.Status));
            if (rows == 0)
                return Conflict(new { error = "Driver not found or currently on a trip" });
            if (req.Status == DriverStatus.OffLine)
                await _location.RemoveAsync(id);
            return NoContent();
        }
        // Driver app polls this every 2-3 seconds instead of receiving a push.
        [HttpGet("{id:guid}/offers")]
        public async Task<IActionResult> GetOffers(Guid id)
        {
            var now = DateTime.UtcNow;
            var offers = await _dB.RideOffers.AsNoTracking()
                .Where(o => o.Id == id && o.Status == OfferStatus.Pending && o.ExpiresAt > now)
                .Join(_dB.Rides.Where(r => r.Status == RideStatus.Requested), o => o.rideId, r => r.Id,
               (o, r) => new { rideId = r.Id, r.PickupLat, r.PickupLng, o.ExpiresAt })
                .ToListAsync();
            return Ok(offers);

        }
    }
}

