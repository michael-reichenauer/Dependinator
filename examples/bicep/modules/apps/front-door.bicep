metadata description = 'Azure Front Door (Premium) in front of the storefront and the API gateway, with a WAF policy and the custom shop domain.'

param prefix string
param tags object

@description('Custom host name of the shop, e.g. shop.contoso-webshop.example.')
param customDomainName string

param webOriginHostName string

@description('Resource id of the storefront web app, reached over Private Link (no public endpoint).')
param webOriginResourceId string

@description('Region of the storefront, where Front Door creates its private link connection.')
param webOriginLocation string

param apiOriginHostName string
param logAnalyticsWorkspaceId string

resource profile 'Microsoft.Cdn/profiles@2024-02-01' = {
  name: 'afd-${prefix}'
  location: 'global'
  tags: tags
  sku: {
    name: 'Premium_AzureFrontDoor'
  }

  resource endpoint 'afdEndpoints' = {
    name: 'fde-${prefix}'
    location: 'global'
    tags: tags
    properties: {
      enabledState: 'Enabled'
    }

    resource webRoute 'routes' = {
      name: 'web'
      properties: {
        originGroup: {
          id: webOrigins.id
        }
        customDomains: [
          {
            id: shopDomain.id
          }
        ]
        supportedProtocols: [
          'Https'
        ]
        patternsToMatch: [
          '/*'
        ]
        forwardingProtocol: 'HttpsOnly'
        linkToDefaultDomain: 'Enabled'
        httpsRedirect: 'Enabled'
        cacheConfiguration: {
          queryStringCachingBehavior: 'IgnoreQueryString'
          compressionSettings: {
            isCompressionEnabled: true
            contentTypesToCompress: [
              'text/html'
              'text/css'
              'application/javascript'
              'application/json'
            ]
          }
        }
      }
      dependsOn: [
        webOrigins::webApp
      ]
    }

    resource apiRoute 'routes' = {
      name: 'api'
      properties: {
        originGroup: {
          id: apiOrigins.id
        }
        customDomains: [
          {
            id: shopDomain.id
          }
        ]
        supportedProtocols: [
          'Https'
        ]
        patternsToMatch: [
          '/api/*'
        ]
        forwardingProtocol: 'HttpsOnly'
        linkToDefaultDomain: 'Enabled'
        httpsRedirect: 'Enabled'
      }
      dependsOn: [
        apiOrigins::gateway
      ]
    }
  }

  resource webOrigins 'originGroups' = {
    name: 'web'
    properties: {
      loadBalancingSettings: {
        sampleSize: 4
        successfulSamplesRequired: 3
      }
      healthProbeSettings: {
        probePath: '/health'
        probeRequestType: 'GET'
        probeProtocol: 'Https'
        probeIntervalInSeconds: 60
      }
    }

    resource webApp 'origins' = {
      name: 'storefront'
      properties: {
        hostName: webOriginHostName
        originHostHeader: webOriginHostName
        httpPort: 80
        httpsPort: 443
        priority: 1
        weight: 1000
        enabledState: 'Enabled'
        // The private endpoint connection this creates on the web app must be approved once
        // (portal or az network private-endpoint-connection approve) before traffic flows.
        sharedPrivateLinkResource: {
          privateLink: {
            id: webOriginResourceId
          }
          groupId: 'sites'
          privateLinkLocation: webOriginLocation
          requestMessage: 'Front Door origin for the webshop storefront'
        }
      }
    }
  }

  resource apiOrigins 'originGroups' = {
    name: 'api'
    properties: {
      loadBalancingSettings: {
        sampleSize: 4
        successfulSamplesRequired: 3
      }
      healthProbeSettings: {
        probePath: '/status-0123456789abcdef'
        probeRequestType: 'GET'
        probeProtocol: 'Https'
        probeIntervalInSeconds: 60
      }
    }

    resource gateway 'origins' = {
      name: 'api-gateway'
      properties: {
        hostName: apiOriginHostName
        originHostHeader: apiOriginHostName
        httpPort: 80
        httpsPort: 443
        priority: 1
        weight: 1000
        enabledState: 'Enabled'
      }
    }
  }

  resource shopDomain 'customDomains' = {
    name: replace(customDomainName, '.', '-')
    properties: {
      hostName: customDomainName
      tlsSettings: {
        certificateType: 'ManagedCertificate'
        minimumTlsVersion: 'TLS12'
      }
    }
  }

  resource wafAssociation 'securityPolicies' = {
    name: 'waf'
    properties: {
      parameters: {
        type: 'WebApplicationFirewall'
        wafPolicy: {
          id: wafPolicy.id
        }
        associations: [
          {
            domains: [
              {
                id: endpoint.id
              }
              {
                id: shopDomain.id
              }
            ]
            patternsToMatch: [
              '/*'
            ]
          }
        ]
      }
    }
  }
}

resource wafPolicy 'Microsoft.Network/FrontDoorWebApplicationFirewallPolicies@2024-02-01' = {
  name: replace('waf${prefix}', '-', '')
  location: 'global'
  tags: tags
  sku: {
    name: 'Premium_AzureFrontDoor'
  }
  properties: {
    policySettings: {
      enabledState: 'Enabled'
      mode: 'Prevention'
      requestBodyCheck: 'Enabled'
    }
    managedRules: {
      managedRuleSets: [
        {
          ruleSetType: 'Microsoft_DefaultRuleSet'
          ruleSetVersion: '2.1'
          ruleSetAction: 'Block'
        }
        {
          ruleSetType: 'Microsoft_BotManagerRuleSet'
          ruleSetVersion: '1.0'
        }
      ]
    }
  }
}

resource profileDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: profile
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'FrontDoorAccessLog'
        enabled: true
      }
      {
        category: 'FrontDoorWebApplicationFirewallLog'
        enabled: true
      }
    ]
  }
}

output frontDoorId string = profile.properties.frontDoorId
output endpointHostName string = profile::endpoint.properties.hostName
output customDomainValidationToken string = profile::shopDomain.properties.validationProperties.validationToken
