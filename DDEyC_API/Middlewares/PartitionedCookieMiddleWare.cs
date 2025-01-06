namespace DDEyC_API.Middlewares;

/// <summary>
/// Middleware to handle Partitioned cookie attributes
/// </summary>
public class PartitionedCookieMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(() =>
        {
            var setCookieHeaders = context.Response.Headers["Set-Cookie"];
            if (setCookieHeaders.Count > 0)
            {
                var newHeaders = new List<string>();
                foreach (var header in setCookieHeaders)
                {
                    if (header.Contains("DDEyC.Auth") && !header.Contains("Partitioned"))
                    {
                        // Add Partitioned attribute to existing auth cookies
                        newHeaders.Add($"{header}; Partitioned");
                    }
                    else
                    {
                        newHeaders.Add(header);
                    }
                }
                context.Response.Headers["Set-Cookie"] = newHeaders.ToArray();
            }
            return Task.CompletedTask;
        });

        await next(context);
    }
}
