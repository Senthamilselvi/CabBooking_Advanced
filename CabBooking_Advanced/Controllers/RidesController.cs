using CabBooking_Advanced.Controllers;
using CabBooking_Advanced.Data;
using CabBooking_Advanced.Models;
using CabBooking_Advanced.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using static CabBooking_Advanced.Services.RideAssignmentService;

namespace CabBooking_Advanced.Controllers
{
    public record CreateRideRequest(Guid RiderId, double Pickuplat, double Pickuplng);
    public record AssignRideRequest(Guid Driverid);
    [ApiController]
    [Route("rides")]
    public class RidesController : ControllerBase
    {
        private readonly RideServices _rides;
        private readonly RideAssignmentService _assignment;
        private readonly CabDbContext _dB;

        public RidesController(RideServices rides, RideAssignmentService assignment, CabDbContext dB)
        {
            _rides = rides;
            _assignment = assignment;
            _dB = dB;
        }
        // Rider requests a cab. Header "Idempotency-Key" makes retries safe.
        [HttpPost]
        public async Task<IActionResult> Create([FromHeader(Name = "Idempotency-Key")] string? Key, [FromBody] CreateRideRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(Key) || Key.Length > 100) return BadRequest(new { error = "Idempotency - Key header is required (max 100 chars)" });

            var (ride, created) = await _rides.CreateAsync(req.RiderId, req.Pickuplat, req.Pickuplng, Key, ct);
            return created ? CreatedAtAction(nameof(Get), new { id = ride.Id }, ride) : Ok(ride);
        }
        // Rider polls this to see whether a driver was assigned.
        [HttpGet("{id:Guid}")]
        public async Task<IActionResult> Get(Guid Id, CancellationToken ct)
        {
            var ride = await _dB.Rides.AsNoTracking().FirstOrDefaultAsync(r => r.Id == Id, ct);
            return ride is null ? NotFound() : Ok(ride);
        }
        // Driver accepts the offer. Exactly one driver wins.
        [HttpPost("{rideId:guid}/accept")]
        public async Task<IActionResult> Accept(Guid rideId, [FromBody] AssignRideRequest req,CancellationToken ct)
        {
            var result = await _assignment.AcceptRideAsync(rideId, req.Driverid, ct);
            return result switch
            {
                AcceptResult.Accepted or AcceptResult.AlreadyYours
                => Ok(new { status = "assigned", rideId, driverId = req.Driverid }),
                AcceptResult.RideTaken => Conflict(new { error = "Ride already assigned to another driver" }),
                AcceptResult.DriverBusy => Conflict(new { error = "Driver is not available" }),
                AcceptResult.NoValidOffer => Conflict(new { error = "Offer expired or was never made to you" }),
                _ => StatusCode(500)
            };
        }

        // Trip finished: frees the driver.
        [HttpPost("{rideId:guid}/complete")]
        public async Task<IActionResult> Complete(Guid rideId, CancellationToken ct)
        //{
          =>  await _assignment.CompleteRideAsync(rideId, ct)
                ? Ok(new { status = "completed" }) : Conflict(new { error = "Ride is not in Assigned state" });
        
       // }
    }
}


