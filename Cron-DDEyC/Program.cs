using Microsoft.Extensions.DependencyInjection;
using Cron_DDEyC.Infraestructure;
using Cron_BolsaDeTrabajo.Services;

namespace Cron_BolsaDeTrabajo
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create a service provider using the Startup class configuration
            var serviceProvider = Startup.ConfigureServices();

            // Resolve the ICronService and start the cron job
            var cronService = serviceProvider.GetService<ICronService>();

#if TESTING
            // Execute testing method if in testing profile
            //cronService.ExtractJobOffers().Wait();

            /// Uncomment the following line to execute the URL validation method.
            /// This will invoke the `CheckJobUrlsAsync` method to verify the accessibility of each job URL in the database,
            /// logging any URL that returns an error status or fails to respond.

             cronService.CheckJobUrlsAsync().Wait();
#else
            // Start the Cron Service
            cronService.StartAsync().Wait();
#endif

            // Keep the application running
            Console.WriteLine("Press [Enter] to exit the program.");
            Console.ReadLine();
        }
    }
}
