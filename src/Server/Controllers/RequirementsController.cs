using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RequirementsGenerator.Server.Models;
using RequirementsGenerator.Server.Services;

namespace RequirementsGenerator.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RequirementsController : ControllerBase
{
    private readonly IFoundryChatService _chatService;
    private readonly ILogger<RequirementsController> _logger;

    public RequirementsController(IFoundryChatService chatService, ILogger<RequirementsController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Streams generated requirements text back to the client over Server-Sent Events as the
    /// model produces it. The frontend (src/client) reads this stream token-by-token.
    /// </summary>
    [HttpPost("generate")]
    public async Task Generate([FromBody] GenerateRequirementsRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Easy Auth (App Service authentication) injects this header once a user has signed in
        // via Entra ID; it's used here only for audit logging, not for authorization decisions —
        // access itself is already gated at the Entra ID / App Service edge before the request
        // reaches this code (see README.md "Access Control").
        var callerName = Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault() ?? "unknown";
        _logger.LogInformation(
            "Requirements generation requested by {Caller} ({PromptLength} chars)",
            callerName,
            request.Prompt.Length);

        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");
        Response.ContentType = "text/event-stream";

        try
        {
            await foreach (var chunk in _chatService.GenerateRequirementsStreamAsync(request.Prompt, cancellationToken))
            {
                await WriteEventAsync("message", chunk, cancellationToken);
            }

            await WriteEventAsync("done", string.Empty, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or cancelled — nothing to do.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Foundry chat completion failed for caller {Caller}", callerName);
            await WriteEventAsync(
                "error",
                "The requirements generator is temporarily unavailable. Please try again.",
                cancellationToken);
        }
    }

    private async Task WriteEventAsync(string eventName, string data, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { data });
        await Response.WriteAsync($"event: {eventName}\ndata: {payload}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
