using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using anna_bot.Domain;
using anna_bot.Domain.Models.Configurations;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace anna_bot.InServices;

public class DiscordBot(
    IServiceProvider serviceProvider,
    IOptions<DiscordConfiguration> discordConfig,
    DiscordSocketClient client,
    InteractionService interactionService,
    PlayerState playerState,
    ILogger<DiscordBot> logger)
{
    public async Task RunAsync()
    {
        logger.LogInformation("Starting {BotName}...", discordConfig.Value.BotName);
        
        client.Ready += OnReady;
        client.InteractionCreated += HandleInteraction;
        client.MessageDeleted += OnMessageDeleted;
        client.Log += Log;
        interactionService.Log += Log;

        await client.LoginAsync(TokenType.Bot, discordConfig.Value.Token);
        await client.StartAsync();

        await Task.Delay(-1);
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        var ctx = new SocketInteractionContext(client, interaction);
        var result = await interactionService.ExecuteCommandAsync(ctx, serviceProvider);
        if (!result.IsSuccess)
            logger.LogError("Command execution failed: {Error} - {ErrorReason}", result.Error, result.ErrorReason);
    }

    private async Task OnReady()
    {
        logger.LogInformation("{BotName} is ready!", discordConfig.Value.BotName);

        await interactionService.AddModulesAsync(typeof(Program).Assembly, serviceProvider);
        _ = Task.Run(async () =>
        {
            try
            {
                if (discordConfig.Value.RemoveCommands)
                {
                    await RemoveGlobalCommands();
                    //await RemoveGuildCommands(discordConfig.Value.GuildId);
                }
                else
                {
                    await interactionService.RegisterCommandsGloballyAsync();
                    //await interactionService.RegisterCommandsToGuildAsync(discordConfig.Value.GuildId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{BotName} failed to register commands", discordConfig.Value.BotName);
            }
        });
    }

    private async Task RemoveGuildCommands(ulong guildId)
    {
        var commands = await client.GetGuild(guildId).GetApplicationCommandsAsync();
        foreach (var command in commands)
        {
            await command.DeleteAsync();
            logger.LogInformation("Deleted guild command: {CommandName} from {BotName}, and {Guild}", command.Name, discordConfig.Value.BotName, guildId);
        }
    }

    private async Task RemoveGlobalCommands()
    {
        var commands = await client.GetGlobalApplicationCommandsAsync();
        foreach (var command in commands)
        {
            await command.DeleteAsync();
            logger.LogInformation("Deleted global command: {CommandName} for {BotName}", command.Name, discordConfig.Value.BotName);
        }
    }

    private async Task Log(LogMessage msg)
    {
        if (msg.Exception != null)
        {
            logger.LogError(msg.Exception, "{BotName}: An exception was thrown from the discord client", discordConfig.Value.BotName);
            if (msg.Exception is WebSocketException && msg.Exception.Message.Contains("WebSocket connection was closed"))
            {
                await playerState.ReconnectPlayers();
            }
        }
        
        if (msg.Message != null)
            logger.Log(TranslateLogLevel(msg.Severity), "{BotName}: {ErrorMessage}", discordConfig.Value.BotName, msg.Message);
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

    private Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        if (message.HasValue && message.Value.Author.Id != discordConfig.Value.ClientId)
        {
            logger.LogInformation("Message deleted: {MessageContent} by {Author} from {Channel} ({ChannelId})", 
                message.Value.Content, message.Value.Author.Username, message.Value.Channel.Name, message.Value.Channel.Id);
        }
        
        return Task.CompletedTask;
    }
}
