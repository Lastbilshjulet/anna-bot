using System;
using System.IO;
using anna_bot.Domain;
using anna_bot.Domain.Models.Configurations;
using anna_bot.Domain.Services;
using anna_bot.InServices;
using anna_bot.InServices.Commands.Autocompleters;
using anna_bot.InServices.Commands.Helpers;
using anna_bot.OutServices;
using anna_bot.OutServices.DbContexts;
using anna_bot.OutServices.UseCases;
using Discord;
using Discord.Interactions;
using Discord.LibDave.Binding;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using YoutubeExplode;

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
    .Configure<MusicConfiguration>(configuration.GetSection("Music").Bind)
    .AddDbContextFactory<SongDbContext>(options => options.UseSqlite(configuration.GetConnectionString("SongDb")))
    .AddSingleton<SongMapper>()
    .AddSingleton<MessageHelper>()
    .AddSingleton<SongAutocompleteHandler>()
    .AddSingleton(discordSocketConfig)
    .AddSingleton(x => new DiscordSocketClient(x.GetRequiredService<DiscordSocketConfig>()))
    .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
    .AddTransient(typeof(ICommandLogger<>), typeof(CommandLogger<>))
    .AddSingleton<YoutubeClient>()
    .AddSingleton<IYoutubeService, YoutubeService>()
    .AddSingleton<ISpotifyService, SpotifyService>()
    .AddSingleton<ISongDbService, SongDbService>()
    .AddSingleton<IAudioService, AudioService>()
    .AddSingleton<PlayerHolder>()
    .AddSingleton<DiscordBot>()
    .BuildServiceProvider();

var bot = services.GetRequiredService<DiscordBot>();
await bot.RunAsync();
return;

void LogSink(LoggingSeverity severity, string file, int line, string message)
{
    // Log nothing
}
