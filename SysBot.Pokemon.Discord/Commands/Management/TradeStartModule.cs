using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord;

public class TradeStartModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private class TradeStartAction(ulong ChannelId, Action<PokeRoutineExecutorBase, PokeTradeDetail<T>> messager, string channel)
        : ChannelAction<PokeRoutineExecutorBase, PokeTradeDetail<T>>(ChannelId, messager, channel);

    private static DiscordSocketClient? _discordClient;

    private static readonly Dictionary<ulong, TradeStartAction> Channels = [];

    private static void Remove(TradeStartAction entry)
    {
        Channels.Remove(entry.ChannelID);
        SysCord<T>.Runner.Hub.Queues.Forwarders.Remove(entry.Action);
    }

#pragma warning disable RCS1158 // Static member in generic type should use a type parameter.

    public static void RestoreTradeStarting(DiscordSocketClient discord)
    {
        _discordClient = discord; // Store the DiscordSocketClient instance

        var cfg = SysCordSettings.Settings;
        foreach (var ch in cfg.TradeStartingChannels)
        {
            if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                AddLogChannel(c, ch.ID);
        }

        LogUtil.LogInfo("Discord", "机器人启动时已将交易开始通知添加到 Discord 频道。");
    }

    public static bool IsStartChannel(ulong cid)
#pragma warning restore RCS1158 // Static member in generic type should use a type parameter.
    {
        return Channels.TryGetValue(cid, out _);
    }

    [Command("startHere")]
    [Summary("在当前频道启用交易开始通知。")]
    [RequireSudo]
    public async Task AddLogAsync()
    {
        var c = Context.Channel;
        var cid = c.Id;
        if (Channels.TryGetValue(cid, out _))
        {
            await ReplyAsync("该频道已启用交易开始通知。").ConfigureAwait(false);
            return;
        }

        AddLogChannel(c, cid);

        // Add to discord global loggers (saves on program close)
        SysCordSettings.Settings.TradeStartingChannels.AddIfNew([GetReference(Context.Channel)]);
        await ReplyAsync("已在此频道启用交易开始通知！").ConfigureAwait(false);
    }

    private static void AddLogChannel(ISocketMessageChannel c, ulong cid)
    {
        async void Logger(PokeRoutineExecutorBase bot, PokeTradeDetail<T> detail)
        {
            if (detail.Type == PokeTradeType.Random) return;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            var user = _discordClient.GetUser(detail.Trainer.ID);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            if (user == null) { Console.WriteLine($"未找到 ID 为 {detail.Trainer.ID} 的用户。"); return; }

            string speciesName = detail.TradeData != null ? GameInfo.Strings.Species[detail.TradeData.Species] : "";
            string ballImgUrl = "https://raw.githubusercontent.com/hexbyt3/sprites/36e891cc02fe283cd70d9fc8fef2f3c490096d6c/imgs/difficulty.png";

            if (detail.TradeData != null && detail.Type != PokeTradeType.Clone && detail.Type != PokeTradeType.Dump && detail.Type != PokeTradeType.Seed && detail.Type != PokeTradeType.FixOT)
            {
                var ballName = GameInfo.GetStrings("en").balllist[detail.TradeData.Ball]
                    .Replace(" ", "").Replace("(LA)", "").ToLower();
                ballName = ballName == "pokéball" ? "pokeball" : (ballName.Contains("(la)") ? "la" + ballName : ballName);
                ballImgUrl = $"https://raw.githubusercontent.com/hexbyt3/sprites/main/AltBallImg/28x28/{ballName}.png";
            }

            string tradeTitle = detail.IsMysteryEgg ? "✨ 神秘蛋" : detail.Type switch
            {
                PokeTradeType.Clone => "克隆的宝可梦",
                PokeTradeType.Dump => "宝可梦导出",
                PokeTradeType.FixOT => "克隆宝可梦（修复 OT 信息）",
                PokeTradeType.Seed => "克隆宝可梦（特殊请求）",
                _ => speciesName
            };

            string embedImageUrl = detail.IsMysteryEgg ? "https://raw.githubusercontent.com/hexbyt3/sprites/main/mysteryegg3.png" : detail.Type switch
            {
                PokeTradeType.Clone => "https://raw.githubusercontent.com/hexbyt3/sprites/main/clonepod.png",
                PokeTradeType.Dump => "https://raw.githubusercontent.com/hexbyt3/sprites/main/AltBallImg/128x128/dumpball.png",
                PokeTradeType.FixOT => "https://raw.githubusercontent.com/hexbyt3/sprites/main/AltBallImg/128x128/rocketball.png",
                PokeTradeType.Seed => "https://raw.githubusercontent.com/hexbyt3/sprites/main/specialrequest.png",
                _ => detail.TradeData != null ? TradeExtensions<T>.PokeImg(detail.TradeData, false, true) : ""
            };

            var (r, g, b) = await GetDominantColorAsync(embedImageUrl);

            string footerText = detail.Type == PokeTradeType.Clone || detail.Type == PokeTradeType.Dump || detail.Type == PokeTradeType.Seed || detail.Type == PokeTradeType.FixOT
                ? "正在初始化交易。"
                : $"正在初始化交易，祝你享用 {(detail.IsMysteryEgg ? "✨ 神秘蛋" : speciesName)}！";

            var embed = new EmbedBuilder()
                .WithColor(new DiscordColor(r, g, b))
                .WithThumbnailUrl(embedImageUrl)
                .WithAuthor($"即将开始：{user.Username}", user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithDescription($"**将收到**：{tradeTitle}\n**Trade ID**：{detail.ID}")
                .WithFooter($"{footerText}\u200B", ballImgUrl)
                .WithTimestamp(DateTime.Now)
                .Build();

            await c.SendMessageAsync(embed: embed);
        }

        SysCord<T>.Runner.Hub.Queues.Forwarders.Add(Logger);
        Channels.Add(cid, new TradeStartAction(cid, Logger, c.Name));
    }

    [Command("startInfo")]
    [Summary("列出当前交易开始通知设置。")]
    [RequireSudo]
    public async Task DumpLogInfoAsync()
    {
        foreach (var c in Channels)
            await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
    }

    [Command("startClear")]
    [Summary("清除当前频道的交易开始通知设置。")]
    [RequireSudo]
    public async Task ClearLogsAsync()
    {
        var cfg = SysCordSettings.Settings;
        if (Channels.TryGetValue(Context.Channel.Id, out var entry))
            Remove(entry);
        cfg.TradeStartingChannels.RemoveAll(z => z.ID == Context.Channel.Id);
        await ReplyAsync($"已从频道 {Context.Channel.Name} 清除交易开始通知。").ConfigureAwait(false);
    }

    [Command("startClearAll")]
    [Summary("清除所有频道的交易开始通知设置。")]
    [RequireSudo]
    public async Task ClearLogsAllAsync()
    {
        foreach (var l in Channels)
        {
            var entry = l.Value;
            await ReplyAsync($"已从 {entry.ChannelName}（{entry.ChannelID}）清除交易开始通知！").ConfigureAwait(false);
            SysCord<T>.Runner.Hub.Queues.Forwarders.Remove(entry.Action);
        }
        Channels.Clear();
        SysCordSettings.Settings.TradeStartingChannels.Clear();
        await ReplyAsync("已清除所有频道的交易开始通知！").ConfigureAwait(false);
    }

    private RemoteControlAccess GetReference(IChannel channel) => new()
    {
        ID = channel.Id,
        Name = channel.Name,
        Comment = $"由 {Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd-hh:mm:ss} 添加",
    };

    public static async Task<(int R, int G, int B)> GetDominantColorAsync(string imagePath)
    {
        try
        {
            Bitmap image = await LoadImageAsync(imagePath);

            var colorCount = new Dictionary<Color, int>();
#pragma warning disable CA1416 // Validate platform compatibility
            for (int y = 0; y < image.Height; y++)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                for (int x = 0; x < image.Width; x++)
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    var pixelColor = image.GetPixel(x, y);
#pragma warning restore CA1416 // Validate platform compatibility

                    if (pixelColor.A < 128 || pixelColor.GetBrightness() > 0.9) continue;

                    var brightnessFactor = (int)(pixelColor.GetBrightness() * 100);
                    var saturationFactor = (int)(pixelColor.GetSaturation() * 100);
                    var combinedFactor = brightnessFactor + saturationFactor;

                    var quantizedColor = Color.FromArgb(
                        pixelColor.R / 10 * 10,
                        pixelColor.G / 10 * 10,
                        pixelColor.B / 10 * 10
                    );

                    if (colorCount.ContainsKey(quantizedColor))
                    {
                        colorCount[quantizedColor] += combinedFactor;
                    }
                    else
                    {
                        colorCount[quantizedColor] = combinedFactor;
                    }
                }
#pragma warning restore CA1416 // Validate platform compatibility
            }
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
            image.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility

            if (colorCount.Count == 0)
                return (255, 255, 255);

            var dominantColor = colorCount.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            return (dominantColor.R, dominantColor.G, dominantColor.B);
        }
        catch (Exception ex)
        {
            // Log or handle exceptions as needed
            Console.WriteLine($"处理图片 {imagePath} 时发生错误：{ex.Message}");
            return (255, 255, 255);  // Default to white if an exception occurs
        }
    }

    private static async Task<Bitmap> LoadImageAsync(string imagePath)
    {
        if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(imagePath);
            await using var stream = await response.Content.ReadAsStreamAsync();
#pragma warning disable CA1416 // Validate platform compatibility
            return new Bitmap(stream);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        else
        {
#pragma warning disable CA1416 // Validate platform compatibility
            return new Bitmap(imagePath);
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }
}
