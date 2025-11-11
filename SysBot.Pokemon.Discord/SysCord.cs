using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Discord.GatewayIntents;
using static SysBot.Pokemon.DiscordSettings;
using Discord.Net;

namespace SysBot.Pokemon.Discord;

public static class SysCordSettings
{
    public static PokeTradeHubConfig HubConfig { get; internal set; } = default!;

    public static DiscordManager Manager { get; internal set; } = default!;

    public static DiscordSettings Settings => Manager.Config;
}

public sealed class SysCord<T> where T : PKM, new()
{
    public readonly PokeTradeHub<T> Hub;
    private readonly ProgramConfig _config;
    private readonly Dictionary<ulong, ulong> _announcementMessageIds = [];
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;
    private readonly HashSet<ITradeBot> _connectedBots = [];
    private readonly object _botConnectionLock = new object();

    private readonly IServiceProvider _services;

    private readonly HashSet<string> _validCommands =
    [
        "trade", "t", "clone", "fixOT", "fix", "f", "dittoTrade", "ditto", "dt", "itemTrade", "item", "it",
        "egg", "Egg", "hidetrade", "ht", "batchTrade", "bt", "listevents", "le",
        "eventrequest", "er", "battlereadylist", "brl", "battlereadyrequest", "brr", "pokepaste", "pp",
        "PokePaste", "PP", "randomteam", "rt", "RandomTeam", "Rt", "specialrequestpokemon", "srp",
        "queueStatus", "qs", "queueClear", "qc", "ts", "tc", "deleteTradeCode", "dtc", "mysteryegg", "me"
    ];

    private readonly DiscordManager Manager;
    private readonly SemaphoreSlim _reconnectSemaphore = new(1, 1);
    private CancellationTokenSource? _reconnectCts;

    public SysCord(PokeBotRunner<T> runner, ProgramConfig config)
    {
        Runner = runner;
        Hub = runner.Hub;
        Manager = new DiscordManager(Hub.Config.Discord);
        _config = config;

        foreach (var bot in runner.Hub.Bots.ToArray())
        {
            if (bot is ITradeBot tradeBot)
            {
                tradeBot.ConnectionSuccess += async (sender, e) =>
                {
                    bool shouldHandleStart = false;

                    lock (_botConnectionLock)
                    {
                        _connectedBots.Add(tradeBot);
                        if (_connectedBots.Count == 1)
                        {
                            // First bot connected, handle start outside lock
                            shouldHandleStart = true;
                        }
                    }

                    if (shouldHandleStart)
                    {
                        await HandleBotStart();
                    }
                };

                tradeBot.ConnectionError += async (sender, ex) =>
                {
                    bool shouldHandleStop = false;

                    lock (_botConnectionLock)
                    {
                        _connectedBots.Remove(tradeBot);
                        if (_connectedBots.Count == 0)
                        {
                            // All bots disconnected, handle stop outside lock
                            shouldHandleStop = true;
                        }
                    }

                    if (shouldHandleStop)
                    {
                        await HandleBotStop();
                    }
                };
            }
        }

        SysCordSettings.Manager = Manager;
        SysCordSettings.HubConfig = Hub.Config;

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            // How much logging do you want to see?
            LogLevel = LogSeverity.Info,
            GatewayIntents = Guilds | GuildMessages | DirectMessages | GuildMembers | GuildPresences | MessageContent,

            // If you or another service needs to do anything with messages
            // (ex. checking Reactions, checking the content of edited/deleted messages),
            // you must set the MessageCacheSize. You may adjust the number as needed.
            //MessageCacheSize = 50,
        });

        _commands = new CommandService(new CommandServiceConfig
        {
            // Again, log level:
            LogLevel = LogSeverity.Info,

            DefaultRunMode = RunMode.Async,

            // There's a few more properties you can set,
            // for example, case-insensitive commands.
            CaseSensitiveCommands = false,
        });

        // Subscribe the logging handler to both the client and the CommandService.
        _client.Log += Log;
        _commands.Log += Log;

        // Setup your DI container.
        _services = ConfigureServices();

        _client.PresenceUpdated += Client_PresenceUpdated;

        _client.Disconnected += (exception) =>
        {
            LogUtil.LogText($"Discord 连接已断开。原因：{exception?.Message ?? "未知"}");
            Task.Run(() => ReconnectAsync());
            return Task.CompletedTask;
        };
    }

    public static PokeBotRunner<T> Runner { get; private set; } = default!;

    // Track loading of Echo/Logging channels, so they aren't loaded multiple times.
    private bool MessageChannelsLoaded { get; set; }

    private async Task ReconnectAsync()
    {
        // Prevent multiple concurrent reconnection attempts
        if (!await _reconnectSemaphore.WaitAsync(0).ConfigureAwait(false))
        {
            LogUtil.LogText("客户端正在尝试重新连接。");
            return;
        }

        try
        {
            // Cancel any previous reconnection attempt
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = new CancellationTokenSource();
            var cancellationToken = _reconnectCts.Token;

            const int maxRetries = 5;
            const int delayBetweenRetries = 5000; // 5 seconds
            const int initialDelay = 10000; // 10 seconds

            // Initial delay to allow Discord's automatic reconnection
            await Task.Delay(initialDelay, cancellationToken).ConfigureAwait(false);

            for (int i = 0; i < maxRetries; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LogUtil.LogText("已取消重新连接尝试。");
                    return;
                }

                try
                {
                    if (_client.ConnectionState == ConnectionState.Connected)
                    {
                        LogUtil.LogText("客户端已自动重新连接。");
                        return; // Already reconnected
                    }

                    // Check if the client is in the process of reconnecting
                    if (_client.ConnectionState == ConnectionState.Connecting)
                    {
                        LogUtil.LogText("正在等待自动重新连接...");
                        await Task.Delay(delayBetweenRetries, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    await _client.StartAsync().ConfigureAwait(false);
                    LogUtil.LogText("重新连接成功。");
                    return;
                }
                catch (Exception ex)
                {
                    LogUtil.LogText($"第 {i + 1} 次重新连接失败：{ex.Message}");
                    if (i < maxRetries - 1)
                        await Task.Delay(delayBetweenRetries, cancellationToken).ConfigureAwait(false);
                }
            }

            // If all attempts to reconnect fail, stop and restart the bot
            LogUtil.LogText("多次尝试重新连接失败，正在重启机器人...");

            try
            {
                // Stop the bot cleanly
                if (_client.ConnectionState != ConnectionState.Disconnected)
                {
                    await _client.StopAsync().ConfigureAwait(false);
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                }

                // Restart the bot
                await _client.StartAsync().ConfigureAwait(false);
                LogUtil.LogText("机器人重启成功。");
            }
            catch (Exception ex)
            {
                LogUtil.LogText($"机器人重启失败：{ex.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            LogUtil.LogText("已取消重新连接。");
        }
        catch (Exception ex)
        {
            LogUtil.LogText($"ReconnectAsync 中出现未预期的错误：{ex.Message}");
        }
        finally
        {
            _reconnectSemaphore.Release();
        }
    }

    public async Task AnnounceBotStatus(string status, EmbedColorOption color)
    {
        if (!SysCordSettings.Settings.BotEmbedStatus)
            return;

        var botName = string.IsNullOrEmpty(SysCordSettings.HubConfig.BotName) ? "SysBot" : SysCordSettings.HubConfig.BotName;
        var statusText = status switch
        {
            "Online" => "上线",
            "Offline" => "离线",
            _ => status
        };
        var fullStatusMessage = $"**状态**：{botName} 当前{statusText}！";
        var thumbnailUrl = status == "Online"
            ? "https://raw.githubusercontent.com/hexbyt3/sprites/main/botgo.png"
            : "https://raw.githubusercontent.com/hexbyt3/sprites/main/botstop.png";

        var embed = new EmbedBuilder()
            .WithTitle("机器人状态报告")
            .WithDescription(fullStatusMessage)
            .WithColor(EmbedColorConverter.ToDiscordColor(color))
            .WithThumbnailUrl(thumbnailUrl)
            .WithTimestamp(DateTimeOffset.Now)
            .Build();

        foreach (var channelId in SysCordSettings.Manager.WhitelistedChannels.List.Select(channel => channel.ID))
        {
            try
            {
                ITextChannel? textChannel = _client.GetChannel(channelId) as ITextChannel;
                if (textChannel == null)
                {
                    var restChannel = await _client.Rest.GetChannelAsync(channelId);
                    textChannel = restChannel as ITextChannel;
                }

                if (textChannel != null)
                {
                    if (_announcementMessageIds.TryGetValue(channelId, out ulong messageId))
                    {
                        try
                        {
                            await textChannel.DeleteMessageAsync(messageId);
                        }
                        catch { }
                    }
                    var message = await textChannel.SendMessageAsync(embed: embed);
                    _announcementMessageIds[channelId] = message.Id;

                    if (SysCordSettings.Settings.ChannelStatus)
                    {
                        try
                        {
                            var emoji = status == "Online"
                                ? SysCordSettings.Settings.OnlineEmoji
                                : SysCordSettings.Settings.OfflineEmoji;
                            var currentName = textChannel.Name;
                            var updatedChannelName = $"{emoji}{TrimStatusEmoji(currentName)}";

                            if (currentName != updatedChannelName)
                            {
                                await textChannel.ModifyAsync(x => x.Name = updatedChannelName);
                            }
                        }
                        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.InsufficientPermissions)
                        {
                            LogUtil.LogInfo("SysCord", $"无法更新频道 {channelId} 的名称：缺少“管理频道”权限");
                        }
                        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.RequestEntityTooLarge)
                        {
                            LogUtil.LogInfo("SysCord", $"无法更新频道 {channelId} 的名称：受到速率限制");
                        }
                        catch (Exception ex)
                        {
                            LogUtil.LogInfo("SysCord", $"更新频道 {channelId} 名称失败：{ex.Message}");
                        }
                    }
                }
                else
                {
                    LogUtil.LogInfo("SysCord", $"频道 {channelId} 不是文本频道或无法找到");
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogInfo("SysCord", $"AnnounceBotStatus：频道 {channelId} 发生异常：{ex.Message}");
            }
        }
    }

    public async Task HandleBotStart()
    {
        try
        {
            await AnnounceBotStatus("Online", EmbedColorOption.Green);
        }
        catch (Exception ex)
        {
            LogUtil.LogText($"HandleBotStart：在广播机器人启动时发生异常：{ex.Message}");
        }
    }

    public async Task HandleBotStop()
    {
        try
        {
            await AnnounceBotStatus("Offline", EmbedColorOption.Red);
        }
        catch (Exception ex)
        {
            LogUtil.LogText($"HandleBotStop：在广播机器人停止时发生异常：{ex.Message}");
        }
    }

    private void InitializeRecoveryNotifications()
    {
        if (!Hub.Config.Recovery.EnableRecovery)
            return;

        // Get the recovery service from the runner
        var recoveryService = Runner.GetRecoveryService();
        if (recoveryService == null)
            return;

        // Determine the notification channel
        ulong? notificationChannelId = null;
        if (Manager.WhitelistedChannels.List.Count > 0)
        {
            // Use the first whitelisted channel for notifications
            notificationChannelId = Manager.WhitelistedChannels.List[0].ID;
        }

        // Initialize the recovery notification helper
        var hubName = string.IsNullOrEmpty(Hub.Config.BotName) ? "SysBot" : Hub.Config.BotName;
        RecoveryNotificationHelper.Initialize(_client, notificationChannelId, hubName);
        
        // Hook up the recovery events
        RecoveryNotificationHelper.HookRecoveryEvents(recoveryService);
        
        LogUtil.LogInfo("Recovery", "已为 Discord 初始化恢复通知");
    }

    public async Task InitCommands()
    {
        var assembly = Assembly.GetExecutingAssembly();

        await _commands.AddModulesAsync(assembly, _services).ConfigureAwait(false);
        foreach (var t in assembly.DefinedTypes.Where(z => z.IsSubclassOf(typeof(ModuleBase<SocketCommandContext>)) && z.IsGenericType))
        {
            var genModule = t.MakeGenericType(typeof(T));
            await _commands.AddModuleAsync(genModule, _services).ConfigureAwait(false);
        }
        var modules = _commands.Modules.ToList();

        var blacklist = Hub.Config.Discord.ModuleBlacklist
            .Replace("Module", "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(z => z.Trim()).ToList();

        foreach (var module in modules)
        {
            var name = module.Name;
            name = name.Replace("Module", "");
            var gen = name.IndexOf('`');
            if (gen != -1)
                name = name[..gen];
            if (blacklist.Any(z => z.Equals(name, StringComparison.OrdinalIgnoreCase)))
                await _commands.RemoveModuleAsync(module).ConfigureAwait(false);
        }

        // Subscribe a handler to see if a message invokes a command.
        _client.Ready += LoadLoggingAndEcho;
        _client.MessageReceived += HandleMessageAsync;
    }

    public async Task MainAsync(string apiToken, CancellationToken token)
    {
        // Centralize the logic for commands into a separate method.
        await InitCommands().ConfigureAwait(false);

        // Login and connect.
        await _client.LoginAsync(TokenType.Bot, apiToken).ConfigureAwait(false);
        await _client.StartAsync().ConfigureAwait(false);

        var app = await _client.GetApplicationInfoAsync().ConfigureAwait(false);
        Manager.Owner = app.Owner.Id;

        // Initialize recovery notifications if recovery is enabled
        InitializeRecoveryNotifications();
        try
        {
            // Wait infinitely so your bot actually stays connected.
            await MonitorStatusAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Handle the cancellation and perform cleanup tasks
            LogUtil.LogText("MainAsync：因取消操作，机器人正在断开连接...");
            await AnnounceBotStatus("Offline", EmbedColorOption.Red);
            LogUtil.LogText("MainAsync：清理任务已完成。");
        }
        finally
        {
            // Cancel any ongoing reconnection attempts
            _reconnectCts?.Cancel();

            // Disconnect the bot
            await _client.StopAsync();

            // Dispose resources
            _reconnectCts?.Dispose();
            _reconnectSemaphore?.Dispose();
            _client?.Dispose();
        }
    }

    // If any services require the client, or the CommandService, or something else you keep on hand,
    // pass them as parameters into this method as needed.
    // If this method is getting pretty long, you can separate it out into another file using partials.
    private static ServiceProvider ConfigureServices()
    {
        var map = new ServiceCollection();//.AddSingleton(new SomeServiceClass());

        // When all your required services are in the collection, build the container.
        // Tip: There's an overload taking in a 'validateScopes' bool to make sure
        // you haven't made any mistakes in your dependency graph.
        return map.BuildServiceProvider();
    }

    // Example of a logging handler. This can be reused by add-ons
    // that ask for a Func<LogMessage, Task>.

    private static ConsoleColor GetTextColor(LogSeverity sv) => sv switch
    {
        LogSeverity.Critical => ConsoleColor.Red,
        LogSeverity.Error => ConsoleColor.Red,

        LogSeverity.Warning => ConsoleColor.Yellow,
        LogSeverity.Info => ConsoleColor.White,

        LogSeverity.Verbose => ConsoleColor.DarkGray,
        LogSeverity.Debug => ConsoleColor.DarkGray,
        _ => Console.ForegroundColor,
    };

    private static Task Log(LogMessage msg)
    {
        var text = $"[{msg.Severity,8}] {msg.Source}：{msg.Message} {msg.Exception}";
        Console.ForegroundColor = GetTextColor(msg.Severity);
        Console.WriteLine($"{DateTime.Now,-19} {text}");
        Console.ResetColor();

        LogUtil.LogText($"SysCord：{text}");

        return Task.CompletedTask;
    }

    private static async Task RespondToThanksMessage(SocketUserMessage msg)
    {
        var channel = msg.Channel;
        await channel.TriggerTypingAsync();
        await Task.Delay(500).ConfigureAwait(false);

        var responses = new List<string>
        {
            "不客气！❤️",
            "完全没问题！",
            "随时恭候，很高兴帮到你！",
            "乐意效劳！❤️",
            "别客气！欢迎继续使用！",
            "一直都在为你提供帮助！",
            "很高兴能协助你！",
            "服务就是我的使命！",
            "当然可以！不必客气！",
            "包在我身上！"
        };

        var randomResponse = responses[new Random().Next(responses.Count)];
        var finalResponse = $"{randomResponse}";

        await msg.Channel.SendMessageAsync(finalResponse).ConfigureAwait(false);
    }

    private static string TrimStatusEmoji(string channelName)
    {
        var onlineEmoji = SysCordSettings.Settings.OnlineEmoji;
        var offlineEmoji = SysCordSettings.Settings.OfflineEmoji;

        if (channelName.StartsWith(onlineEmoji))
        {
            return channelName[onlineEmoji.Length..].Trim();
        }

        if (channelName.StartsWith(offlineEmoji))
        {
            return channelName[offlineEmoji.Length..].Trim();
        }

        return channelName.Trim();
    }

    private Task Client_PresenceUpdated(SocketUser user, SocketPresence before, SocketPresence after)
    {
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(SocketMessage arg)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (arg is not SocketUserMessage msg)
                return;

            if (msg.Channel is SocketGuildChannel guildChannel)
            {
                if (Manager.BlacklistedServers.Contains(guildChannel.Guild.Id))
                {
                    await guildChannel.Guild.LeaveAsync();
                    return;
                }
            }

            if (msg.Author.Id == _client.CurrentUser.Id || msg.Author.IsBot)
                return;

            string thanksText = msg.Content.ToLower();
            if (SysCordSettings.Settings.ReplyToThanks && (thanksText.Contains("thank") || thanksText.Contains("thx")))
            {
                await SysCord<T>.RespondToThanksMessage(msg).ConfigureAwait(false);
                return;
            }

            var correctPrefix = SysCordSettings.Settings.CommandPrefix;
            var content = msg.Content;
            var argPos = 0;

            if (msg.HasMentionPrefix(_client.CurrentUser, ref argPos) || msg.HasStringPrefix(correctPrefix, ref argPos))
            {
                var context = new SocketCommandContext(_client, msg);
                var handled = await TryHandleCommandAsync(msg, context, argPos);
                if (handled)
                    return;
            }
            else if (content.Length > 1 && content[0] != correctPrefix[0])
            {
                var potentialPrefix = content[0].ToString();
                var command = content.Split(' ')[0][1..];
                if (_validCommands.Contains(command))
                {
                    await SafeSendMessageAsync(msg.Channel, $"前缀使用错误！正确的指令为 **{correctPrefix}{command}**").ConfigureAwait(false);
                    return;
                }
            }

            if (msg.Attachments.Count > 0)
            {
                await TryHandleAttachmentAsync(msg).ConfigureAwait(false);
            }
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.InsufficientPermissions) // Missing Permissions
        {
            await Log(new LogMessage(LogSeverity.Warning, "指令", $"缺少在频道 {arg.Channel.Name} 处理中消息所需的权限")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Log(new LogMessage(LogSeverity.Error, "指令", $"HandleMessageAsync 中出现未处理的异常：{ex.Message}", ex)).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 1000) // Log if processing takes more than 1 second
            {
                await Log(new LogMessage(LogSeverity.Warning, "网关",
                    $"MessageReceived 处理程序阻塞了网关任务。" +
                    $" 方法：HandleMessageAsync，执行耗时：{stopwatch.ElapsedMilliseconds} 毫秒，" +
                    $"消息内容：{arg.Content[..Math.Min(arg.Content.Length, 100)]}...")).ConfigureAwait(false);
            }
        }
    }

    private async Task LoadLoggingAndEcho()
    {
        if (MessageChannelsLoaded)
            return;

        // Restore Echoes
        EchoModule.RestoreChannels(_client, Hub.Config.Discord);

        // Subscribe to queue status changes
        QueueMonitor<T>.OnQueueStatusChanged = async (isFull, currentCount, maxCount) =>
        {
            await EchoModule.SendQueueStatusEmbedAsync(isFull, currentCount, maxCount).ConfigureAwait(false);
        };

        // Restore Logging
        LogModule.RestoreLogging(_client, Hub.Config.Discord);
        TradeStartModule<T>.RestoreTradeStarting(_client);

        // Don't let it load more than once in case of Discord hiccups.
        await Log(new LogMessage(LogSeverity.Info, "加载日志与回声", "日志与回声频道已加载！")).ConfigureAwait(false);
        MessageChannelsLoaded = true;

        var game = Hub.Config.Discord.BotGameStatus;
        if (!string.IsNullOrWhiteSpace(game))
            await _client.SetGameAsync(game).ConfigureAwait(false);
    }

    private async Task MonitorStatusAsync(CancellationToken token)
    {
        const int Interval = 20; // seconds

        // Check datetime for update
        UserStatus state = UserStatus.Idle;
        while (!token.IsCancellationRequested)
        {
            var time = DateTime.Now;
            var lastLogged = LogUtil.LastLogged;
            if (Hub.Config.Discord.BotColorStatusTradeOnly)
            {
                var recent = Hub.Bots.ToArray()
                    .Where(z => z.Config.InitialRoutine.IsTradeBot())
                    .MaxBy(z => z.LastTime);
                lastLogged = recent?.LastTime ?? time;
            }
            var delta = time - lastLogged;
            var gap = TimeSpan.FromSeconds(Interval) - delta;

            bool noQueue = !Hub.Queues.Info.GetCanQueue();
            if (gap <= TimeSpan.Zero)
            {
                var idle = noQueue ? UserStatus.DoNotDisturb : UserStatus.Idle;
                if (idle != state)
                {
                    state = idle;
                    await _client.SetStatusAsync(state).ConfigureAwait(false);
                }
                await Task.Delay(2_000, token).ConfigureAwait(false);
                continue;
            }

            var active = noQueue ? UserStatus.DoNotDisturb : UserStatus.Online;
            if (active != state)
            {
                state = active;
                await _client.SetStatusAsync(state).ConfigureAwait(false);
            }
            await Task.Delay(gap, token).ConfigureAwait(false);
        }
    }


    private async Task TryHandleAttachmentAsync(SocketMessage msg)
    {
        var mgr = Manager;
        var cfg = mgr.Config;
        if (cfg.ConvertPKMToShowdownSet && (cfg.ConvertPKMReplyAnyChannel || mgr.CanUseCommandChannel(msg.Channel.Id)))
        {
            if (msg is SocketUserMessage userMessage)
            {
                foreach (var att in msg.Attachments)
                    await msg.Channel.RepostPKMAsShowdownAsync(att, userMessage).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> TryHandleCommandAsync(SocketUserMessage msg, SocketCommandContext context, int pos)
    {
        try
        {
            var AbuseSettings = Hub.Config.TradeAbuse;
            // Check if the user is in the bannedIDs list
            if (msg.Author is SocketGuildUser user && AbuseSettings.BannedIDs.List.Any(z => z.ID == user.Id))
            {
                await SysCord<T>.SafeSendMessageAsync(msg.Channel, "你已被禁止使用此机器人。").ConfigureAwait(false);
                return true;
            }

            var mgr = Manager;
            if (!mgr.CanUseCommandUser(msg.Author.Id))
            {
                await SysCord<T>.SafeSendMessageAsync(msg.Channel, "你没有权限使用该指令。").ConfigureAwait(false);
                return true;
            }

            if (!mgr.CanUseCommandChannel(msg.Channel.Id) && msg.Author.Id != mgr.Owner)
            {
                if (Hub.Config.Discord.ReplyCannotUseCommandInChannel)
                    await SysCord<T>.SafeSendMessageAsync(msg.Channel, "此频道无法使用该指令。").ConfigureAwait(false);
                return true;
            }

            var guild = msg.Channel is SocketGuildChannel g ? g.Guild.Name : "未知服务器";
            await Log(new LogMessage(LogSeverity.Info, "指令", $"正在执行来自 {guild}#{msg.Channel.Name} 的指令：@{msg.Author.Username}。内容：{msg}")).ConfigureAwait(false);

            var result = await _commands.ExecuteAsync(context, pos, _services).ConfigureAwait(false);

            if (result.Error == CommandError.UnknownCommand)
                return false;

            if (!result.IsSuccess)
                await SafeSendMessageAsync(msg.Channel, result.ErrorReason).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            await Log(new LogMessage(LogSeverity.Error, "指令", $"执行指令时出错：{ex.Message}", ex)).ConfigureAwait(false);
            return false;
        }
    }

    private static async Task SafeSendMessageAsync(IMessageChannel channel, string message)
    {
        try
        {
            await channel.SendMessageAsync(message).ConfigureAwait(false);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.InsufficientPermissions) // Missing Permissions
        {
            await Log(new LogMessage(LogSeverity.Warning, "指令", $"缺少在频道 {channel.Name} 发送消息的权限")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Log(new LogMessage(LogSeverity.Error, "指令", $"发送消息时出错：{ex.Message}", ex)).ConfigureAwait(false);
        }
    }
}
