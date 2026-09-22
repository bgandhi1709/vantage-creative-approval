targetScope = 'resourceGroup'

@description('Short environment name, used in every resource name.')
param environmentName string

@description('Azure region for every resource in this deployment.')
param location string = resourceGroup().location

@description('Container image for the web container (SPA plus its reverse proxy).')
param webImage string

@description('Container image for the API container.')
param apiImage string

@description('Revision suffix, so each deployment produces a distinct, addressable revision.')
param revisionSuffix string

@description('Name of an existing storage account holding the approval tables and snapshot blobs.')
param storageAccountName string

@description('Resource group of that storage account.')
param storageAccountResourceGroup string = resourceGroup().name

@description('Hostname the portal is served on. Review links in outgoing mail are built from the request, so this is used for the CORS/allowed-host configuration only.')
param portalHostname string

@description('Base URL of the upstream campaign system.')
param campaignSystemBaseUrl string

@description('SMTP relay used for review notifications.')
param smtpHost string

param smtpPort int = 25

param notificationFromAddress string

@description('Email domain allowed to hold a producer console token.')
param allowedProducerDomain string

@secure()
param producerTokenSigningKey string

@secure()
param reviewerSessionSigningKey string

@secure()
param campaignSystemApiKey string

param tags object = {
  workload: 'creative-approval'
  environment: environmentName
}

var namePrefix = 'vw-approval-${environmentName}'

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    namePrefix: namePrefix
    location: location
    tags: tags
  }
}

// The storage account is referenced, not created: it outlives any single deployment of this app,
// and its keys are resolved at deploy time so no connection string is ever passed in as a value.
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
  scope: resourceGroup(storageAccountResourceGroup)
}

module app 'modules/container-app.bicep' = {
  name: 'container-app'
  params: {
    namePrefix: namePrefix
    location: location
    tags: tags
    environmentId: monitoring.outputs.managedEnvironmentId
    webImage: webImage
    apiImage: apiImage
    revisionSuffix: revisionSuffix
    tableConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
    blobConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
    portalHostname: portalHostname
    campaignSystemBaseUrl: campaignSystemBaseUrl
    campaignSystemApiKey: campaignSystemApiKey
    smtpHost: smtpHost
    smtpPort: smtpPort
    notificationFromAddress: notificationFromAddress
    allowedProducerDomain: allowedProducerDomain
    producerTokenSigningKey: producerTokenSigningKey
    reviewerSessionSigningKey: reviewerSessionSigningKey
  }
}

output portalFqdn string = app.outputs.fqdn
output applicationInsightsName string = monitoring.outputs.applicationInsightsName
