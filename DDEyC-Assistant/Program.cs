using DDEyC_Assistant.Data;
using DDEyC_Assistant.Services;
using DDEyC_Assistant.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenAI;
using Swashbuckle.AspNetCore.Filters;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
//try to read MY_OPEN_AI_KEY environment variable
string? openAiKey= Environment.GetEnvironmentVariable("MY_OPEN_AI_API_KEY");
if (string.IsNullOrEmpty(openAiKey))
{
    throw new Exception("Open AI key is not set");
}
builder.Services.AddSingleton(new OpenAIClient(openAiKey));

// builder.Services.AddOpenAIService(settings => { settings.ApiKey = Environment.GetEnvironmentVariable("MY_OPEN_AI_API_KEY")??"";
// settings.UseBeta = true; });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,

    });
    options.OperationFilter<SecurityRequirementsOperationFilter>();
});

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

// In Program.cs
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IAssistantService, AssistantService>();
builder.Services.AddScoped<IFormDataService, FormDataService>();

builder.Services.AddLogging();
builder.Services.AddHttpClient();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
