using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Cron_BolsaDeTrabajo.Services;
using Cron_BolsaDeTrabajo.Infrastructure;

namespace Cron_DDEyC.Infraestructure
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup()
        {
            Configuration = LoadConfiguration();
        }

        public static ServiceProvider ConfigureServices()
        {
            // Setup Dependency Injection and build the service provider
            var services = new ServiceCollection();

            var startup = new Startup();
            services.AddSingleton(startup.Configuration)
                    .AddSingleton<IMongoDbConnection>(sp =>
                    {
                        var mongoConnectionString = startup.Configuration["MongoDB:ConnectionString"];
                        var mongoDatabaseName = startup.Configuration["MongoDB:DatabaseName"];
                        return new MongoDbConnection(mongoConnectionString, mongoDatabaseName);
                    })
                    .AddSingleton<IApiService, ApiService>()
                    .AddSingleton<ICronService, CronService>()
                    .AddSingleton<ILinkedInJobService, LinkedInJobService>();

            return services.BuildServiceProvider();
        }

        private IConfiguration LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            return builder.Build();
        }
    }
}
