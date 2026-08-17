# Azure Configuration Guide

This covers the Azure-side setup this app assumes. It does **not** cover provisioning Azure AI
Search or the Foundry model deployment/agent itself — per the project brief, that RAG setup
(indexing the internal requirements-writing guide, linking the index to a deployed chat model in
Microsoft Foundry) is handled separately in Azure. This document covers everything needed so the
web app can securely call that already-deployed model and so only the intended employees can
reach it.

All role/portal names reflect Microsoft's naming as of 2026 (Microsoft Foundry; the RBAC role
formerly called "Azure AI User" is now **"Foundry User"** — see
[Role-based access control for Microsoft Foundry](https://learn.microsoft.com/en-us/azure/foundry/concepts/rbac-foundry)).
The old role names still work during the rename rollout if your resource predates it.

## 1. Managed identity: App Service → Foundry

The App Service must be able to call the Foundry model deployment without any stored secret.

1. **Enable the identity** on the App Service:
   - Portal: App Service → **Identity** → System assigned → Status: **On** → Save.
   - CLI: `az webapp identity assign --name <app-name> --resource-group <rg-name>`
2. **Grant it access to Foundry**:
   - Portal: open the Foundry resource/project → **Access control (IAM)** → **Add role
     assignment** → role **Foundry User** (a.k.a. "Azure AI User") → assign to the App Service's
     managed identity → scope it to the specific Foundry account/project, not the whole
     subscription.
   - CLI:
     ```bash
     principalId=$(az webapp identity show --name <app-name> --resource-group <rg-name> --query principalId -o tsv)
     az role assignment create \
       --assignee-object-id "$principalId" \
       --assignee-principal-type ServicePrincipal \
       --role "Foundry User" \
       --scope "/subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<foundry-account>"
     ```
3. **App settings**: set `Foundry__Endpoint` and `Foundry__DeploymentName` on the App Service (see
   README.md "Configuration"). Leave `Foundry__ApiKey` unset in Azure — the app falls back to
   `DefaultAzureCredential`, which resolves to the managed identity automatically when running in
   App Service.
4. **Local development**: give your own account (or a dev security group) the same **Foundry
   User** role on the resource, then `az login` locally — `DefaultAzureCredential` picks up your
   CLI session the same way it picks up the managed identity in Azure.

If you use a **user-assigned** managed identity instead (e.g. shared across staging/prod slots or
provisioned ahead of the app via IaC), assign it to the App Service, grant *it* the Foundry User
role, and set `Foundry__ClientId` to its client ID.

## 2. Restricting access to specific employees (Entra ID)

This is enforced at the identity provider, before any request reaches the app — no code in this
repo performs authorization decisions.

1. **App Service authentication ("Easy Auth")**:
   - Portal: App Service → **Authentication** → **Add identity provider** → **Microsoft**.
   - Create a new App registration (or pick an existing one) — this becomes the Entra ID app
     that represents this web app.
   - Under **Restrict access**, choose **Require authentication**. Unauthenticated requests get
     redirected to Entra ID sign-in and never reach the ASP.NET Core app.
   - Set the **Token store** on (default) so `X-MS-CLIENT-PRINCIPAL-*` headers are populated for
     `GET /api/user/me`.
2. **Require assignment** (the actual team restriction):
   - Portal: **Microsoft Entra ID** → **Enterprise applications** → select the app registration
     created above → **Properties** → **Assignment required?** → **Yes** → Save.
   - Then **Users and groups** → **Add user/group** → select the Entra **security group** for the
     team this tool is for (e.g. `SG-Embedded-SW-Team`) — prefer a group over individual users so
     membership changes don't require touching the app.
   - Anyone not in that group is blocked at the Microsoft login page
     (`AADSTS50105: User is not assigned to a role for the application`) — the request never
     reaches App Service.
3. **Tenant restriction** (defense in depth): under the app registration's authentication
   settings, restrict token issuance to your own tenant only, so a guest/foreign-tenant account
   can't authenticate even if it were somehow assigned.

This is "Option 1" from the design discussion — restriction lives entirely in Entra ID
configuration. If the tool later needs multiple in-app permission tiers (e.g. viewers vs. editors
of a saved requirements library), add Entra **App Roles** and enforce them with
`Microsoft.Identity.Web` + `[Authorize(Policy = ...)]` in the controllers instead of/alongside
Easy Auth — see `docs/ARCHITECTURE.md` for that alternative.

## 3. Network hardening (VNet + private endpoints)

For an internal tool, keep both the app and its dependencies off the public internet where
possible:

- **App Service**: enable **VNet integration** (outbound) so calls to Foundry/AI Search traverse
  your private network instead of the public internet.
- **Microsoft Foundry** and **Azure AI Search**: disable public network access and expose them via
  **Azure Private Endpoints** inside the same (or peered) VNet. Add **Private DNS zones** for
  `privatelink.services.ai.azure.com` (Foundry) and `privatelink.search.windows.net` (AI Search)
  so the app's DNS resolution of the Foundry endpoint resolves to the private IP.
- **Inbound to the app**: if the App Service Plan tier supports it (Premium v3+), add an
  **App Service Access Restriction** or front it with **Azure Front Door / Application Gateway +
  Private Link** if you need to keep the public endpoint fully closed while still allowing
  corporate-network users through.
- **NSGs**: apply network security groups to the integration subnet limiting outbound traffic to
  the Foundry/AI Search private endpoints and required Azure management endpoints only.

## 4. Content safety and observability

- Microsoft Foundry model deployments have built-in content filtering (prompt injection /
  jailbreak detection, harmful content categories) configurable per deployment — enable it on the
  Foundry side; a filtered request surfaces to this app as an SDK exception, which
  `RequirementsController` reports as a generic error to the user without leaking details.
- Add **Application Insights** to the App Service (Portal → Application Insights → Turn on, or via
  the `APPLICATIONINSIGHTS_CONNECTION_STRING` app setting) to capture request telemetry and the
  structured logs already emitted by `RequirementsController` (caller name from
  `X-MS-CLIENT-PRINCIPAL-NAME`, prompt length — not prompt content, to avoid logging potentially
  sensitive design details).

## 5. Summary checklist

- [ ] App Service system-assigned (or user-assigned) managed identity enabled
- [ ] Managed identity granted **Foundry User** role, scoped to the Foundry account/project
- [ ] `Foundry__Endpoint` / `Foundry__DeploymentName` app settings configured
- [ ] Entra ID App registration created and linked via App Service Authentication
- [ ] "Assignment required" set to **Yes**, target security group assigned
- [ ] Tenant restricted to your own org (no guest access)
- [ ] App Service VNet integration enabled; Foundry + AI Search behind Private Endpoints
- [ ] Public network access disabled on Foundry / AI Search resources
- [ ] Application Insights connected for telemetry/audit logging
