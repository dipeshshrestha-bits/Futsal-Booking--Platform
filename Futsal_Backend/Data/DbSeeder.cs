using Backend.Common;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Data;

public static class DbSeeder
{
    /// <summary>Applies migrations (if enabled) and creates the first Admin account.</summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<ApplicationDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var seed = provider.GetRequiredService<IOptions<SeedAdminOptions>>().Value;

        if (databaseOptions.ApplyMigrationsOnStartup)
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied.");
        }

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(seed.Username) ||
            string.IsNullOrWhiteSpace(seed.Email) ||
            string.IsNullOrWhiteSpace(seed.Password))
        {
            logger.LogWarning("No Admin account exists and the SeedAdmin settings are empty. Fill them in to create one.");
            return;
        }

        if (!InputRules.IsStrongPassword(seed.Password))
        {
            logger.LogWarning("SeedAdmin password must be at least 8 characters with a letter and a number. Admin not created.");
            return;
        }

        db.Users.Add(new User
        {
            FullName = string.IsNullOrWhiteSpace(seed.FullName) ? "Platform Admin" : seed.FullName.Trim(),
            Username = seed.Username.Trim().ToLowerInvariant(),
            Email = seed.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(seed.Password, workFactor: 12),
            Role = UserRole.Admin,
            IsActive = true,
            MustChangePassword = false,
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded Admin account '{Username}'.", seed.Username);
    }
}