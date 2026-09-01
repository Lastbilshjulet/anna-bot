using System;
using System.IO;
using System.Threading.Tasks;
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
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Http;
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
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

if (args.Length > 0 && args[0] == "--spotify-auth")
{
    var clientId = configuration["Spotify:ClientId"]!;
    var clientSecret = configuration["Spotify:ClientSecret"]!;
    var tcs = new TaskCompletionSource();

    var server = new EmbedIOAuthServer(new Uri("http://127.0.0.1:5543/callback"), 5543);
    await server.Start();

    server.AuthorizationCodeReceived += async (_, response) =>
    {
        await server.Stop();
        var token = await new OAuthClient().RequestToken(
            new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, server.BaseUri)
        );
        Console.WriteLine($"\nRefresh token:\n{token.RefreshToken}");
        Console.WriteLine("\nAdd this to your .env as: Spotify:RefreshToken=<token>");
        tcs.SetResult();
    };

    var request = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
    {
        Scope =
        [
            Scopes.PlaylistReadPrivate,
            Scopes.PlaylistReadCollaborative,
            Scopes.PlaylistModifyPublic,
            Scopes.PlaylistModifyPrivate,
            Scopes.UserLibraryRead,
            Scopes.UserLibraryModify
        ]
    };

    BrowserUtil.Open(request.ToUri());
    Console.WriteLine("Browser opened. Waiting for Spotify authorization...");
    await tcs.Task;
    return;
}

var discordSocketConfig = new DiscordSocketConfig()
{
    EnableVoiceDaveEncryption = true,
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.GuildVoiceStates | GatewayIntents.MessageContent,
    AlwaysDownloadUsers = true,
    MessageCacheSize = 100
};

Discord.LibDave.Dave.SetLogSink(LogSink);

var spotifyConfig = SpotifyClientConfig
    .CreateDefault()
    .WithAuthenticator(new AuthorizationCodeAuthenticator(
        configuration["Spotify:ClientId"]!,
        configuration["Spotify:ClientSecret"]!,
        new AuthorizationCodeTokenResponse
        {
            RefreshToken = configuration["Spotify:RefreshToken"]!
        }
    ));

var spotify = new SpotifyClient(spotifyConfig);

var services = new ServiceCollection()
    .AddLogging(loggingBuilder => loggingBuilder.AddSerilog())
    .AddSingleton<IConfiguration>(configuration)
    .Configure<DiscordConfiguration>(configuration.GetSection("Discord").Bind)
    .Configure<MusicConfiguration>(configuration.GetSection("Music").Bind)
    .Configure<SpotifyConfiguration>(configuration.GetSection("Spotify").Bind)
    .AddDbContextFactory<SongDbContext>(options => options.UseSqlite(configuration.GetConnectionString("SongDb")))
    .AddSingleton<SongMapper>()
    .AddSingleton<MessageHelper>()
    .AddSingleton<ValidationHelper>()
    .AddSingleton<SongAutocompleteHandler>()
    .AddSingleton(discordSocketConfig)
    .AddSingleton(x => new DiscordSocketClient(x.GetRequiredService<DiscordSocketConfig>()))
    .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(), new InteractionServiceConfig { DefaultRunMode = RunMode.Async }))
    .AddTransient(typeof(ICommandLogger<>), typeof(CommandLogger<>))
    .AddSingleton<YoutubeClient>()
    .AddSingleton(spotify)
    .AddSingleton<IDiscordDmService, DiscordDmService>()
    .AddSingleton<IYoutubeService, YoutubeService>()
    .AddSingleton<ISpotifyService, SpotifyService>()
    .AddSingleton<ISongDbService, SongDbService>()
    .AddSingleton<IAudioService, AudioService>()
    .AddSingleton<PlayerState>()
    .AddSingleton<DiscordBot>()
    .BuildServiceProvider();

var bot = services.GetRequiredService<DiscordBot>();
await bot.RunAsync();
return;

void LogSink(LoggingSeverity severity, string file, int line, string message)
{
    // Log nothing
}
