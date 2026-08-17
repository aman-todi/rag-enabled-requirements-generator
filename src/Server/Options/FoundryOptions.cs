namespace RequirementsGenerator.Server.Options;

/// <summary>
/// Bound from the "Foundry" configuration section. In Azure these values are supplied as App
/// Service Application Settings (which surface as environment variables Foundry__Endpoint,
/// Foundry__DeploymentName, etc. — see README.md and .env.example).
/// </summary>
public sealed class FoundryOptions
{
    /// <summary>
    /// Base endpoint of the Microsoft Foundry resource / project, e.g.
    /// https://&lt;your-resource&gt;.services.ai.azure.com/ or the Azure OpenAI-compatible
    /// endpoint for the resource hosting the model deployment.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Name of the chat model deployment (e.g. "gpt-4o" deployment name) to call.</summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Optional client ID of a user-assigned managed identity. Leave empty to use the App
    /// Service's system-assigned managed identity (recommended default).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Optional API key, used only for local development when a caller has no Azure identity
    /// available (e.g. quick testing without `az login`). Never set this in Azure App Service —
    /// production auth is via managed identity + DefaultAzureCredential.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional system message reinforcing the requirements-writing guidelines. The primary
    /// grounding (internal requirements-writing guide + examples) lives in the Azure AI Search
    /// index wired to the Foundry model deployment/agent, not in this app.
    /// </summary>
    public string? SystemPrompt { get; set; }
}
