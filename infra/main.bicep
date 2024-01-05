targetScope = 'subscription'

@description('Specifies the location for all resources.')
param location string

@minLength(1)
@description('The URL of your first Azure OpenAI endpoint in the following format: https://[name].openai.azure.com')
param backend_1_url string

@description('The priority of your first OpenAI endpoint (lower number means higher priority)')
param backend_1_priority int

@minLength(1)
@description('The API key your first OpenAI endpoint')
param backend_1_api_key string

@minLength(1)
@description('The URL of your second Azure OpenAI endpoint in the following format: https://[name].openai.azure.com')
param backend_2_url string

@description('The priority of your second OpenAI endpoint (lower number means higher priority)')
param backend_2_priority int

@minLength(1)
@description('The API key your second OpenAI endpoint')
param backend_2_api_key string

@minLength(1)
@maxLength(64)
@description('Name which is used to generate a short unique hash for each resource')
param name string

var resourceToken = toLower(uniqueString(subscription().id, name, location))
var prefix = '${name}-${resourceToken}'
var tags = { 'azd-env-name': name }

resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: '${name}-rg'
  location: location
  tags: tags
}

// Monitor application with Azure Monitor
module monitoring 'core/monitor/monitoring.bicep' = {
  name: 'monitoring'
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    applicationInsightsDashboardName: '${prefix}-appinsights-dashboard'
    applicationInsightsName: '${prefix}-appinsights'
    logAnalyticsName: '${take(prefix, 50)}-loganalytics' // Max 63 chars
  }
}

// Web frontend
module web 'web.bicep' = {
  name: 'web'
  scope: resourceGroup
  params: {
    name: replace('${take(prefix, 19)}-ca', '--', '-')
    location: location
    tags: tags
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
    identityName: '${prefix}-id-web'
    containerAppsEnvironmentName: '${prefix}-containerapps-env'
    containerRegistryName: '${replace(prefix, '-', '')}registry'
    backend_1_url: backend_1_url
    backend_1_priority: backend_1_priority
    backend_1_api_key: backend_1_api_key
    backend_2_url: backend_2_url
    backend_2_priority: backend_2_priority
    backend_2_api_key: backend_2_api_key
  }
}
