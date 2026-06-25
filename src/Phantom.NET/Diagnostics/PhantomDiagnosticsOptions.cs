namespace Phantom.NET.Diagnostics;

public class PhantomDiagnosticsOptions
{
    public const string SectionName = "Phantom:Diagnostics";

    public bool Enabled { get; set; } = true;

    public string EndpointPrefix { get; set; } = "/phantom/diagnostics";

    public bool ExposeConfiguration { get; set; } = true;

    public bool ExposeOutbox { get; set; } = true;

    public bool ExposeChannels { get; set; } = true;

    public bool ExposeHandlers { get; set; } = true;

    public bool ExposeIdempotency { get; set; } = true;

    public bool RequireAuthorization { get; set; } = true;
}
