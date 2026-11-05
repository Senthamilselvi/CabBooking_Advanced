using CabBooking_Advanced.Models;
using StackExchange.Redis;

namespace CabBooking_Advanced.Services
{
    public class DriverLocationService
    {
        private const string GeoKey = "drivers:geo";
        private readonly IDatabase _redis;

        public DriverLocationService(IConnectionMultiplexer redis)
        { _redis = redis.GetDatabase(); }
        public async Task UpdateAsync(Guid driverId, double lat, double lng)
        {
            await _redis.GeoAddAsync(GeoKey, lat, lng, driverId.ToString());
            await _redis.StringSetAsync($"driver:alive:{driverId}", "1", TimeSpan.FromSeconds(30));
        }
        public async Task RemoveAsync(Guid driverId)
        {
            _redis.GeoRemoveAsync(GeoKey,driverId.ToString());
        }
        public async Task<List<Guid>> FindNearByAsync(double lat, double lng, double RadiusKm, int max)
        {
            var results = await _redis.GeoSearchAsync(GeoKey, lat, lng, new GeoSearchCircle(RadiusKm, GeoUnit.Kilometers),
                count: max, order: Order.Ascending);
            var ids = new List< Guid > ();
            foreach(var r in results)
            {
                var id = Guid.Parse(r.Member.ToString());
                if (await _redis.KeyExistsAsync($"driver:alive{id}")) ids.Add(id);
            }
            return(ids);
        }

    }
}


