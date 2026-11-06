using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Shared TelemetryLogs table client (created on first use).
        services.AddSingleton(_ =>
        {
            var config = context.Configuration;
            var table = new TableClient(
                config["AgriStreamStorage"],
                config["TelemetryTableName"] ?? "TelemetryLogs");
            table.CreateIfNotExists();
            return table;
        });
    })
    .Build();

host.Run();
