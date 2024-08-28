using Microsoft.Extensions.Configuration;
using Cron_BolsaDeTrabajo.Services;
using Cron_BolsaDeTrabajo.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Cron_BolsaDeTrabajo
{
    class Program
    {
        static void Main(string[] args)
        {
            // Load configuration from appsettings.json
            var configuration = LoadConfiguration();

            // Setup Dependency Injection
            var serviceProvider = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<IMongoDbConnection>(sp =>
                {
                    var mongoConnectionString = configuration["MongoDB:ConnectionString"];
                    var mongoDatabaseName = configuration["MongoDB:DatabaseName"];
                    return new MongoDbConnection(mongoConnectionString, mongoDatabaseName);
                })
                .AddSingleton<IApiService, ApiService>()
                .AddSingleton<ICronService, CronService>()
                .BuildServiceProvider();

            // Start the Cron Service
            var cronService = serviceProvider.GetService<ICronService>();
            cronService.StartAsync().Wait();

            // Keep the application running
            Console.WriteLine("Press [Enter] to exit the program.");
            Console.ReadLine();
        }

        private static IConfiguration LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            return builder.Build();
        }
    }
}
