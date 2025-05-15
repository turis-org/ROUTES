using Database.Entities;

namespace Database;

public interface IRoutingService 
{
    public Task<Entities.Route> GetRouteByName(string name);
}