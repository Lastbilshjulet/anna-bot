using System;
using System.IO;
using anna_bot.InServices;
using anna_bot.InServices.Commands.Helpers;
using anna_bot.InServices.Models;
using anna_bot.OutServices;
using Discord;
using Discord.Interactions;
using Discord.LibDave.Binding;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

var envContent = File.ReadAllLines(".env");
foreach (var line in envContent)
{
    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
    var keyValuePair = line.Split('=', 2);
    if (keyValuePair.Length == 2)
    {
        Environment.SetEnvironmentVariable(keyValuePair[0], keyValuePair[1]);
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

var discordSocketConfig = new DiscordSocketConfig()
{
    EnableVoiceDaveEncryption = true,
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.GuildVoiceStates | GatewayIntents.MessageContent,
    AlwaysDownloadUsers = true,
    MessageCacheSize = 100
};

Discord.LibDave.Dave.SetLogSink(LogSink);

var services = new ServiceCollection()
    .AddLogging(loggingBuilder => loggingBuilder.AddSerilog())
    .AddSingleton<IConfiguration>(configuration)
    .Configure<DiscordConfiguration>(configuration.GetSection("Discord").Bind)
    .AddSingleton(discordSocketConfig)
    .AddSingleton(x => new DiscordSocketClient(x.GetRequiredService<DiscordSocketConfig>()))
    .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
    .AddTransient(typeof(ICommandLogger<>), typeof(CommandLogger<>))
    .AddSingleton<IYoutubeService, YoutubeService>()
    .AddSingleton<DiscordBot>()
    .BuildServiceProvider();

var bot = services.GetRequiredService<DiscordBot>();
await bot.RunAsync();
return;

void LogSink(LoggingSeverity severity, string file, int line, string message)
{
    // Log nothing
}
