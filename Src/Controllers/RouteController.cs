using System.Text.Json;
using Database;
using Microsoft.AspNetCore.Mvc;

namespace Src.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RouteController(IRoutingService service) : ControllerBase
{
    private IRoutingService _service = service; 
    // выдаст результат при localhost:port/api/route/hello
    [HttpGet("hello")]
    public IActionResult GetHello() => Ok("Hello World");

    public IActionResult GetRoute(string name)
    {
        return Ok(JsonSerializer.Serialize(_service.GetRouteByName(name)));
    }
}