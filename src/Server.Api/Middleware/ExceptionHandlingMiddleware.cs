using Server.Application.Common.Exceptions;
using Server.Domain.Exceptions;

namespace Server.Api.Middleware;

/// <summary>Maps application/domain exceptions to the original API's status codes and { error } body.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex) { await Write(context, StatusCodes.Status400BadRequest, ex.FirstMessage); }
        catch (NotFoundException ex) { await Write(context, StatusCodes.Status404NotFound, ex.Message); }
        catch (ConflictException ex) { await Write(context, StatusCodes.Status409Conflict, ex.Message); }
        catch (UnauthorizedException ex) { await Write(context, StatusCodes.Status401Unauthorized, ex.Message); }
        catch (DomainException ex) { await Write(context, StatusCodes.Status400BadRequest, ex.Message); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await Write(context, StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private static async Task Write(HttpContext ctx, int status, string error)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new { error });
    }
}
