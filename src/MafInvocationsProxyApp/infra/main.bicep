targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment used for naming resources')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Name of the resource group to create or use')
param resourceGroupName string = 'rg-${environmentName}'

@description('Name of the existing AI Services (Foundry) resource to grant access to')
param aiServicesAccountName string

@description('Resource group containing the existing AI Services resource')
param aiServicesResourceGroupName string

@minLength(1)
@description('Full Foundry invocations endpoint URL (including ?api-version=v1). Set via: azd env set FOUNDRY_INVOCATIONS_ENDPOINT <url>')
param foundryInvocationsEndpoint string

var tags = { 'azd-env-name': environmentName }
var resourceToken = toLower(uniqueString(subscription().id, resourceGroupName, location))

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module storage 'modules/storage.bicep' = {
  scope: rg
  name: 'storage'
  params: {
    name: 'st${resourceToken}'
    location: location
    tags: tags
  }
}

module app 'modules/functionapp.bicep' = {
  scope: rg
  name: 'functionapp'
  params: {
    appName: 'func-${resourceToken}'
    planName: 'plan-${resourceToken}'
    location: location
    tags: tags
    storageAccountName: storage.outputs.name
    foundryInvocationsEndpoint: foundryInvocationsEndpoint
  }
}

// Grant Azure AI Developer role on the existing AI Services resource so the
// Function App's managed identity can call the Foundry invocations endpoint.
module aiRole 'modules/ai-role.bicep' = {
  scope: resourceGroup(aiServicesResourceGroupName)
  name: 'ai-role'
  params: {
    aiServicesAccountName: aiServicesAccountName
    principalId: app.outputs.principalId
  }
}

output AZURE_RESOURCE_GROUP string = resourceGroupName
output AZURE_FUNCTION_APP_NAME string = app.outputs.name
output AZURE_FUNCTION_APP_URI string = app.outputs.uri
output FOUNDRY_INVOCATIONS_ENDPOINT string = foundryInvocationsEndpoint
