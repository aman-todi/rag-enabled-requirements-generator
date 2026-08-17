namespace RequirementsGenerator.Server.Models;

/// <summary>Request body for POST /api/requirements/generate.</summary>
public sealed class GenerateRequirementsRequest
{
    /// <summary>
    /// User's natural-language ask, e.g. "write complete testable requirements for an RC filter module".
    /// </summary>
    public string Prompt { get; set; } = string.Empty;
}
