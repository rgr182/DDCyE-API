namespace DDEyC_API.Shared.Configuration;

using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

public static class AuthenticationConfig
{
public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
{
    var key = Encoding.ASCII.GetBytes(configuration.GetValue<string>("Jwt:Key"));
    var cookieDomain = configuration["Authentication:CookieDomain"];

    if (string.IsNullOrEmpty(cookieDomain))
    {
        throw new ArgumentException("Cookie domain must be configured in Authentication:CookieDomain");
    }

    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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
        
        // Add event handler to extract token from cookie if present
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var isProd = context.HttpContext.RequestServices
                    .GetRequiredService<IHostEnvironment>().IsProduction();
                var preferCookies = context.Request.Cookies["prefer-cookies"] != null || isProd;
                
                if (preferCookies)
                {
                    var token = context.Request.Cookies["DDEyC.Auth"];
                    if (!string.IsNullOrEmpty(token))
                    {
                        context.Token = token;
                    }
                }
                return Task.CompletedTask;
            }
        };
    });
}
}