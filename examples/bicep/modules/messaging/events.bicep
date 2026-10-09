metadata description = 'Event Grid: blob uploads of product images queue thumbnail jobs, and a custom topic publishes order events to partners.'

param prefix string
param location string
param tags object

@description('Storage account whose blob events are routed.')
param storageAccountId string

@description('Service Bus queue that receives thumbnail jobs.')
param thumbnailQueueId string

resource storageEvents 'Microsoft.EventGrid/systemTopics@2022-06-15' = {
  name: 'evgst-${prefix}-storage'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    source: storageAccountId
    topicType: 'Microsoft.Storage.StorageAccounts'
  }

  resource productImageUploaded 'eventSubscriptions' = {
    name: 'product-image-uploaded'
    properties: {
      eventDeliverySchema: 'CloudEventSchemaV1_0'
      destination: {
        endpointType: 'ServiceBusQueue'
        properties: {
          resourceId: thumbnailQueueId
        }
      }
      filter: {
        includedEventTypes: [
          'Microsoft.Storage.BlobCreated'
        ]
        subjectBeginsWith: '/blobServices/default/containers/product-images/'
      }
      retryPolicy: {
        maxDeliveryAttempts: 10
        eventTimeToLiveInMinutes: 1440
      }
    }
  }
}

resource partnerTopic 'Microsoft.EventGrid/topics@2022-06-15' = {
  name: 'evgt-${prefix}-partners'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    inputSchema: 'CloudEventSchemaV1_0'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
  }
}

output partnerTopicEndpoint string = partnerTopic.properties.endpoint
