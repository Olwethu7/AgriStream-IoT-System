# AgriStream IoT Platform

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
