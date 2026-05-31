using Server.Application.Tags.Commands;
using Server.Application.Tags.Queries;

namespace Server.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tags")]
public sealed class TagsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await mediator.Send(new GetTagsQuery()));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) => Ok(await mediator.Send(new GetTagByIdQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTagCommand command) =>
        StatusCode(StatusCodes.Status201Created, await mediator.Send(command));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTagCommand command) =>
        Ok(await mediator.Send(command with { Id = id }));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await mediator.Send(new DeleteTagCommand(id));
        return Ok(new { message = "Tag deleted" });
    }
}
