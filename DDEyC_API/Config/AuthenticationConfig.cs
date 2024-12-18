namespace DDEyC_API.Shared.Configuration;

    

using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

public static class AuthenticationConfig
{
    public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var key = Encoding.ASCII.GetBytes(configuration.GetValue<string>("Jwt:Key"));
        var cookieDomain = configuration["Authentication:CookieDomain"];

        // Validate cookie domain
        if (string.IsNullOrEmpty(cookieDomain))
        {
            throw new ArgumentException("Cookie domain must be configured in Authentication:CookieDomain");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Mixed";
            options.DefaultChallengeScheme = "Mixed";
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = "DDEyC.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.Domain = cookieDomain;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(configuration.GetValue<int>("Jwt:ExpirationMinutes"));
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
        })
        .AddPolicyScheme("Mixed", "Mixed Schema", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var isProd = context.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction();
                if (isProd || context.Request.Cookies["prefer-cookies"] != null)
                {
                    return CookieAuthenticationDefaults.AuthenticationScheme;
                }

                string authorization = context.Request.Headers["Authorization"];
                if (authorization?.StartsWith("Bearer ") == true)
                {
                    return JwtBearerDefaults.AuthenticationScheme;
                }
                return CookieAuthenticationDefaults.AuthenticationScheme;
            };
        });
    }
}