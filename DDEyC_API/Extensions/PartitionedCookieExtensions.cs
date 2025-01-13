using DDEyC_API.Middlewares;
using DDEyC_API.Shared.Configuration;

public static class PartitionedCookieExtensions
{
    public static IApplicationBuilder UsePartitionedCookies(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PartitionedCookieMiddleware>();
    }
}