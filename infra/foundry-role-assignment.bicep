// Separate module so the role assignment can target the Foundry account's resource group, which
// may differ from the resource group the web app is deployed into (see main.bicep).

@description('Name of the existing Microsoft Foundry (Cognitive Services) account.')
param foundryAccountName string

@description('Principal ID of the identity being granted access (the web app''s managed identity).')
param principalId string

@description('Role definition GUID to assign (defaults to "Foundry User" in main.bicep).')
param roleDefinitionId string

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: foundryAccountName
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, principalId, roleDefinitionId)
  scope: foundryAccount
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
  }
}
