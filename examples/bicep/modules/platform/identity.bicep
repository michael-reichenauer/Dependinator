metadata description = 'User-assigned managed identities: one for the customer-facing apps, one for background jobs.'

param prefix string
param location string
param tags object

resource appsIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-apps'
  location: location
  tags: tags
}

resource jobsIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-jobs'
  location: location
  tags: tags
}

output appsIdentityId string = appsIdentity.id
output appsIdentityPrincipalId string = appsIdentity.properties.principalId
output appsIdentityClientId string = appsIdentity.properties.clientId
output jobsIdentityId string = jobsIdentity.id
output jobsIdentityPrincipalId string = jobsIdentity.properties.principalId
output jobsIdentityClientId string = jobsIdentity.properties.clientId
