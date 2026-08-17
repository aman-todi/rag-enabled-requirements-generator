// Reference infrastructure-as-code for the pattern documented in docs/AZURE_SETUP.md.
//
// Provisions: an App Service Plan + Linux App Service running the .NET app, a system-assigned
// managed identity, App Service Authentication (Easy Auth) wired to an existing Entra ID App
// registration, Application Insights, and (optionally) a role assignment granting the app's
// managed identity the "Foundry User" role on an existing Microsoft Foundry account.
//
// This does NOT provision Azure AI Search or the Foundry model deployment/agent — per the
// project scope, that RAG setup is provisioned separately. `foundryAccountName` /
// `foundryResourceGroupName` below must point at that already-existing resource.

@description('Base name used to derive resource names, e.g. "reqgen-embedded".')
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('App Service Plan SKU. B1 for dev/test, P1v3+ for VNet integration in production.')
param appServicePlanSku string = 'P1v3'

@description('.NET runtime version string for the Linux App Service.')
param linuxFxVersion string = 'DOTNETCORE|10.0'

@description('Entra ID App registration (client) ID used for App Service authentication.')
param entraClientId string

@description('Entra ID tenant ID.')
param entraTenantId string = subscription().tenantId

@description('Endpoint of the existing Microsoft Foundry resource/project, e.g. https://<resource>.services.ai.azure.com/')
param foundryEndpoint string

@description('Name of the chat model deployment on the Foundry resource.')
param foundryDeploymentName string

@description('Name of the existing Microsoft Foundry (Cognitive Services) account to grant this app access to. Leave empty to skip the role assignment and grant access manually.')
param foundryAccountName string = ''

@description('Resource group containing the existing Foundry account, if different from this deployment''s resource group.')
param foundryResourceGroupName string = resourceGroup().name

@description('Subnet resource ID for outbound VNet integration (regional VNet integration). Leave empty to skip.')
param vnetIntegrationSubnetId string = ''

var appServicePlanName = '${appName}-plan'
var webAppName = '${appName}-app'
var appInsightsName = '${appName}-ai'

// "Foundry User" built-in role (formerly "Azure AI User"); role definition GUID is unchanged by
// the 2026 rename. See docs/AZURE_SETUP.md section 1.
var foundryUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d'

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    virtualNetworkSubnetId: vnetIntegrationSubnetId
    vnetRouteAllEnabled: !empty(vnetIntegrationSubnetId)
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'Foundry__Endpoint', value: foundryEndpoint }
        { name: 'Foundry__DeploymentName', value: foundryDeploymentName }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
      ]
    }
  }
}

// Easy Auth: require sign-in via Entra ID for every request. Combine with "Assignment required"
// on the App registration (Entra ID portal) to restrict sign-in to a specific security group —
// that step cannot be expressed in this resource and must be set on the Enterprise Application
// (see docs/AZURE_SETUP.md section 2).
resource webAppAuth 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: webApp
  name: 'authsettingsV2'
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'RedirectToLoginPage'
      redirectToProvider: 'azureactivedirectory'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: entraClientId
          openIdIssuer: 'https://login.microsoftonline.com/${entraTenantId}/v2.0'
        }
        validation: {
          defaultAuthorizationPolicy: {
            allowedApplications: [
              entraClientId
            ]
          }
        }
      }
    }
  }
}

// Grants this app's managed identity the "Foundry User" role on the existing Foundry account, so
// it can call the model deployment via DefaultAzureCredential. Skipped when foundryAccountName is
// left empty (grant access manually instead — see docs/AZURE_SETUP.md).
module foundryRoleAssignment 'foundry-role-assignment.bicep' = if (!empty(foundryAccountName)) {
  name: '${appName}-foundry-role-assignment'
  scope: resourceGroup(foundryResourceGroupName)
  params: {
    foundryAccountName: foundryAccountName
    principalId: webApp.identity.principalId
    roleDefinitionId: foundryUserRoleId
  }
}

output webAppName string = webApp.name
output webAppDefaultHostname string = webApp.properties.defaultHostName
output webAppPrincipalId string = webApp.identity.principalId
