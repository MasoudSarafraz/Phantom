namespace Phantom.AspNetCore.ProblemDetails;

public class PhantomProblemDetail
{
    public string? Type { get; set; }

    public int Status { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string? Instance { get; set; }

    public string? TraceId { get; set; }

    public Dictionary<string, string[]>? Errors { get; set; }
}
