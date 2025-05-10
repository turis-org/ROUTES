using Database.Entities;

namespace Database;

public interface IRoutingService 
{
    public Task<Route> GetRouteByName(string name);
}