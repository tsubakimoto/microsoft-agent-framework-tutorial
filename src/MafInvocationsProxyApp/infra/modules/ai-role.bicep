param aiServicesAccountName string
param principalId string

// Azure AI Developer role — grants access to call Foundry endpoints including invocations.
// Role ID: 64702f94-c441-49e6-a78b-ef80e0188fee
var azureAiDeveloperRoleId = '64702f94-c441-49e6-a78b-ef80e0188fee'

resource aiServices 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' existing = {
  name: aiServicesAccountName
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiServices.id, principalId, azureAiDeveloperRoleId)
  scope: aiServices
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', azureAiDeveloperRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
