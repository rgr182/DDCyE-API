namespace DDEyC_Assistant.Attributes;

using DDEyC_Assistant.Policies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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

        try
        {
            // Get JWT either from cookie or Authorization header
            var preferCookies = httpContext.Request.Cookies["prefer-cookies"] != null || isProd;
            
            if (preferCookies)
            {
                token = httpContext.Request.Cookies["DDEyC.Auth"];
                logger.LogInformation("Using JWT from cookie");
            }
            else 
            {
                token = httpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                logger.LogInformation("Using bearer token from Authorization header");
            }

            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("No authentication token found. Request path: {Path}", httpContext.Request.Path);
                context.Result = new UnauthorizedResult();
                return;
            }

            // Store the JWT token in HttpContext.Items for use in controllers
            httpContext.Items["JwtToken"] = token;

            var httpClientFactory = httpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("AuthClient");
            var ddEycApiUrl = configuration["AppSettings:BackendUrl"];

            var response = await authPolicy.RetryPolicy.ExecuteAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{ddEycApiUrl}/api/auth/validateSession");
                
                if (preferCookies)
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

                return await httpClient.SendAsync(request);
            });

            if (!response.IsSuccessStatusCode)
            {
                throw new UnauthorizedAccessException("Token validation failed");
            }

            await next();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Authentication failed for request path: {Path}", httpContext.Request.Path);
            context.Result = new UnauthorizedResult();
            return;
        }
    }
}