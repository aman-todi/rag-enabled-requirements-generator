# Architecture

This document records the design pattern this app follows, so future changes (or a rebuild for a
different internal tool) can reuse it without re-deriving it from scratch.

## Why this pattern

For an internal, employee-restricted chat-style tool backed by a RAG model in Microsoft Foundry,
the standard enterprise pattern is:

1. **Single deployable unit.** An ASP.NET Core Web API serves both the JSON API and the compiled
   React static assets from `wwwroot/`. One `dotnet publish`, one App Service, one origin — no
   CORS configuration, no second hosting resource for the frontend.
2. **Zero secrets in the app.** The backend never holds an API key for Foundry. It authenticates
   as itself (the App Service's managed identity) using `DefaultAzureCredential`, which Azure
   issues short-lived tokens for automatically.
3. **Access control lives at the identity boundary, not in application code.** Restricting the
   tool to one team is done in Entra ID (App registration → "Assignment required" + a security
   group), and enforced before any request reaches the app, via Azure App Service's built-in
   authentication ("Easy Auth"). This keeps the C# codebase free of OIDC middleware, token
   validation, and login/logout endpoints.
4. **RAG lives entirely in Azure, outside this repo.** Azure AI Search (indexing the internal
   requirements-writing guide + examples) and the Foundry model deployment/agent are provisioned
   and linked in Azure. This app is a thin client: it forwards the user's prompt to the model
   deployment and streams back whatever text comes out. If the retrieval or grounding behavior
   needs to change, that happens in Azure AI Search / Foundry, not in this codebase.

## Request flow

```
┌─────────────┐  1. HTTPS + Entra ID sign-in  ┌───────────────────────────────────────────────┐
│   Browser   │ ─────────────────────────────▶ │              Azure App Service                │
│  (React SPA)│                                │  ┌─────────────────────────────────────────┐  │
│             │ ◀───── wwwroot/ (SPA) ──────── │  │ App Service Authentication ("Easy Auth") │  │
└─────────────┘                                │  │  - validates Entra ID token              │  │
       │                                       │  │  - blocks anyone not assigned to the app │  │
       │ 2. fetch POST /api/requirements/      │  │  - injects X-MS-CLIENT-PRINCIPAL-* headers│ │
       │    generate (same origin, SSE)        │  └─────────────────────────────────────────┘  │
       ▼                                       │  ┌─────────────────────────────────────────┐  │
┌─────────────┐                                │  │   ASP.NET Core (Server/)                 │  │
│  Streamed   │ ◀──── text/event-stream ────── │  │   RequirementsController                 │  │
│  requirements│                               │  │     -> IFoundryChatService                │  │
└─────────────┘                                │  │          -> AzureOpenAIClient             │  │
                                                │  │             (Azure.AI.OpenAI SDK)         │  │
                                                │  └──────────────────┬────────────────────────┘  │
                                                │                     │ 3. DefaultAzureCredential  │
                                                │  ┌──────────────────▼────────────────────────┐  │
                                                │  │   System-assigned Managed Identity         │  │
                                                │  └──────────────────┬────────────────────────┘  │
                                                └─────────────────────┼───────────────────────────┘
                                                                      │ 4. Bearer token
                                                                      ▼
                                                     ┌───────────────────────────────────────┐
                                                     │            Microsoft Foundry            │
                                                     │  RBAC check: "Foundry User" role        │
                                                     │  Model deployment (chat completions)    │
                                                     │  ─ grounded via ─▶  Azure AI Search      │
                                                     │                    (internal req. guide) │
                                                     └───────────────────────────────────────┘
```

## Repository layout

See the [README's Repository layout](../README.md#repository-layout) for the full file tree with
a comment on every tracked file — kept in one place so it doesn't drift out of sync with itself.
The short version: `src/Server` is the ASP.NET Core Web API (Controllers/Services/Options/Models),
`src/client` is the React+TS frontend that builds into `src/Server/wwwroot`, `docs/` holds this
file and the Azure setup guide, and `infra/` holds the reference Bicep.

## Key decisions and alternatives considered

| Decision | Chosen approach | Alternative (documented, not implemented) |
|---|---|---|
| Frontend framework | React + TypeScript (Vite), built into `wwwroot/` | Blazor WebAssembly — viable for an all-.NET team; same single-deployment pattern applies, swap `src/client` for a Blazor project and keep the rest of this architecture. |
| Access restriction | Entra ID "Assignment required" + security group, enforced by App Service Easy Auth (zero in-app auth code) | `Microsoft.Identity.Web` + Entra App Roles + `[Authorize(Policy = ...)]` in controllers — better if you need multiple permission tiers (e.g. `Requirements.User` vs `Requirements.Admin`) inside the same app. |
| Foundry auth | System-assigned managed identity + `DefaultAzureCredential`, no keys anywhere | User-assigned managed identity (`Foundry__ClientId`) if the identity needs to be shared across multiple App Service instances or pre-provisioned before the app exists. |
| Foundry SDK | `Azure.AI.OpenAI` (wraps the OpenAI SDK's `ChatClient`) | `Azure.AI.Inference` — the older, more model-agnostic SDK; Microsoft's guidance as of 2026 is to prefer the OpenAI-compatible SDK for chat/completions-style deployments. |
| Response delivery | Server-Sent Events (`text/event-stream`), one `IAsyncEnumerable<string>` per request | SignalR — more overhead, only worth it if you need bidirectional messaging (e.g. server-initiated pushes) beyond a single streamed response. |
| RAG | Fully external: Azure AI Search index + Foundry model deployment/agent, provisioned and linked in Azure (see `docs/AZURE_SETUP.md`) | None — bringing retrieval logic into this app would duplicate what Foundry/AI Search already do server-side and would require the app to hold AI Search credentials. |

## Extending this pattern

- **Multi-turn conversations**: `FoundryChatService.GenerateRequirementsStreamAsync` currently
  sends a single user message per request. To add conversation history, thread a `List<ChatMessage>`
  through from the client (or keep it server-side, keyed by a session ID) instead of constructing
  a single `UserChatMessage`.
- **Citations**: if the Foundry deployment is configured as an agent with file-search/AI Search
  tool calls that return citations, extend `StreamingChatCompletionUpdate` handling in
  `FoundryChatService` to also emit citation metadata as a separate SSE event, and render it in
  `RequirementsOutput.tsx`.
- **Content safety**: Azure AI Content Safety filtering is enabled at the Foundry deployment
  level (see `docs/AZURE_SETUP.md`); a blocked prompt/response surfaces as an error from the SDK
  call, which `RequirementsController` already catches and reports as a generic `error` SSE event.
