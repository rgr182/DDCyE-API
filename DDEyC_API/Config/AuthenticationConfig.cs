using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DDEyC_API.Shared.Configuration
{
    public static class AuthenticationConfig
    {
        public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var key = Encoding.ASCII.GetBytes(configuration.GetValue<string>("Jwt:Key"));

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = "Mixed";
                options.DefaultChallengeScheme = "Mixed";
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = "DDEyC.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.Domain = configuration["Authentication:CookieDomain"];
                options.ExpireTimeSpan = TimeSpan.FromMinutes(configuration.GetValue<int>("Jwt:ExpirationMinutes"));
                options.SlidingExpiration = true;
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
                    // Check if running in production
                    var isProd = context.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction();
                    if (isProd)
                    {
                        return CookieAuthenticationDefaults.AuthenticationScheme;
                    }

                    // In non-prod, check for both cookie and bearer
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
}