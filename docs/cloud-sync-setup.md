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

Do not use `AzureWebJobsStorage` for your application blob storage in the deployed Static Web App.

Azure Static Web Apps managed Functions reserve `AzureWeb...` settings for the platform runtime.

Use `CloudSync__StorageConnectionString` for the storage account that should hold synced models.

`AzureWebJobsStorage` remains valid for local Functions development in `Api/local.settings.json`.

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
