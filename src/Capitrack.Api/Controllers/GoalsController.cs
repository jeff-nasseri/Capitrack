using Capitrack.Api.Data;
using Capitrack.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Capitrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/goals")]
public class GoalsController(CapitrackDbContext db) : ControllerBase
{
    private List<TagDto> TagsFor(int goalId) =>
        (from gt in db.GoalTags
         join t in db.Tags on gt.TagId equals t.Id
         where gt.GoalId == goalId
         orderby t.Name
         select new TagDto(t.Id, t.Name, t.Color, t.CreatedAt)).ToList();

    private GoalDto ToDto(Goal g) => new(
        g.Id, g.Title, g.TargetAmount, g.TargetDate, g.Description, g.Achieved, g.CategoryId,
        g.CreatedAt, g.UpdatedAt, TagsFor(g.Id));

    private void SyncTags(int goalId, List<int>? tagIds)
    {
        db.GoalTags.Where(x => x.GoalId == goalId).ExecuteDelete();
        if (tagIds is { Count: > 0 })
            foreach (var tagId in tagIds.Distinct())
                db.GoalTags.Add(new GoalTag { GoalId = goalId, TagId = tagId });
        db.SaveChanges();
    }

    [HttpGet]
    public IActionResult GetAll([FromQuery(Name = "category_id")] int? categoryId, [FromQuery(Name = "tag_id")] int? tagId)
    {
        var query = db.Goals.AsQueryable();
        if (categoryId is int cid) query = query.Where(g => g.CategoryId == cid);
        var goals = query.OrderBy(g => g.TargetDate).ToList();

        if (tagId is int tid)
        {
            var goalIds = db.GoalTags.Where(gt => gt.TagId == tid).Select(gt => gt.GoalId).ToHashSet();
            goals = goals.Where(g => goalIds.Contains(g.Id)).ToList();
        }
        return Ok(goals.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        var goal = db.Goals.FirstOrDefault(g => g.Id == id);
        if (goal == null) return NotFound(new { error = "Goal not found" });
        return Ok(ToDto(goal));
    }

    [HttpPost]
    public IActionResult Create([FromBody] GoalRequest body)
    {
        if (string.IsNullOrEmpty(body.Title) || string.IsNullOrEmpty(body.TargetDate))
            return BadRequest(new { error = "Title and target date required" });

        var goal = new Goal
        {
            Title = body.Title,
            TargetAmount = body.TargetAmount ?? 0,
            TargetDate = body.TargetDate,
            Description = body.Description ?? "",
            CategoryId = body.CategoryId
        };
        db.Goals.Add(goal);
        db.SaveChanges();
        if (body.TagIds is { Count: > 0 }) SyncTags(goal.Id, body.TagIds);
        return StatusCode(201, ToDto(db.Goals.First(g => g.Id == goal.Id)));
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] GoalRequest body)
    {
        var existing = db.Goals.FirstOrDefault(g => g.Id == id);
        if (existing == null) return NotFound(new { error = "Goal not found" });

        existing.Title = string.IsNullOrEmpty(body.Title) ? existing.Title : body.Title;
        existing.TargetAmount = body.TargetAmount ?? existing.TargetAmount;
        existing.TargetDate = string.IsNullOrEmpty(body.TargetDate) ? existing.TargetDate : body.TargetDate;
        existing.Description = body.Description ?? existing.Description;
        existing.Achieved = body.Achieved.HasValue ? (body.Achieved.Value ? 1 : 0) : existing.Achieved;
        existing.CategoryId = body.CategoryId ?? existing.CategoryId;
        existing.UpdatedAt = DateTime.UtcNow;
        db.SaveChanges();

        if (body.TagIds != null) SyncTags(id, body.TagIds);
        return Ok(ToDto(db.Goals.First(g => g.Id == id)));
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var existing = db.Goals.FirstOrDefault(g => g.Id == id);
        if (existing == null) return NotFound(new { error = "Goal not found" });
        db.Goals.Remove(existing);
        db.SaveChanges();
        return Ok(new { message = "Goal deleted" });
    }

    [HttpDelete]
    public IActionResult DeleteAll()
    {
        db.GoalTags.ExecuteDelete();
        db.Goals.ExecuteDelete();
        return Ok(new { message = "All goals deleted" });
    }
}
