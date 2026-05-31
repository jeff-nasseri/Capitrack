using Capitrack.Api.Models;
using Capitrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capitrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private const string Version = "1.0.0";
    private const string Repository = "https://github.com/jeff-nasseri/Capitrack";

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        db_path = DbPathResolver.Resolve(),
        version = Version,
        app_name = "Capitrack",
        repository = Repository,
        license = "MIT"
    });

    [HttpGet("database")]
    public IActionResult GetDatabase()
    {
        var path = DbPathResolver.Resolve();
        return Ok(new { path, exists = System.IO.File.Exists(path) });
    }

    [HttpPut("database")]
    public IActionResult SetDatabase([FromBody] DatabasePathRequest body)
    {
        if (string.IsNullOrEmpty(body.Path))
            return BadRequest(new { error = "Database path required" });

        var absolutePath = Path.IsPathRooted(body.Path) ? body.Path : Path.GetFullPath(body.Path);
        var dir = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); }
            catch (Exception e) { return BadRequest(new { error = $"Cannot create directory: {e.Message}" }); }
        }

        try
        {
            DbPathResolver.Save(absolutePath);
            return Ok(new { message = "Database path updated successfully", path = absolutePath, exists = System.IO.File.Exists(absolutePath) });
        }
        catch (Exception e)
        {
            return StatusCode(500, new { error = $"Failed to reinitialize database: {e.Message}" });
        }
    }

    [HttpPost("refresh")]
    public IActionResult Refresh() =>
        Ok(new { message = "Application refreshed successfully", db_path = DbPathResolver.Resolve() });

    [HttpGet("about")]
    public IActionResult About() => Ok(new
    {
        name = "Capitrack",
        description = "Personal wealth tracking and investment portfolio management platform",
        version = Version,
        license = "MIT",
        repository = Repository,
        author = "Jeff Nasseri",
        open_source = true
    });
}
