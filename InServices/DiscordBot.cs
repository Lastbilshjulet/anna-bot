using System;
using System.Linq;
using System.Threading.Tasks;
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
    ILogger<DiscordBot> logger)
{
    public async Task RunAsync()
    {
        logger.LogInformation("Starting {BotName}...", discordConfig.Value.BotName);
        
        client.Ready += OnReady;
        client.InteractionCreated += HandleInteraction;
        client.MessageDeleted += OnMessageDeleted;
        client.Log += Log;

        await client.LoginAsync(TokenType.Bot, discordConfig.Value.Token);
        await client.StartAsync();
        
        logger.LogInformation("{BotName} is connected to guilds {Guilds}", discordConfig.Value.BotName, string.Join(", ", client.Guilds.Select(x => x.Name)));

        await Task.Delay(-1);
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        var ctx = new SocketInteractionContext(client, interaction);
        await interactionService.ExecuteCommandAsync(ctx, serviceProvider);
    }

    private async Task OnReady()
    {
        try
        {
            logger.LogInformation("{BotName} is ready!", discordConfig.Value.BotName);
        
            await interactionService.AddModulesAsync(typeof(Program).Assembly, serviceProvider);
            if (discordConfig.Value.RemoveCommands)
            {
                await RemoveGlobalCommands();
                await RemoveGuildCommands(discordConfig.Value.GuildId);
            }
            else
            {
                await interactionService.RegisterCommandsGloballyAsync();
                await interactionService.RegisterCommandsToGuildAsync(discordConfig.Value.GuildId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{BotName} failed to be ready", discordConfig.Value.BotName);
        }
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

    private Task Log(LogMessage msg)
    {
        logger.Log(TranslateLogLevel(msg.Severity), "{BotName}: {ErrorMessage}", discordConfig.Value.BotName, msg.Message);
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

    private Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        if (message.HasValue && message.Value.Author.Id != discordConfig.Value.ClientId)
        {
            logger.LogInformation("Message deleted: {MessageContent} by {Author} from {Channel} ({ChannelId})", 
                message.Value.Content, message.Value.Author.Username, message.Value.Channel.Name, message.Value.Channel.Id);
            return Task.CompletedTask;
        }

        if (channel.HasValue)
        {
            logger.LogInformation("Message not in cache, deleted from channel: {ChannelName} ({ChannelId})", 
                channel.Value.Name, channel.Value.Id);
        }
        
        return Task.CompletedTask;
    }
}
