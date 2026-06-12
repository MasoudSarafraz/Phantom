namespace Phantom.Data.Extensions;

public class PhantomDataOptions
{
    public string? ConnectionString { get; set; }

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.PostgreSQL;

    public string? MigrationsAssembly { get; set; }

    public bool UseSoftDelete { get; set; }

    public bool UseAuditable { get; set; }

    public bool UseOutbox { get; set; }

    public Action<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder>? ConfigureDbContext { get; set; }

    public void Validate()
    {
        if (ConfigureDbContext == null && string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                "Either ConnectionString or ConfigureDbContext must be provided. " +
                "Set ConnectionString for automatic provider configuration, or use " +
                "ConfigureDbContext for custom DbContext configuration.");
        }
    }
}

public enum DatabaseProvider
{
    PostgreSQL,

    SqlServer,

    InMemory
}
