using Capitrack.Api.Data;
using Capitrack.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capitrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tags")]
public class TagsController(CapitrackDbContext db) : ControllerBase
{
    private static TagDto ToDto(Tag t) => new(t.Id, t.Name, t.Color, t.CreatedAt);

    [HttpGet]
    public IActionResult GetAll() =>
        Ok(db.Tags.OrderBy(t => t.Name).ToList().Select(ToDto));

    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        var tag = db.Tags.FirstOrDefault(t => t.Id == id);
        if (tag == null) return NotFound(new { error = "Tag not found" });
        return Ok(ToDto(tag));
    }

    [HttpPost]
    public IActionResult Create([FromBody] TagRequest body)
    {
        if (string.IsNullOrEmpty(body.Name))
            return BadRequest(new { error = "Tag name required" });
        if (db.Tags.Any(t => t.Name == body.Name))
            return BadRequest(new { error = "Tag already exists" });

        var tag = new Tag { Name = body.Name, Color = string.IsNullOrEmpty(body.Color) ? "#6366f1" : body.Color };
        db.Tags.Add(tag);
        db.SaveChanges();
        return StatusCode(201, ToDto(tag));
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] TagRequest body)
    {
        var existing = db.Tags.FirstOrDefault(t => t.Id == id);
        if (existing == null) return NotFound(new { error = "Tag not found" });

        if (!string.IsNullOrEmpty(body.Name) && body.Name != existing.Name && db.Tags.Any(t => t.Name == body.Name && t.Id != id))
            return BadRequest(new { error = "Tag name already exists" });

        existing.Name = string.IsNullOrEmpty(body.Name) ? existing.Name : body.Name;
        existing.Color = string.IsNullOrEmpty(body.Color) ? existing.Color : body.Color;
        db.SaveChanges();
        return Ok(ToDto(existing));
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var existing = db.Tags.FirstOrDefault(t => t.Id == id);
        if (existing == null) return NotFound(new { error = "Tag not found" });
        db.Tags.Remove(existing);
        db.SaveChanges();
        return Ok(new { message = "Tag deleted" });
    }
}
