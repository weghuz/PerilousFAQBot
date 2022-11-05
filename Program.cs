using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using FAQBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;

namespace FAQBot
{
    class Program
    {

        public static Task Main(string[] args)
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services => {
                    services.AddSingleton<IConfiguration>(config);
                    services.AddDbContext<FAQDB>();
                    services.AddTransient<FAQBot>(); 
                })
                .Build();

            var bot = host.Services.GetRequiredService<FAQBot>();
            return bot.MainAsync(args);
        }

    }
}