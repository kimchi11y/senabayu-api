using Homestay.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Homestay.Tests;

public class MigrationTests
{
    /// <summary>Fails if someone changed the model and forgot to add a migration. Needs no database.</summary>
    [Fact]
    public void Model_has_no_pending_changes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost")
            .Options;
        using var db = new AppDbContext(options);

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
