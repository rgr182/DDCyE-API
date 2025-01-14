using DDEyC_API.DataAccess.Context;
using DDEyC_API.DataAccess.Repositories;
using DDEyC_API.DataAccess.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc.Razor;
using DDEyC_API.Services;
using DDEyC_API.Extensions;
using DDEyC_API.Models.JSearch;
using DDEyC_API.Infrastructure.Http;
using DDEyC_API.Services.JSearch;
using DDEyC_API.Services.TextAnalysis;
using DDEyC_API.Shared.Configuration;
using DDEyC_Auth.Utils;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services to the container, including controllers with views (for Razor)
builder.Services.AddControllersWithViews();  

builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Add("/Templates/Emails/{0}" + RazorViewEngine.ViewExtension);
});

// Add CORS configuration to allow any origin, method, and header
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("AllowAll", policy =>
//     {
//         policy.AllowAnyOrigin()
//               .AllowAnyMethod()
//               .AllowAnyHeader();
//     });
// });
//TODO add null check
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        builder =>
        {
            var originsSection = configuration.GetSection("Cors:AllowedOrigins");
            var origins = originsSection.Get<string[]>();
            
            if (origins == null || origins.Length == 0)
            {
                throw new InvalidOperationException("CORS origins are not configured correctly in appsettings.json");
            }

            builder
                .WithOrigins(origins)
                .AllowCredentials()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

// Add HttpContextAccessor
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.ConfigureAuthentication(builder.Configuration);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DDEyC API", Version = "v1" });

    // JWT security scheme configuration
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    // Global security scheme configuration for JWT
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});


// Register your services


builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IAuthUtils, AuthUtils>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPasswordRecoveryRequestService, PasswordRecoveryRequestService>();
builder.Services.AddScoped<IPasswordRecoveryRequestRepository, PasswordRecoveryRequestRepository>();
// builder.Services.AddScoped<IJobListingRepository, JobListingRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddSingleton<ITextNormalizationService, TextNormalizationService>();
builder.Services.AddScoped<ICourseImportService,CourseImportService>();
builder.Services.Configure<TextAnalysisConfig>(
    builder.Configuration.GetSection("TextAnalysis"));
// Register AuthContext
builder.Services.AddDbContext<AuthContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddJSearchServices(builder.Configuration);
builder.Services.AddMemoryCache();

var app = builder.Build();

// Middleware order is crucial here

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Use static files for MVC views
app.UseStaticFiles();

// add the file Assets


// Correct order: Routing first, then CORS, then Authentication and Authorization
app.UseRouting();

// Enable CORS after routing but before authentication/authorization
app.UseCors("AllowSpecificOrigins");

// Enable Authentication and Authorization after UseRouting
app.UseAuthentication();
app.UseAuthorization();

// Configure routing for API controllers and MVC (with Razor views)
app.UseEndpoints(endpoints =>
{
    // Map API controllers
    endpoints.MapControllers();

    // Map default MVC route for Razor views
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

// Use Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Run the application
app.Run();
