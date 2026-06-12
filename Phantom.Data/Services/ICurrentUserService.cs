namespace Phantom.Data.Services;

/// <summary>
/// Provides the current user identifier for audit and soft-delete tracking.
/// Implement this service to integrate with your application's authentication system.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the identifier of the currently authenticated user, or <c>null</c> if no user is authenticated.
    /// </summary>
    /// <returns>The current user identifier, or <c>null</c>.</returns>
    string? GetCurrentUserId();
}
