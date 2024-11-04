using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace DDEyC_Assistant.Attributes
{
    public class RequireAuthAttribute : Attribute, IAsyncActionFilter
    {
        

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            // Resolve logger from DI
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireAuthAttribute>>();
            
            var token = httpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            
            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("Authorization header is missing or empty. Request path: {Path}", httpContext.Request.Path);
                context.Result = new UnauthorizedResult();
                return;
            }
            logger.LogInformation("Validating token for request path: {Path}", httpContext.Request.Path);

            var httpClientFactory = httpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
            var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
            var httpClient = httpClientFactory.CreateClient();
            var ddEycApiUrl = configuration["AppSettings:BackendUrl"];

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{ddEycApiUrl}/api/auth/validateSession");
                request.Headers.Add("Authorization", $"Bearer {token}");
                
                logger.LogDebug("Sending validation request to: {Url}", request.RequestUri);
                
                var response = await httpClient.SendAsync(request);

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
            catch (HttpRequestException ex)
            {
                logger.LogError(
                    ex,
                    "Failed to validate token due to HTTP request error. Request path: {Path}",
                    httpContext.Request.Path
                );
                context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error during token validation. Request path: {Path}",
                    httpContext.Request.Path
                );
                throw;
            }

            await next();
        }
    }
}