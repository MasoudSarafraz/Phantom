using Microsoft.AspNetCore.Builder;
using Phantom.AspNetCore.Middleware;

namespace Phantom.AspNetCore.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UsePhantom(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }
}
