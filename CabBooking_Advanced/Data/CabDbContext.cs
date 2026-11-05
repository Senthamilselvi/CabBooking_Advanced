using CabBooking_Advanced.Services;
using Microsoft.EntityFrameworkCore;
using CabBooking_Advanced.Models;
using Microsoft.Identity.Client;
namespace CabBooking_Advanced.Data
{
    public class CabDbContext: DbContext
    {
        public CabDbContext(DbContextOptions<CabDbContext> options) : base(options){ }
        public DbSet<Ride> Rides => Set<Ride>();
        public DbSet<Driver> Drivers => Set<Driver>();
        public DbSet<RideOffer> RideOffers => Set<RideOffer>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Ride>().Property(r => r.IdempotencyKey).HasMaxLength(100);
            b.Entity<Ride>().HasIndex(r => r.IdempotencyKey).IsUnique();

            b.Entity<Ride>().HasIndex(r => r.DriverId).IsUnique().HasFilter("[driverId] IS NOT NULL AND [Status] IN(1,2)");

            b.Entity<RideOffer>().HasIndex(o => new { o.rideId, o.DriverId }).IsUnique();
                    
        }
    }
}



//// Safety net inside the database itself:
//// one driver can have at most ONE active ride (Status 1 = Assigned, 2 = InProgress).
//b.Entity<Ride>().HasIndex(r => r.DriverId).IsUnique()
//    .HasFilter("[DriverId] IS NOT NULL AND [Status] IN (1, 2)");

//// A driver gets at most one offer per ride.
//b.Entity<RideOffer>().HasIndex(o => new { o.RideId, o.DriverId }).IsUnique();