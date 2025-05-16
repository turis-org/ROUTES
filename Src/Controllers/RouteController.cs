using System.Text.Json;
using Database;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace Src.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RouteController(IRoutingService service) : ControllerBase
{
    private IRoutingService _service = service;
    // выдаст результат при localhost:port/api/route/hello
    [HttpGet("hello")]
    public IActionResult GetHello() => Ok("Hello World");

    [HttpGet("get_route")]
    public IActionResult GetRoute(string name)
    {
        return Ok(JsonSerializer.Serialize(_service.GetRouteByName(name)));
    }

    [HttpPost("get_route_from_points")]
    public IActionResult GetRouteFromPoints(string name, List<Coordinate> locationPoints)
    {
        return Ok(JsonSerializer.Serialize(_service.CreateRouteFromPoints(name, locationPoints)));
    }
}