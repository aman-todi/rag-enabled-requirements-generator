using Microsoft.AspNetCore.Mvc;

namespace RequirementsGenerator.Server.Controllers;

/// <summary>
/// Exposes the signed-in user's identity to the frontend, as surfaced by Azure App Service's
/// built-in authentication ("Easy Auth"). No token validation happens here — App Service has
/// already authenticated the request against Entra ID (and rejected anyone not assigned to the
/// app) before it reaches this controller; these headers are for display/audit only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class UserController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me()
    {
        var principalName = Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
        var principalId = Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"].FirstOrDefault();
        var identityProvider = Request.Headers["X-MS-CLIENT-PRINCIPAL-IDP"].FirstOrDefault();

        return Ok(new
        {
            authenticated = !string.IsNullOrEmpty(principalName),
            name = principalName,
            id = principalId,
            identityProvider,
        });
    }
}
