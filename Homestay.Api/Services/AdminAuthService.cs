using Homestay.Api.Data;
using Homestay.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Homestay.Api.Services;

public class AdminAuthService(AppDbContext db, IPasswordHasher<AdminUser> hasher)
{
    /// <summary>Returns the admin, or null for any wrong email or password, so callers can't tell which was wrong.</summary>
    public async Task<AdminUser?> ValidateAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var admin = await db.AdminUsers.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Email == normalizedEmail, cancellationToken);

        // Hash even when the email is unknown, so response time doesn't reveal which emails exist.
        var hash = admin?.PasswordHash ?? hasher.HashPassword(new AdminUser(), password);
        var result = hasher.VerifyHashedPassword(admin ?? new AdminUser(), hash, password);

        return admin is not null && result != PasswordVerificationResult.Failed ? admin : null;
    }
}
