using System;
using System.IO;
using anna_bot.InServices;
using anna_bot.InServices.Models;
using anna_bot.OutServices;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

var envContent = File.ReadAllLines(".env");
foreach (var line in envContent)
{
    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
    var parts = line.Split('=', 2);
    if (parts.Length == 2)
    {
        Environment.SetEnvironmentVariable(parts[0], parts[1]);
    }
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/anna-bot-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var services = new ServiceCollection()
    .AddLogging(loggingBuilder => loggingBuilder.AddSerilog())
    .AddSingleton<IConfiguration>(configuration)
    .Configure<DiscordConfiguration>(configuration.GetSection("Discord").Bind)
    .AddSingleton<DiscordSocketClient>()
    .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
    .AddSingleton<IYoutubeService, YoutubeService>()
    .AddSingleton<DiscordBot>()
    .BuildServiceProvider();

var bot = services.GetRequiredService<DiscordBot>();
await bot.RunAsync();
