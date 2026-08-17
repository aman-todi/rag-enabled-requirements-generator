namespace RequirementsGenerator.Server.Services;

public interface IFoundryChatService
{
    /// <summary>
    /// Streams the model's response text in incremental chunks as they arrive from Microsoft
    /// Foundry. Retrieval augmentation against the internal requirements-writing guide is
    /// performed entirely on the Azure side (Azure AI Search index wired to the model
    /// deployment/agent) — this method simply forwards the prompt and relays the tokens.
    /// </summary>
    IAsyncEnumerable<string> GenerateRequirementsStreamAsync(string prompt, CancellationToken cancellationToken);
}
