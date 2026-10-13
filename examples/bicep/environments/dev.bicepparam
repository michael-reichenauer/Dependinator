using '../webshop.bicep'

param environment = 'dev'
param location = 'swedencentral'
param platformAdminsGroupId = '00000000-0000-0000-0000-000000000001'
param alertEmail = 'platform-team@contoso-webshop.example'
param deployAi = false
param paymentProviderApiKey = readEnvironmentVariable('PAYMENT_PROVIDER_API_KEY', 'replace-me')
