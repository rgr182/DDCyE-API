using DDEyC_Assistant.Policies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace DDEyC_Assistant.Attributes
{
    public class RequireAuthAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireAuthAttribute>>();
            var authPolicy = httpContext.RequestServices.GetRequiredService<IAuthenticationPolicy>();
            var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

            string? token = null;

            // Try to get token from cookie first
            var tokenClaim = httpContext.User?.FindFirst("token");
            if (tokenClaim != null)
            {
                token = tokenClaim.Value;
            }
            else
            {
                // Fall back to bearer token
                token = httpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            }

            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("No authentication token found. Request path: {Path}", httpContext.Request.Path);
                context.Result = new UnauthorizedResult();
                return;
            }

            var httpClientFactory = httpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("AuthClient");
            var ddEycApiUrl = configuration["AppSettings:BackendUrl"];

            try
            {
                var response = await authPolicy.RetryPolicy.ExecuteAsync(async () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{ddEycApiUrl}/api/auth/validateSession");
                    request.Headers.Add("Authorization", $"Bearer {token}");

                    logger.LogDebug("Sending validation request to: {Url}", request.RequestUri);
                    return await httpClient.SendAsync(request);
                });

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "Token validation failed. Status code: {StatusCode}, Request path: {Path}",
                        response.StatusCode,
                        httpContext.Request.Path
                    );
                    context.Result = new UnauthorizedResult();
                    return;
                }

                logger.LogInformation(
                    "Token successfully validated for request path: {Path}",
                    httpContext.Request.Path
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unrecoverable error during token validation. Request path: {Path}",
                    httpContext.Request.Path
                );
                context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                return;
            }

            await next();
        }
    }
}