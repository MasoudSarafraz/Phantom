using Microsoft.AspNetCore.Http;
using Phantom.Core.Services;

namespace ECommerce.Infrastructure;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public string? GetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name;
}
