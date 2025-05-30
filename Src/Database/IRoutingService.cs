using Database.Entities;
using NetTopologySuite.Geometries;
using Route = Database.Entities.Route;

namespace Database;

public interface IRoutingService
{
    public Task<Route> GetRouteByName(string name);
    public Task<Route> CreateRouteFromPoints(String name, List<Coordinate> points);
}