using '../main.bicep'

param environmentName = 'uat'
param webImage = readEnvironmentVariable('WEB_IMAGE', 'ghcr.io/example/vantage-approvals-portal:latest')
param apiImage = readEnvironmentVariable('API_IMAGE', 'ghcr.io/example/vantage-approvals-api:latest')
param revisionSuffix = readEnvironmentVariable('REVISION_SUFFIX', 'local')
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME', 'vwapprovaluatsa')
param portalHostname = readEnvironmentVariable('PORTAL_HOSTNAME', 'approvals-uat.example.test')
param campaignSystemBaseUrl = readEnvironmentVariable('CAMPAIGN_SYSTEM_URL', 'https://campaigns-uat.example.test')
param smtpHost = readEnvironmentVariable('SMTP_HOST', 'smtp.example.test')
param smtpPort = 587
param notificationFromAddress = readEnvironmentVariable('NOTIFICATION_FROM', 'reviews@example.test')
param allowedProducerDomain = readEnvironmentVariable('PRODUCER_DOMAIN', 'example.test')

// Secrets come from the pipeline's environment, never from this file.
param producerTokenSigningKey = readEnvironmentVariable('PRODUCER_TOKEN_KEY', '')
param reviewerSessionSigningKey = readEnvironmentVariable('REVIEWER_SESSION_KEY', '')
param campaignSystemApiKey = readEnvironmentVariable('CAMPAIGN_SYSTEM_API_KEY', '')
