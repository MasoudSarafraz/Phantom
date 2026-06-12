namespace Phantom.AspNetCore.ProblemDetails;

public class PhantomProblemDetail
{
    public int Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? Instance { get; set; }
    public string? TraceId { get; set; }
    public List<string>? Errors { get; set; }
}
