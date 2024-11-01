using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DDEyC_Assistant.Attributes
{
    public class RequireAuthAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var token = httpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (string.IsNullOrEmpty(token))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var httpClientFactory = httpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
            var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
            var httpClient = httpClientFactory.CreateClient();
            var ddEycApiUrl = configuration["AppSettings:BackendUrl"];

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{ddEycApiUrl}/api/auth/validateSession");
                request.Headers.Add("Authorization", $"Bearer {token}");
                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }
            }
            catch (HttpRequestException)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                return;
            }

            await next();
        }
    }
}