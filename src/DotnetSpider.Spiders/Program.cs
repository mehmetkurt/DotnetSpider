using System;
using System.Threading.Tasks;
using DotnetSpider.RabbitMQ;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace DotnetSpider.Spiders;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console().WriteTo.RollingFile("logs/spiders.log")
            .CreateLogger();

        

            var builder = Builder.CreateDefaultBuilder<EntitySpider>();
            builder.UseSerilog();
            builder.UseRabbitMQ();
            await builder.Build().RunAsync();
        


        Console.WriteLine("Bye!");
        Environment.Exit(0);
    }
}
