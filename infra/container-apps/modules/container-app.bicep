@description('Prefix shared by every resource this module creates.')
param namePrefix string

param location string

param tags object

param environmentId string

param webImage string

param apiImage string

param revisionSuffix string

param portalHostname string

param campaignSystemBaseUrl string

param smtpHost string

param smtpPort int

param notificationFromAddress string

param allowedProducerDomain string

@secure()
param tableConnectionString string

@secure()
param blobConnectionString string

@secure()
param campaignSystemApiKey string

@secure()
param producerTokenSigningKey string

@secure()
param reviewerSessionSigningKey string

// One app, two containers in one revision. They share a network namespace, so the web container
// reaches the API over 127.0.0.1 — the same topology the local Compose stack reproduces, which is
// why the forwarded-header handling behaves identically in both.
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-app'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8081
        transport: 'auto'
        allowInsecure: false
      }
      secrets: [
        {
          name: 'table-connection-string'
          value: tableConnectionString
        }
        {
          name: 'blob-connection-string'
          value: blobConnectionString
        }
        {
          name: 'campaign-system-api-key'
          value: campaignSystemApiKey
        }
        {
          name: 'producer-token-signing-key'
          value: producerTokenSigningKey
        }
        {
          name: 'reviewer-session-signing-key'
          value: reviewerSessionSigningKey
        }
      ]
    }
    template: {
      revisionSuffix: revisionSuffix
      containers: [
        {
          name: 'web'
          image: webImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'API_UPSTREAM'
              value: 'http://127.0.0.1:8080'
            }
          ]
          probes: [
            {
              type: 'Readiness'
              httpGet: {
                path: '/healthz'
                port: 8081
              }
              periodSeconds: 10
            }
          ]
        }
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json('0.75')
            memory: '1.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ConnectionStrings__ApprovalTableStorage'
              secretRef: 'table-connection-string'
            }
            {
              name: 'Snapshots__ConnectionString'
              secretRef: 'blob-connection-string'
            }
            {
              name: 'Snapshots__ContainerName'
              value: 'review-snapshots'
            }
            {
              name: 'Notifications__SmtpHost'
              value: smtpHost
            }
            {
              name: 'Notifications__SmtpPort'
              value: string(smtpPort)
            }
            {
              name: 'Notifications__FromAddress'
              value: notificationFromAddress
            }
            {
              name: 'Notifications__FromName'
              value: 'Vantage Works'
            }
            {
              name: 'ProducerToken__Issuer'
              value: 'vantage-approvals-api'
            }
            {
              name: 'ProducerToken__Audience'
              value: 'vantage-producer-console'
            }
            {
              name: 'ProducerToken__SigningKey'
              secretRef: 'producer-token-signing-key'
            }
            {
              name: 'ProducerToken__AllowedEmailDomain'
              value: allowedProducerDomain
            }
            {
              name: 'ReviewerSession__SigningKey'
              secretRef: 'reviewer-session-signing-key'
            }
            {
              name: 'ReviewerSession__CookieName'
              value: 'vw_review'
            }
            {
              name: 'CampaignSystem__BaseUrl'
              value: campaignSystemBaseUrl
            }
            {
              name: 'CampaignSystem__ApiKey'
              secretRef: 'campaign-system-api-key'
            }
            {
              name: 'AllowedHosts'
              value: portalHostname
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              periodSeconds: 15
            }
            {
              // The readiness check asserts configuration, so a revision missing a setting never
              // takes traffic — it fails here instead of failing its first request.
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              periodSeconds: 10
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
