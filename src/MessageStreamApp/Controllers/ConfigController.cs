using MessageStreamApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MessageStreamApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ConfigController : ControllerBase
{
    private readonly IOptions<StreamConfiguration> _configuration;

    public ConfigController(IOptions<StreamConfiguration> configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    [HttpGet]
    public ActionResult<StreamConfiguration> GetConfig()
    {
        var config = _configuration.Value;
        config.Validate();
        return Ok(config);
    }
}
