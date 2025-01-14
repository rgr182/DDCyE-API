using System.Text;
using DDEyC_API.Middlewares;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

namespace DDEyC_API.Shared.Configuration;

public static class AuthenticationConfig
{
    public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var key = Encoding.ASCII.GetBytes(configuration.GetValue<string>("Jwt:Key") ??
            throw new ArgumentException("JWT key must be configured in Jwt:Key"));

        var cookieDomain = configuration["Authentication:CookieDomain"] ??
            throw new ArgumentException("Cookie domain must be configured in Authentication:CookieDomain");

        // Configure JWT Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Configure cookie extraction for JWT validation
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
                },

                OnAuthenticationFailed = context =>
                {
                    if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                    {
                        context.Response.Headers.Add("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                },

                OnChallenge = context =>
                {
                    // Handle authentication challenges
                    if (context.AuthenticateFailure != null)
                    {
                        // Log authentication failure if it becomes necessary
                    }
                    return Task.CompletedTask;
                }
            };
        });

        // Configure auth cookie options
        services.Configure<CookieAuthenticationOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Cookie = new CookieBuilder
            {
                Name = "DDEyC.Auth",
                HttpOnly = true,
                SecurePolicy = CookieSecurePolicy.Always,
                SameSite = SameSiteMode.None,
                Domain = cookieDomain,
                IsEssential = true
            };

            options.ExpireTimeSpan = TimeSpan.FromHours(24);
            options.SlidingExpiration = true;
            options.Cookie.Path = "/";
        });

        // Configure CORS for authentication endpoints
        services.AddCors(options =>
        {
            options.AddPolicy("AuthCorsPolicy", builder =>
            {
                builder
                    .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
                    .AllowCredentials()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .SetIsOriginAllowed(origin =>
                    {
                        // Add additional origin validation if needed
                        return true; // Or implement your origin validation logic
                    });
            });
        });

        if (services.All(d => d.ServiceType != typeof(IDistributedCache)))
        {
            services.AddDistributedMemoryCache();
        }
        // Configure session for additional state management if needed
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.None;
        });

        // Add custom header middleware for Partitioned cookies
        services.AddTransient<IMiddleware, PartitionedCookieMiddleware>();
    }
}


