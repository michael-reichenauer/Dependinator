metadata description = 'Service Bus namespace with the order queues and the order-events topic, RBAC for senders and receivers, and a private endpoint.'

param prefix string
param location string
param tags object

@description('Identity that publishes orders and events (the apps).')
param senderPrincipalId string

@description('Identity that processes queues and subscriptions (background jobs).')
param receiverPrincipalId string

param privateEndpointSubnetId string
param privateDnsZoneId string

var roleIds = {
  serviceBusDataSender: '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
  serviceBusDataReceiver: '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
}

resource namespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: 'sb-${prefix}'
  location: location
  tags: tags
  sku: {
    name: 'Premium'
    tier: 'Premium'
    capacity: 1
  }
  properties: {
    minimumTlsVersion: '1.2'
    disableLocalAuth: true
    publicNetworkAccess: 'Disabled'
    zoneRedundant: true
  }

  resource ordersQueue 'queues' = {
    name: 'orders'
    properties: {
      lockDuration: 'PT1M'
      maxDeliveryCount: 10
      deadLetteringOnMessageExpiration: true
      defaultMessageTimeToLive: 'P7D'
      requiresDuplicateDetection: true
      duplicateDetectionHistoryTimeWindow: 'PT10M'
    }
  }

  resource paymentsQueue 'queues' = {
    name: 'payments'
    properties: {
      lockDuration: 'PT2M'
      maxDeliveryCount: 5
      deadLetteringOnMessageExpiration: true
      requiresSession: true
    }
  }

  resource thumbnailsQueue 'queues' = {
    name: 'thumbnails'
    properties: {
      lockDuration: 'PT1M'
      maxDeliveryCount: 3
      defaultMessageTimeToLive: 'P1D'
    }
  }

  resource orderEventsTopic 'topics' = {
    name: 'order-events'
    properties: {
      supportOrdering: true
      defaultMessageTimeToLive: 'P3D'
    }

    resource fulfilmentSubscription 'subscriptions' = {
      name: 'fulfilment'
      properties: {
        maxDeliveryCount: 10
        lockDuration: 'PT1M'
        deadLetteringOnMessageExpiration: true
      }

      resource paidOrdersOnly 'rules' = {
        name: 'paid-orders-only'
        properties: {
          filterType: 'SqlFilter'
          sqlFilter: {
            sqlExpression: 'status = \'Paid\''
          }
        }
      }
    }

    resource notificationsSubscription 'subscriptions' = {
      name: 'notifications'
      properties: {
        maxDeliveryCount: 10
        lockDuration: 'PT30S'
      }
    }

    resource analyticsSubscription 'subscriptions' = {
      name: 'analytics'
      properties: {
        maxDeliveryCount: 3
        lockDuration: 'PT30S'
        defaultMessageTimeToLive: 'P1D'
      }
    }
  }
}

resource senderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, senderPrincipalId, roleIds.serviceBusDataSender)
  scope: namespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.serviceBusDataSender)
    principalId: senderPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource receiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, receiverPrincipalId, roleIds.serviceBusDataReceiver)
  scope: namespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.serviceBusDataReceiver)
    principalId: receiverPrincipalId
    principalType: 'ServicePrincipal'
  }
}

module privateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${namespace.name}'
  params: {
    name: 'pe-${prefix}-servicebus'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: namespace.id
    groupId: 'namespace'
    privateDnsZoneId: privateDnsZoneId
  }
}

output namespaceId string = namespace.id
output namespaceFqdn string = '${namespace.name}.servicebus.windows.net'
output ordersQueueName string = namespace::ordersQueue.name
output thumbnailQueueId string = namespace::thumbnailsQueue.id
output orderEventsTopicName string = namespace::orderEventsTopic.name
