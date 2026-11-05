namespace CabBooking_Advanced.Models
{   
        public enum DriverStatus {OffLine=0,Available=1, OnTrip=2 }
        public enum RideStatus { Requested = 0, Assigned = 1, InProgress = 2, Completed=3, Cancelled=4,NoDriversFound=5 }
        public enum OfferStatus {Pending=0, Accepted=1, Expired=2 }
        public class Driver
        {
        public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public DriverStatus Status { get; set; }
            public Guid? CurrentRideId { get; set; }
        }
        public class Ride
        { 
        public Guid Id { get; set; }
            public Guid RiderId { get; set; }
            public double PickupLat { get; set; }
            public double PickupLng { get; set; }
            public RideStatus Status { get; set; }
            public Guid? DriverId { get; set; }
            public string IdempotencyKey { get; set; } = "";
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime? AssignedAt { get; set; }
        }
        public class RideOffer
        {
            public Guid Id { get; set; }
            public Guid rideId { get; set; }
            public Guid DriverId { get; set; }
            public OfferStatus Status { get; set; }
            public DateTime ExpiresAt { get; set; }

        }
    }


