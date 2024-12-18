using DDEyC_Assistant.Policies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authentication.Cookies;
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
            var isProd = httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction();

            string? token = null;
            logger.LogInformation("Is production: {IsProd}", isProd);
            
            // Get auth cookie if it exists
            var authCookie = httpContext.Request.Cookies["DDEyC.Auth"];
            
            if (!string.IsNullOrEmpty(authCookie))
            {
                token = authCookie;
                logger.LogInformation("Using token from auth cookie");
            }
            else 
            {
                // Fall back to bearer token
                token = httpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                logger.LogInformation("Using bearer token from Authorization header");
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
                    
                    // Set auth based on token type
                    if (!string.IsNullOrEmpty(authCookie))
                    {
                        var cookieContainer = new System.Net.CookieContainer();
                        cookieContainer.Add(new Uri(ddEycApiUrl), new System.Net.Cookie("DDEyC.Auth", token));
                        
                        var handler = new HttpClientHandler
                        {
                            CookieContainer = cookieContainer,
                            UseCookies = true
                        };
                        
                        httpClient = new HttpClient(handler);
                    }
                    else
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }

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