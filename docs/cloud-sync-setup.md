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
- `.azurite/` is local emulator state only and is intentionally ignored by Git.

## VS Code Extension Sync

The VS Code webview host cannot rely on SWA browser cookies for cloud sync.

For VS Code cloud sync, create a separate Entra External ID app registration for a public client:

1. In the external tenant, create a new app registration, for example `Dependinator VS Code Sync`.
2. Enable public client flows on that app registration.
3. Under `Expose an API`, set the Application ID URI to the default `api://<client-id>` form if it is not already set.
4. Add a scope named `access_as_user`.
5. Use the same user flow/OpenID metadata URL that the site uses.

Then configure this VS Code setting if you need to override production:

- `dependinator.cloudSync.baseUrl`
  - Example: `https://dependinator.com`

The extension keeps the production OpenID metadata URL and VS Code client ID in internal constants, so normal users do not configure them manually.

Then set the matching API validation settings in Azure Static Web Apps:

- `CloudSync__OpenIdConfigurationUrl`
  - The same OpenID metadata URL compiled into the VS Code extension
- `CloudSync__BearerAudience`
  - The same client ID compiled into the VS Code extension

Notes:

- VS Code sign-in currently uses device authorization flow because the extension host performs the network calls directly.
- The extension requests an API access token for `api://<client-id>/access_as_user`.
- The browser-hosted WASM app still uses the SWA login flow at `/.auth/login/entraExternalId`.
