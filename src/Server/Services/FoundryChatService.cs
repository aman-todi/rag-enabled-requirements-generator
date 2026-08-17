using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using RequirementsGenerator.Server.Options;

namespace RequirementsGenerator.Server.Services;

/// <summary>
/// Calls a Microsoft Foundry chat model deployment using the Azure OpenAI-compatible client.
/// In Azure App Service this authenticates with the App Service's managed identity via
/// DefaultAzureCredential — no keys are stored in configuration. Locally, DefaultAzureCredential
/// falls back to the developer's own `az login` session (who must be granted the same "Foundry
/// User" RBAC role for local testing), or to Foundry:ApiKey if set for quick, keyless-free testing.
/// </summary>
public sealed class FoundryChatService : IFoundryChatService
{
    private readonly ChatClient _chatClient;
    private readonly FoundryOptions _options;

    public FoundryChatService(IOptions<FoundryOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException(
                "Foundry:Endpoint is not configured. Set the Foundry__Endpoint app setting " +
                "(see README.md / .env.example).");
        }

        if (string.IsNullOrWhiteSpace(_options.DeploymentName))
        {
            throw new InvalidOperationException(
                "Foundry:DeploymentName is not configured. Set the Foundry__DeploymentName app setting.");
        }

        var endpoint = new Uri(_options.Endpoint);

        AzureOpenAIClient azureClient = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? new AzureOpenAIClient(endpoint, BuildCredential(_options))
            : new AzureOpenAIClient(endpoint, new AzureKeyCredential(_options.ApiKey));

        _chatClient = azureClient.GetChatClient(_options.DeploymentName);
    }

    private static DefaultAzureCredential BuildCredential(FoundryOptions options) =>
        string.IsNullOrWhiteSpace(options.ClientId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = options.ClientId,
            });

    public async IAsyncEnumerable<string> GenerateRequirementsStreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
        {
            messages.Add(new SystemChatMessage(_options.SystemPrompt));
        }

        messages.Add(new UserChatMessage(prompt));

        AsyncCollectionResult<StreamingChatCompletionUpdate> updates =
            _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken);

        await foreach (StreamingChatCompletionUpdate update in updates.WithCancellation(cancellationToken))
        {
            foreach (ChatMessageContentPart part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }
}
