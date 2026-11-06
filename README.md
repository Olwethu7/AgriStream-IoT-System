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

Before deploying the code, ensure you have created the following in the Azure Portal:
*   Azure App Service (Windows, .NET 8)
*   Azure SQL Database
*   Azure Storage Account (The blob container, queue, and table will be created automatically on the first run).
*   Azure Function App (.NET 8 isolated, Functions v4)

## 2. Configuration

You must configure the connection strings before deploying.

**For `AgriStreamWeb/appsettings.json`:**
Replace all `<...>` placeholders with your actual Azure values:
*   `ConnectionStrings:AgriStreamSql`: Your Azure SQL connection string.
*   `ConnectionStrings:AzureStorage`: Your Storage Account connection string.
*   `FunctionApp:BaseUrl`: The URL of your deployed Function App (e.g., `https://myfuncapp.azurewebsites.net/api`).
*   `FunctionApp:FunctionKey`: The host key for your Function App.
*   `Seed:AdminPassword`: The password for the initial `admin` account. (This is created automatically in Azure SQL on the first run).

**For `AgriStreamFunctions`:**
When deploying to Azure, add these settings under the Function App's **Environment variables**:
*   `AgriStreamStorage`: Your Storage Account connection string.
*   `SqlConnectionString`: Your Azure SQL connection string.

*Note: Ensure you add your local IP address to the Azure SQL Server firewall settings so you can manage the database.*

## 3. Deployment

To deploy the code to Azure, use the following commands:

**Deploy Functions:**
```powershell
cd AgriStreamFunctions
func azure functionapp publish <your-function-app-name>
