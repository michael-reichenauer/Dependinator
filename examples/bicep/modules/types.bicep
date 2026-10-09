// Shared types and functions of the webshop platform, imported by the orchestrator and modules.

@export()
@description('Environments the platform is deployed to.')
type environmentName = 'dev' | 'test' | 'prod'

@export()
@description('Address space of the virtual network and the CIDR of each subnet.')
type networkSettings = {
  addressSpace: string
  subnets: {
    @description('App Service (web apps) regional VNet integration.')
    apps: string
    @description('Container Apps environment infrastructure (needs a /23 or larger).')
    containerApps: string
    @description('Private endpoints of the data stores and platform services.')
    privateEndpoints: string
    @description('Function app regional VNet integration.')
    integration: string
  }
}

@export()
type sqlSettings = {
  @description('SKU of the catalog and orders databases, e.g. GP_S_Gen5_2 (serverless) or GP_Gen5_4.')
  skuName: string
  zoneRedundant: bool
}

@export()
@description('Builds the globally unique, lowercase, dash-free name a storage-like resource needs.')
func uniqueResourceName(prefix string, suffix string, salt string) string =>
  toLower(replace('${prefix}${suffix}${substring(uniqueString(salt), 0, 6)}', '-', ''))
