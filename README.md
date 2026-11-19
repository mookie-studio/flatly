# Flatly

Flatly is a complete platform for people who share a flat and their day-to-day life: flats and invitations, chores, landlord communication, and a shared home pack.

## Layout

- `api/`: .NET 10 Minimal API with EF Core and PostgreSQL, orchestrated with .NET Aspire.
- `web/`: React, TypeScript and Vite responsive web app.
- `docs/adr/`: Architecture Decision Records.

## Prerequisites

- .NET 10 SDK
- Docker (for local PostgreSQL through Aspire)
- Node.js 22 or newer and npm

## Sign-in providers setup

Flatly signs users in with ASP.NET Core Identity and two external providers in MVP1: Google and Microsoft. Apple is planned for MVP2. There is no email/password login.

Follow these steps once per environment. Never commit client secrets to the repository.

### 1. Google (Google Cloud Console)

1. Open the [Google Cloud Console](https://console.cloud.google.com/) and select or create a project.
2. Go to **APIs & Services → OAuth consent screen**. Set the app name and support email, and request the `openid`, `email` and `profile` scopes. While the app is in *Testing*, add the test users' Google accounts.
3. Go to **APIs & Services → Credentials → Create credentials → OAuth client ID**.
4. Choose **Web application**, then add the redirect URIs from the table below.
5. Copy the **Client ID** and **Client secret**.

### 2. Microsoft (Microsoft Entra admin center)

1. Open the [Microsoft Entra admin center](https://entra.microsoft.com/) and go to **Identity → Applications → App registrations → New registration**.
2. Under **Supported account types**, choose **Accounts in any organizational directory and personal Microsoft accounts**. Personal accounts (Outlook.com, Hotmail, Live) must be allowed, because most flatmates use one.
3. Under **Redirect URI**, choose the **Web** platform and add the URIs from the table below.
4. Copy the **Application (client) ID** from the overview page.
5. Go to **Certificates & secrets → New client secret**. Copy the secret **value** right away: Microsoft shows it only once.

### Redirect URIs

ASP.NET Core's default callback paths are `/signin-google` and `/signin-microsoft`. Register one URI per environment in each provider.

| Environment | Google | Microsoft |
| --- | --- | --- |
| Local | `https://localhost:<port>/signin-google` | `https://localhost:<port>/signin-microsoft` |
| Production | `https://<api-host>/signin-google` | `https://<api-host>/signin-microsoft` |

Replace `<port>` with the HTTPS port the API uses locally, and `<api-host>` with the production API host.

### Configuration keys

Both providers read the same configuration keys:

| Key | Secret? |
| --- | --- |
| `Authentication:Google:ClientId` | No |
| `Authentication:Google:ClientSecret` | **Yes** |
| `Authentication:Microsoft:ClientId` | No |
| `Authentication:Microsoft:ClientSecret` | **Yes** |

### Where the values live

- **Local development:** store the values with .NET user secrets, which keeps them outside the repository. Run these commands from the repository root, using the API project path once it exists:

  ```bash
  dotnet user-secrets set "Authentication:Google:ClientId" "<value>" --project api/<ApiProject>
  dotnet user-secrets set "Authentication:Google:ClientSecret" "<value>" --project api/<ApiProject>
  dotnet user-secrets set "Authentication:Microsoft:ClientId" "<value>" --project api/<ApiProject>
  dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "<value>" --project api/<ApiProject>
  ```

- **Deployment:** set the same keys as application settings on the Azure App Service that hosts the API, using the double-underscore form (`Authentication__Google__ClientSecret`, and so on). Mark the client secrets as secret settings.

### Setup is complete when

- Signing in with a Google account works locally.
- Signing in with a personal Microsoft account works locally.
- The same two flows work in production with the production redirect URIs.

## Running

The API and web app are scaffolded in later tasks. Once they exist, this section will explain how to run both locally.

## License

To be decided.
