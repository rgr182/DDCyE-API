using DDEyC_API.Shared.Configuration;
using DDEyC_Assistant.Data;
using DDEyC_Assistant.Extensions;
using DDEyC_Assistant.Options;
using DDEyC_Assistant.Repositories;
using DDEyC_Assistant.Services;
using DDEyC_Assistant.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// OpenAI configuration
string? openAiKey = Environment.GetEnvironmentVariable("MY_OPEN_AI_API_KEY");
if (string.IsNullOrEmpty(openAiKey))
{
    throw new Exception("Open AI key is not set");
}
builder.Services.AddSingleton(new OpenAIClient(openAiKey));

// Swagger configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "DDEyC Assistant API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});

// Database configuration
builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

// Service registrations
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IAssistantService, AssistantService>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IConversationLockManager, ConversationLockManager>();
builder.Services.AddHostedService<MessageCleanupService>();
// Logging and HttpClient
builder.Services.AddLogging();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
//Auth
// var authOptions = builder.Configuration.GetSection("AuthenticationPolicy")
//     .Get<AuthenticationPolicyOptions>() ?? new AuthenticationPolicyOptions();

// builder.Services.AddAuthenticationPolicy(authOptions);
builder.Services.ConfigureAuthentication(builder.Configuration);
// Add configuration for DDEyC API URL
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DDEyC Assistant API V1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins");

app.MapControllers();

app.Run();