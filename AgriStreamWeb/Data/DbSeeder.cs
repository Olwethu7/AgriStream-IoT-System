using AgriStreamWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriStreamWeb.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.Database.EnsureCreatedAsync();

            if (await db.Users.AnyAsync()) return;

            var username = config["Seed:AdminUsername"];
            var password = config["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("No users exist and Seed is not set.");
                return;
            }

            var admin = new User { Username = username, Password = password, FullName = "Farm Administrator", Role = "Admin", Status = "Idle" };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded admin user '{Username}'.", username);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialisation failed.");
        }
    }
}