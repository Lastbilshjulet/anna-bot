using System;
using System.Threading.Tasks;
using anna_bot.InServices.Models;
using anna_bot.OutServices;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace anna_bot.InServices;

public class DiscordBot(
    IServiceProvider serviceProvider,
    DiscordSocketClient client,
    InteractionService interactionService,
    IYoutubeService youtubeService,
    IOptions<DiscordConfiguration> discordConfig,
    ILogger<DiscordBot> logger)
{
    public async Task RunAsync()
    {
        logger.LogInformation("Starting anna-bot...");
        
        client.InteractionCreated += HandleInteraction;
        client.Log += Log;

        await client.LoginAsync(TokenType.Bot, discordConfig.Value.Token);
        await client.StartAsync();

        await Task.Delay(-1);
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        var ctx = new SocketInteractionContext(client, interaction);
        await interactionService.ExecuteCommandAsync(ctx, serviceProvider);
    }

    private Task Log(LogMessage msg)
    {
#pragma warning disable CA2254
        logger.Log(TranslateLogLevel(msg.Severity), msg.Message);
#pragma warning restore CA2254
        return Task.CompletedTask;
    }

    private static LogLevel TranslateLogLevel(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Debug => LogLevel.Debug,
            LogSeverity.Verbose => LogLevel.Trace,
            _ => throw new ArgumentException($"Unknown log severity {severity}")
        };
    }
}
