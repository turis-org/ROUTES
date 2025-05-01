using Microsoft.AspNetCore.Mvc;

namespace Src.Controllers;

[ApiController]
[Route("request/[controller]")]
public class RouteController : ControllerBase
{
    // выдаст результат при localhost:port/request/route/hello
    [HttpGet("hello")]
    public IActionResult GetHello() => Ok("Hello World");
}