# Simulink Requirements Generator

An internal chat-style tool that turns a short prompt (e.g. *"write complete testable
requirements for an RC filter module"*) into a set of embedded-software requirements formatted
according to the internal requirements-writing guide, using a retrieval-augmented (RAG) chat model
deployed in Microsoft Foundry.

This repository contains the web app only: an ASP.NET Core backend that calls the already-deployed
Foundry model, and a React/TypeScript frontend that renders the streamed output. **Provisioning
Azure AI Search, indexing the internal guide, and deploying/grounding the chat model in Microsoft
Foundry are all handled separately in Azure** — see `docs/AZURE_SETUP.md` for what this app expects
to already exist.

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — design pattern, request flow, repo layout
- [`docs/AZURE_SETUP.md`](docs/AZURE_SETUP.md) — managed identity, RBAC, Entra ID access
  restriction, VNet, content safety
- [`infra/main.bicep`](infra/main.bicep) — reference IaC for the App Service + identity + auth

## Stack

- **Backend**: ASP.NET Core Web API (.NET 10), `Azure.AI.OpenAI` + `Azure.Identity` for keyless
  calls to Microsoft Foundry, Server-Sent Events for streaming responses.
- **Frontend**: React + TypeScript, built with Vite, compiled directly into the backend's
  `wwwroot/` so the whole app ships as a single deployable unit.
- **Hosting**: Azure App Service (Linux), single instance serving both the API and the SPA —
  same origin, no CORS.
- **Auth**: Azure App Service built-in authentication ("Easy Auth") against Microsoft Entra ID,
  restricted to a specific security group ("Assignment required").

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
  - App Service's GA (non-preview) runtime rollout for .NET 10 may still be completing in some
    regions at time of writing. If your target region only offers `DOTNETCORE|10.0` as *Preview*
    and that's not acceptable, target `net8.0` instead (LTS, supported through Nov 2026) by
    changing `TargetFramework` in `src/Server/Server.csproj` — no other code changes are needed.
- [Node.js 20+](https://nodejs.org/) and npm (for the React client)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli), logged in
  (`az login`) with access to the target subscription
- An existing Microsoft Foundry model deployment already linked to the internal requirements
  guide's Azure AI Search index (see `docs/AZURE_SETUP.md`)

## Repository layout

Every tracked file, with what it's for:

```
.
├── README.md                        This file — config, build, and publish steps
├── .env.example                     Every environment variable the app reads, documented
├── .gitignore                       Excludes build output (bin/, obj/, wwwroot/, node_modules/, dist/) and .env
│
├── docs/
│   ├── ARCHITECTURE.md              Design pattern, request-flow diagram, decisions-vs-alternatives
│   └── AZURE_SETUP.md               Azure-side setup: managed identity, RBAC, Entra ID restriction, VNet
│
├── infra/
│   ├── main.bicep                   App Service + managed identity + Easy Auth + App Insights (IaC reference)
│   ├── foundry-role-assignment.bicep  Module: grants the app's identity the "Foundry User" role; kept separate
│   │                                   so it can target the Foundry account's own resource group
│   └── main.parameters.example.json Template parameter values for main.bicep — copy, fill in, then deploy
│
└── src/
    ├── Server/                      ASP.NET Core Web API (.NET 10) — also serves the built React app
    │   ├── Server.csproj             Project file: PackageReferences (Azure.AI.OpenAI, Azure.Identity) plus
    │   │                              the BuildClient target that runs `npm run build` into wwwroot/ on publish
    │   ├── Program.cs                 Host setup: DI registration, forwarded headers, static files + SPA
    │   │                              fallback routing, /healthz
    │   ├── appsettings.json           Default configuration; Foundry:* keys left blank, set via env vars
    │   ├── appsettings.Development.json  Local-dev-only override (more verbose logging)
    │   │
    │   ├── Controllers/
    │   │   ├── RequirementsController.cs  POST /api/requirements/generate — streams model output as
    │   │   │                              Server-Sent Events
    │   │   └── UserController.cs          GET /api/user/me — reads the Easy Auth identity headers so the
    │   │                                  frontend can show "Signed in as <name>"
    │   │
    │   ├── Services/
    │   │   ├── IFoundryChatService.cs     Interface for the Foundry-calling service (keeps FoundryChatService
    │   │   │                              swappable/mockable behind DI)
    │   │   └── FoundryChatService.cs      Calls the Foundry chat deployment via Azure.AI.OpenAI +
    │   │                                  DefaultAzureCredential; yields streamed text chunks
    │   │
    │   ├── Options/
    │   │   └── FoundryOptions.cs          Strongly-typed settings bound from the "Foundry" config section
    │   │                                  (Endpoint, DeploymentName, ClientId, ApiKey, SystemPrompt)
    │   │
    │   ├── Models/
    │   │   └── RequirementsModels.cs      Request DTO (GenerateRequirementsRequest) for the generate endpoint
    │   │
    │   ├── Properties/
    │   │   └── launchSettings.json        Local `dotnet run`/IDE profile — port 5080, Development environment
    │   │
    │   └── wwwroot/                       React production build lands here at publish time
    │                                      (git-ignored — not committed, regenerated by `npm run build`)
    │
    └── client/                      React + TypeScript frontend (Vite)
        ├── package.json              Dependencies (react, react-dom) and scripts (dev, build, preview, lint)
        ├── package-lock.json         npm's locked dependency graph — commit as-is, don't hand-edit
        ├── vite.config.ts            Build outDir -> ../Server/wwwroot; dev-server proxy for /api -> backend
        ├── tsconfig.json              Root TypeScript config; references the two configs below
        ├── tsconfig.app.json          Compiler options for the browser app code (src/)
        ├── tsconfig.node.json         Compiler options for Vite's own config file (vite.config.ts)
        ├── eslint.config.js           ESLint flat config (React hooks + TypeScript rules) for `npm run lint`
        ├── index.html                 Vite entry HTML — mounts the app at #root, loads src/main.tsx
        │
        └── src/
            ├── main.tsx                React entry point: renders <App /> into the DOM, imports global styles
            ├── App.tsx                 Root component: prompt/output state, streaming lifecycle, layout
            ├── styles.css              Global stylesheet for the chat UI (layout, colors, buttons, output pane)
            ├── vite-env.d.ts           Ambient type declarations for Vite's client APIs (import.meta.env, etc.)
            │
            ├── api/
            │   └── requirementsApi.ts   fetch + SSE-stream parsing for /api/requirements/generate, plus
            │                            fetchCurrentUser() for /api/user/me
            │
            └── components/
                ├── AppHeader.tsx           Top bar: app title and signed-in user display
                ├── PromptComposer.tsx      Textarea + submit/cancel controls for the component prompt
                └── RequirementsOutput.tsx  Renders the streamed requirements text, loading state, errors,
                                            and a copy-to-clipboard button
```

## Configuration

The app reads configuration from ASP.NET Core's standard sources (`appsettings.json` →
`appsettings.{Environment}.json` → environment variables), with environment variables taking
precedence — this is how App Service "Application settings" reach the app in Azure.

Copy `.env.example` to `.env` for local reference (the app itself does not load `.env` files — see
below for how to actually set these locally) and fill in:

| Variable | Required | Description |
|---|---|---|
| `Foundry__Endpoint` | Yes | Base endpoint of the Foundry resource/project hosting the model deployment. |
| `Foundry__DeploymentName` | Yes | Name of the chat model deployment to call. |
| `Foundry__ClientId` | No | Client ID of a user-assigned managed identity. Omit to use the App Service's system-assigned identity. |
| `Foundry__ApiKey` | No (local only) | API key fallback for local dev without `az login`. Never set in Azure. |
| `Foundry__SystemPrompt` | No | Extra system message; the primary grounding lives in Azure AI Search/Foundry, not here. |
| `ASPNETCORE_ENVIRONMENT` | No | `Development` locally, `Production` in Azure (App Service sets this by default). |
| `VITE_API_PROXY_TARGET` | No | Where `npm run dev` proxies `/api` calls; defaults to `http://localhost:5080`. |

In Azure, set the `Foundry__*` variables as **App Service → Configuration → Application settings**
(or via the Bicep template / `az webapp config appsettings set`). See `docs/AZURE_SETUP.md` for the
managed identity and RBAC steps these depend on.

## Local development

Run the backend and frontend as two processes, using Vite's dev server proxy so the SPA talks to
the API the same way it will in production (same relative `/api` paths):

```bash
# Terminal 1 — backend
cd src/Server
export Foundry__Endpoint="https://<your-foundry-resource>.services.ai.azure.com/"
export Foundry__DeploymentName="<deployment-name>"
az login   # so DefaultAzureCredential can authenticate as you
dotnet run

# Terminal 2 — frontend
cd src/client
npm install
npm run dev
```

Open the URL Vite prints (typically `http://localhost:5173`). API requests are proxied to the
backend at `http://localhost:5080` (see `src/client/vite.config.ts` and
`src/Server/Properties/launchSettings.json`).

## Build

A production build compiles the React app straight into the backend's `wwwroot/` and produces a
single publishable output:

```bash
dotnet publish src/Server/Server.csproj -c Release -o ./publish
```

The `BuildClient` MSBuild target in `Server.csproj` runs `npm ci` + `npm run build` in
`src/client` automatically before publish — no separate frontend build step is required in CI.

To build/test the frontend in isolation:

```bash
cd src/client
npm ci
npm run build   # type-checks and emits into ../Server/wwwroot
npm run lint
```

## Publish to Azure App Service

### One-time environment setup

1. Create the resource group, App Service Plan, and App Service (or deploy `infra/main.bicep` —
   see below).
2. Follow `docs/AZURE_SETUP.md` to: enable the managed identity, grant it the **Foundry User**
   role on the Foundry resource, configure Entra ID authentication with "Assignment required", and
   (recommended) enable VNet integration.
3. Set the `Foundry__Endpoint` / `Foundry__DeploymentName` app settings.

#### Deploy infrastructure with Bicep

```bash
az group create --name <rg-name> --location <region>

az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.example.json
```

Copy `infra/main.parameters.example.json` to a real parameters file first and fill in your actual
values (App registration client ID, tenant ID, Foundry endpoint/deployment, etc.) — the example
file is a template, not meant to be deployed as-is.

### Deploying the app

```bash
dotnet publish src/Server/Server.csproj -c Release -o ./publish
cd publish
zip -r ../publish.zip .
cd ..

az webapp deploy \
  --resource-group <rg-name> \
  --name <app-name> \
  --src-path publish.zip \
  --type zip
```

Or wire the same `dotnet publish` + `az webapp deploy` (or `az webapp deployment source config-zip`)
commands into your CI/CD pipeline (GitHub Actions / Azure DevOps) for repeatable deployments.

### Verify

- `https://<app-name>.azurewebsites.net/healthz` returns `{"status":"healthy"}`. If you enabled
  "Require authentication" for all routes, this path will redirect to sign-in like any other —
  add `/healthz` to Easy Auth's excluded paths (or use App Service's separate **Health check**
  feature, which probes the app directly and isn't subject to Easy Auth) if you need an
  unauthenticated liveness check.
- Signing in with an account **not** in the assigned security group should be rejected at the
  Microsoft login page (`AADSTS50105`), before reaching the app.
- Signing in with an assigned account should load the chat UI, show "Signed in as <name>" in the
  header, and successfully stream a response for a prompt like *"write complete testable
  requirements for an RC filter module"*.

## Notes on the RAG setup this app depends on

This app assumes the Foundry model deployment it calls is already configured (in Azure, outside
this repo) to ground its responses in the internal requirements-writing guide — either via a
Foundry Agent with a file-search/Azure AI Search tool, or an "on your data"-style deployment
configuration. The backend does not pass retrieval parameters, index names, or search filters; it
sends the user's prompt and streams back the model's response as-is. If retrieval behavior needs
to change (which documents are indexed, how strongly the guide is enforced, citation behavior),
that's a Foundry/Azure AI Search configuration change, not a code change here.
