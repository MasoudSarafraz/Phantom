namespace Phantom.Core.Services;

/// <summary>
/// Provides the current user's identity for auditing and soft-delete purposes.
/// Implement this in your application layer (e.g., extracting from HttpContext).
/// </summary>
public interface ICurrentUserService
{
    string? GetCurrentUserId();
}
