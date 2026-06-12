namespace Phantom.Data.Extensions;

/// <summary>
/// Configuration options for the Phantom.Data persistence layer.
/// </summary>
public class PhantomDataOptions
{
    /// <summary>
    /// Gets or sets the database connection string.
    /// Required unless <see cref="ConfigureDbContext"/> is provided.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the database provider to use.
    /// Defaults to <see cref="DatabaseProvider.PostgreSQL"/>.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.PostgreSQL;

    /// <summary>
    /// Gets or sets the name of the assembly containing EF Core migrations.
    /// Required when using migrations with a separate data assembly.
    /// </summary>
    public string? MigrationsAssembly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether soft-delete interceptor is enabled.
    /// </summary>
    public bool UseSoftDelete { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the auditable interceptor is enabled.
    /// </summary>
    public bool UseAuditable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the outbox pattern is enabled.
    /// </summary>
    public bool UseOutbox { get; set; }

    /// <summary>
    /// Gets or sets a custom action to configure the <see cref="Microsoft.EntityFrameworkCore.DbContextOptionsBuilder"/>.
    /// When set, this overrides the default provider-based configuration.
    /// </summary>
    public Action<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder>? ConfigureDbContext { get; set; }

    /// <summary>
    /// Validates that the options are correctly configured.
    /// Throws <see cref="InvalidOperationException"/> if required settings are missing.
    /// </summary>
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

/// <summary>
/// Specifies the database provider to use for the Phantom.Data persistence layer.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>PostgreSQL via Npgsql.</summary>
    PostgreSQL,

    /// <summary>Microsoft SQL Server.</summary>
    SqlServer,

    /// <summary>In-memory database (for testing only).</summary>
    InMemory
}
