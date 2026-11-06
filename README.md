# AgriStream IoT Platform

This repository contains the source code for the AgriStream IoT system, split into two projects:

*   **`AgriStreamWeb`**: The ASP.NET Core 8 MVC web dashboard. This handles user login, displaying live telemetry, uploading drone images, and sending irrigation commands.
*   **`AgriStreamFunctions`**: The Azure Functions project (.NET 8 isolated). This contains the four serverless background processes.

## System Architecture

The system is event-driven. The web app and the serverless functions communicate using Azure Storage and Azure SQL:

*   **Image Upload Flow:** The user uploads an image on the Web App. It is saved to the `drone-images` Blob container. This triggers the `BlobTriggerFunction`, which logs the event to the `TelemetryLogs` Table.
*   **Live Telemetry:** The Web App calls the `HttpGetTelemetry` function to fetch the latest sensor data from the `TelemetryLogs` Table.
*   **Irrigation Flow:** The user clicks "Send Command" on the Web App. It calls the `HttpPostIrrigation` function, which places a message in the `irrigation-commands` Queue. This triggers the `QueueTriggerFunction`, which updates the user's status in the SQL Database.

## 1. Azure Resources Required

Before running the code, ensure you have created the following in the Azure Portal:
*   Azure App Service (Windows, .NET 8)
*   Azure SQL Database
*   Azure Storage Account (The blob container, queue, and table will be created automatically on the first run).
*   Azure Function App (.NET 8 isolated, Functions v4)

## 2. Configuration

You must configure the connection strings before running or deploying.

**For `AgriStreamWeb/appsettings.json`:**
Replace all `<...>` placeholders with your actual values:
*   `ConnectionStrings:AgriStreamSql`: Your Azure SQL connection string.
*   `ConnectionStrings:AzureStorage`: Your Storage Account connection string.
*   `FunctionApp:BaseUrl`: The URL of your Function App (e.g., `https://myfuncapp.azurewebsites.net/api`).
*   `FunctionApp:FunctionKey`: The host key for your Function App.
*   `Seed:AdminPassword`: The password for the initial `admin` account. (This is created automatically on the first run if the Users table is empty).

**For `AgriStreamFunctions/local.settings.json`:**
*   Set `AgriStreamStorage` to your Storage connection string.
*   Set `SqlConnectionString` to your SQL connection string.

*Note: Ensure you add your local IP address to the Azure SQL Server firewall settings, otherwise your database connection will time out.*

## 3. Running Locally

To run the system on your local machine, open two separate terminals:

**Terminal 1 (Backend Functions):**
```powershell
cd AgriStreamFunctions
func start# AgriStream IoT Platform

Two projects:

| Folder | What it is |
|---|---|
| `AgriStreamWeb` | ASP.NET Core 8 MVC dashboard (login, live telemetry, drone image upload, irrigation control) |
| `AgriStreamFunctions` | Azure Functions (.NET 8 isolated): 4 functions |

## How the pieces connect

```
 Web: Upload image ──► Blob "drone-images" ──► BlobTriggerFunction ──► Table "TelemetryLogs"
 Web: Live feed    ◄── HttpGetTelemetry    ◄──────────────────────────┘
 Web: Irrigate     ──► HttpPostIrrigation ──► Queue "irrigation-commands" ──► QueueTriggerFunction ──► SQL Users.Status
 Web: Status chip  ◄── SQL Users.Status
```

## 1. Azure resources you need

- Azure SQL Database
- Storage account (blob container `drone-images`, queue `irrigation-commands`, table `TelemetryLogs` — all created automatically)
- Function App (.NET 8 isolated, Functions v4)

## 2. Configure

**AgriStreamWeb/appsettings.json** — replace every `<...>` placeholder:
- `ConnectionStrings:AgriStreamSql` — Azure SQL connection string
- `ConnectionStrings:AzureStorage` — storage account connection string
- `FunctionApp:BaseUrl` — e.g. `https://myfuncapp.azurewebsites.net/api` (or `http://localhost:7071/api` when running functions locally)
- `FunctionApp:FunctionKey` — function/host key (leave the placeholder when running locally; keys aren't checked there)
- `Seed:AdminPassword` — password for the first `admin` account, created on first run when the Users table is empty

> Add your SQL client IP in the Azure SQL server **Networking** firewall settings, or the login will fail.

**AgriStreamFunctions/local.settings.json** — set `AgriStreamStorage` and `SqlConnectionString`.
`AzureWebJobsStorage` uses Azurite locally (`UseDevelopmentStorage=true`); you can also point it at the real storage account.
When deploying, add the same settings under Function App → **Environment variables**.

## 3. Run locally

```powershell
# Terminal 1 – functions (needs Azure Functions Core Tools; start Azurite first, or set AzureWebJobsStorage to a real account)
cd AgriStreamFunctions
func start

# Terminal 2 – web app
cd AgriStreamWeb
dotnet run
```

Open https://localhost:7180 and sign in as `admin` with your seed password.

## 4. Deploy

```powershell
cd AgriStreamFunctions;  func azure functionapp publish <your-function-app>
cd AgriStreamWeb;        dotnet publish -c Release   # then deploy to an App Service (VS / VS Code / az webapp deploy)
```

## Notes

- Passwords are stored as PBKDF2 hashes (ASP.NET Core `PasswordHasher`), never in plain text.
- Bootstrap 5.3, Bootstrap Icons, Chart.js and Google Fonts are loaded from CDNs, so the browser needs internet access.
- Soil moisture / temperature / humidity are optional fields on the upload form. They travel as blob metadata and the blob trigger records them in TelemetryLogs, which is what drives the chart and KPI tiles.
- Don't commit `appsettings.json` or `local.settings.json` with real secrets.
