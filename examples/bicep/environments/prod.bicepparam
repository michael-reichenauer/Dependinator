using '../webshop.bicep'

param environment = 'prod'
param location = 'swedencentral'
param platformAdminsGroupId = '00000000-0000-0000-0000-000000000001'
param alertEmail = 'platform-oncall@contoso-webshop.example'
param imageTags = {
  catalog: '2026.10.3'
  checkout: '2026.10.1'
}
param deployAi = true
param paymentProviderApiKey = readEnvironmentVariable('PAYMENT_PROVIDER_API_KEY')
