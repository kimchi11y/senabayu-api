using Homestay.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Homestay.Tests;

/// <summary>The whole API, running in memory against a real Postgres, with one admin login.</summary>
public class ApiFixture : PostgresFixture
{
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "correct-horse-battery-staple";
    public const string SigningKey = "test-signing-key-that-is-long-enough-for-hmac";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        await using var db = CreateContext();
        var admin = new AdminUser { Email = AdminEmail };
        admin.PasswordHash = new PasswordHasher<AdminUser>().HashPassword(admin, AdminPassword);
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
    }

    public WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(host => host
            .UseSetting("ConnectionStrings:Default", ConnectionString)
            .UseSetting("Jwt:SigningKey", SigningKey));
}
