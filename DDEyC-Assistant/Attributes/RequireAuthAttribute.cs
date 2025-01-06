namespace DDEyC_Assistant.Attributes;

using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DDEyC_Assistant.Policies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;

public class RequireAuthAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireAuthAttribute>>();
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var isProd = httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction();

        try
        {
            string? token = null;
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

            // Validate the token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(configuration["Jwt:Key"]);

            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out var validatedToken);

                await next();
            }
            catch (SecurityTokenException)
            {
                logger.LogWarning("Invalid token for request path: {Path}", httpContext.Request.Path);
                context.Result = new UnauthorizedResult();
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Authentication failed for request path: {Path}", httpContext.Request.Path);
            context.Result = new UnauthorizedResult();
            return;
        }
    }
}