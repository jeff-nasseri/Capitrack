using Server.Application.Goals.Commands;
using Server.Application.Goals.Queries;

namespace Server.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/goals")]
public sealed class GoalsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery(Name = "category_id")] int? categoryId,
        [FromQuery(Name = "tag_id")] int? tagId) =>
        Ok(await mediator.Send(new GetGoalsQuery(categoryId, tagId)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) => Ok(await mediator.Send(new GetGoalByIdQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGoalCommand command) =>
        StatusCode(StatusCodes.Status201Created, await mediator.Send(command));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGoalCommand command) =>
        Ok(await mediator.Send(command with { Id = id }));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await mediator.Send(new DeleteGoalCommand(id));
        return Ok(new { message = "Goal deleted" });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAll()
    {
        await mediator.Send(new DeleteAllGoalsCommand());
        return Ok(new { message = "All goals deleted" });
    }
}
