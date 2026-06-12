namespace Phantom.Data.Extensions;

public class PhantomDataOptions
{
    public string? ConnectionString { get; set; }
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.PostgreSQL;
    public bool UseSoftDelete { get; set; }
    public bool UseAuditable { get; set; }
    public bool UseOutbox { get; set; }
    public Action<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder>? ConfigureDbContext { get; set; }
}

public enum DatabaseProvider { PostgreSQL, SqlServer, InMemory }
