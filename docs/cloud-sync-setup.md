# Cloud Sync Setup

This document captures the deployment and local setup needed for the cloud sync feature.

## Static Web App Auth

The WASM app is configured to use a custom OpenID Connect provider named `entraExternalId`.

The login URL is:

- `/.auth/login/entraExternalId`

The callback URL that must be registered with the identity provider is:

- `https://<your-site>/.auth/login/entraExternalId/callback`

Because custom authentication is enabled, preconfigured providers are disabled by Azure Static Web Apps.

## Static Web App Application Settings

Set these application settings in the Static Web App:

- `ENTRA_EXTERNAL_ID_CLIENT_ID`
- `ENTRA_EXTERNAL_ID_CLIENT_SECRET`
- `CloudSync__ContainerName`
- `CloudSync__MaxUserQuotaBytes`
- `CloudSync__StorageConnectionString`
- `CloudSync__OpenIdConfigurationUrl`
- `CloudSync__BearerAudience`

Do not use `AzureWebJobsStorage` for your application blob storage in the deployed Static Web App.

Azure Static Web Apps managed Functions reserve `AzureWeb...` settings for the platform runtime.

Use `CloudSync__StorageConnectionString` for the storage account that should hold synced models.

`AzureWebJobsStorage` remains valid for local Functions development in `Api/local.settings.json`.

`CloudSync__OpenIdConfigurationUrl` and `CloudSync__BearerAudience` are used by the API to validate bearer tokens from the VS Code extension host.

## OIDC Metadata Placeholder

`[staticwebapp.config.json](/workspaces/Dependinator/Dependinator.Wasm/staticwebapp.config.json)` contains a placeholder for:

- `https://<ENTRA_EXTERNAL_ID_ISSUER>/.well-known/openid-configuration`

Replace `<ENTRA_EXTERNAL_ID_ISSUER>` with the issuer host/path from your Entra External ID tenant.

This is intentionally left as a placeholder because the exact issuer URL depends on your tenant configuration.

## Local Run

Use:

- `./run-sync`

What it does:

1. Publishes `Dependinator.Wasm`
2. Copies `Api/local.settings.example.json` to `Api/local.settings.json` if needed
3. Starts Azurite
4. Starts SWA CLI with the API project attached

Notes:

- `./run-sync` uses same-origin `/api` through SWA CLI, which is closer to production than calling the Functions host directly.
- Local auth emulation can still require some manual verification depending on your provider setup.

## VS Code Extension Sync

The VS Code webview host cannot rely on SWA browser cookies for cloud sync.

For VS Code cloud sync, create a separate Entra External ID app registration for a public client:

1. In the external tenant, create a new app registration, for example `Dependinator VS Code Sync`.
2. Enable public client flows on that app registration.
3. Use the same user flow/OpenID metadata URL that the site uses.

Then configure these VS Code settings:

- `dependinator.cloudSync.baseUrl`
  - Example: `https://dependinator.com`
- `dependinator.cloudSync.openIdConfigurationUrl`
  - The exact `.../.well-known/openid-configuration` URL for the Entra External ID user flow
- `dependinator.cloudSync.clientId`
  - The client ID of the VS Code public client app registration

The extension now ships with production defaults for these values, so normal users do not need to configure them manually.

Only set these VS Code settings when you want to override production for local/dev testing.

Then set the matching API validation settings in Azure Static Web Apps:

- `CloudSync__OpenIdConfigurationUrl`
  - The same OpenID metadata URL used by the VS Code setting
- `CloudSync__BearerAudience`
  - The same client ID used by the VS Code setting

Notes:

- VS Code sign-in currently uses device authorization flow because the extension host performs the network calls directly.
- The browser-hosted WASM app still uses the SWA login flow at `/.auth/login/entraExternalId`.
