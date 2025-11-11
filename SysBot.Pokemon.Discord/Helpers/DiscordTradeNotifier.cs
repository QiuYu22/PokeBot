using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using PKHeX.Drawing.PokeSprite;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Color = Discord.Color;

namespace SysBot.Pokemon.Discord;

public class DiscordTradeNotifier<T> : IPokeTradeNotifier<T>, IDisposable
    where T : PKM, new()
{
    private T Data { get; set; }
    private PokeTradeTrainerInfo Info { get; }
    private int Code { get; }
    private List<Pictocodes> LGCode { get; }
    private SocketUser Trader { get; }
    private int BatchTradeNumber { get; set; }
    private int TotalBatchTrades { get; }
    private bool IsMysteryEgg { get; }

    private readonly ulong _traderID;
    private int _uniqueTradeID;
    private Timer? _periodicUpdateTimer;
    private const int PeriodicUpdateInterval = 60000; // 60 seconds in milliseconds
    private bool _isTradeActive = true;
    private bool _initialUpdateSent = false;
    private bool _almostUpNotificationSent = false;
    private int _lastReportedPosition = -1;

    public readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;

    public DiscordTradeNotifier(T data, PokeTradeTrainerInfo info, int code, SocketUser trader, int batchTradeNumber, int totalBatchTrades, bool isMysteryEgg, List<Pictocodes> lgcode)
    {
        Data = data;
        Info = info;
        Code = code;
        Trader = trader;
        BatchTradeNumber = batchTradeNumber;
        TotalBatchTrades = totalBatchTrades;
        IsMysteryEgg = isMysteryEgg;
        LGCode = lgcode;
        _traderID = trader.Id;
        _uniqueTradeID = GetUniqueTradeID();
    }

    public Action<PokeRoutineExecutor<T>>? OnFinish { private get; set; }

    public void UpdateBatchProgress(int currentBatchNumber, T currentPokemon, int uniqueTradeID)
    {
        BatchTradeNumber = currentBatchNumber;
        Data = currentPokemon;
        _uniqueTradeID = uniqueTradeID;
    }

    public void UpdateUniqueTradeID(int uniqueTradeID)
    {
        _uniqueTradeID = uniqueTradeID;
    }

    private int GetUniqueTradeID()
    {
        // Generate a unique trade ID using timestamp or another method
        return (int)(DateTime.UtcNow.Ticks % int.MaxValue);
    }

    private void StartPeriodicUpdates()
    {
        // Dispose existing timer if it exists
        _periodicUpdateTimer?.Dispose();

        _isTradeActive = true;

        // Create a new timer that checks if user is up next
        // Only sends ONE notification when they're truly up next to avoid Discord spam
        _periodicUpdateTimer = new Timer(async _ =>
        {
            if (!_isTradeActive)
                return;

            // Check the current position using the unique trade ID
            var position = Hub.Queues.Info.CheckPosition(_traderID, _uniqueTradeID, PokeRoutineType.LinkTrade);
            if (!position.InQueue)
                return;

            var currentPosition = position.Position < 1 ? 1 : position.Position;

            // Store the latest position for future reference
            _lastReportedPosition = currentPosition;

            var botct = Hub.Bots.Count;

            // Only send ONE notification when the user is truly up next (position 1 or ready to be processed)
            if (position.InQueue && position.Detail != null)
            {
                // Only notify when position is 1 (truly up next) and we haven't sent the notification yet
                if (currentPosition == 1 && _initialUpdateSent && !_almostUpNotificationSent)
                {
                    // Send notification that they're up next - only sent ONCE
                    _almostUpNotificationSent = true;

                    var batchInfo = TotalBatchTrades > 1 ? $"\n\n**重要提示：**这是一个包含 {TotalBatchTrades} 只宝可梦的批量交易，请保持在线直至全部完成！" : "";

                    var upNextEmbed = new EmbedBuilder
                    {
                        Color = Color.Gold,
                        Title = "🎯 轮到你了！",
                        Description = $"你的交易即将开始，请做好准备！{batchInfo}",
                        Footer = new EmbedFooterBuilder
                        {
                            Text = "准备连接！"
                        },
                        Timestamp = DateTimeOffset.Now
                    }.Build();

                    await Trader.SendMessageAsync(embed: upNextEmbed).ConfigureAwait(false);
                }
                // No other periodic updates - this prevents Discord spam
            }
        },
        null,
        PeriodicUpdateInterval, // Start after 60 seconds
        PeriodicUpdateInterval); // Repeat every 60 seconds
    }

    private void StopPeriodicUpdates()
    {
        _isTradeActive = false;
        _periodicUpdateTimer?.Dispose();
        _periodicUpdateTimer = null;
    }

    public async Task SendInitialQueueUpdate()
    {
        var position = Hub.Queues.Info.CheckPosition(_traderID, _uniqueTradeID, PokeRoutineType.LinkTrade);
        var currentPosition = position.Position < 1 ? 1 : position.Position;
        var botct = Hub.Bots.Count;
        var currentETA = currentPosition > botct ? Hub.Config.Queues.EstimateDelay(currentPosition, botct) : 0;

        _lastReportedPosition = currentPosition;

        var batchDescription = TotalBatchTrades > 1
            ? $"你的批量交易请求（共 {TotalBatchTrades} 只宝可梦）已加入队列。\n\n⚠️ **重要说明：**\n• 请在整个 {TotalBatchTrades} 次交易中保持在线\n• 准备好所有 {TotalBatchTrades} 只宝可梦\n• 在看到完成提示前请勿退出\n\n当前排队位置：**{currentPosition}**"
            : $"你的交易请求已加入队列。当前排队位置：**{currentPosition}**";

        var initialEmbed = new EmbedBuilder
        {
            Color = Color.Green,
            Title = TotalBatchTrades > 1 ? "🎁 批量交易请求已入队" : "交易请求已入队",
            Description = batchDescription,
            Footer = new EmbedFooterBuilder
            {
                Text = $"预计等待时间：{(currentETA > 0 ? $"{currentETA} 分钟" : "少于 1 分钟")}"
            },
            Timestamp = DateTimeOffset.Now
        }.Build();

        await Trader.SendMessageAsync(embed: initialEmbed).ConfigureAwait(false);

        _initialUpdateSent = true;

        // Start sending periodic updates about queue position
        StartPeriodicUpdates();
    }

    public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        // Update unique trade ID from the detail
        _uniqueTradeID = info.UniqueTradeID;

        // Stop periodic updates as we're now moving to the active trading phase
        StopPeriodicUpdates();

        // Mark trade as active to prevent any further queue messages
        _almostUpNotificationSent = true;

        int language = 2;
        var speciesName = IsMysteryEgg ? "神秘蛋" : SpeciesName.GetSpeciesName(Data.Species, language);
        var receive = Data.Species == 0 ? string.Empty : (IsMysteryEgg ? "" : $" ({Data.Nickname})");

        if (Data is PK9)
        {
            string message;
            if (TotalBatchTrades > 1)
            {
                if (BatchTradeNumber == 1)
                {
                    message = $"开始你的批量交易，共 {TotalBatchTrades} 只宝可梦。\n\n" +
                             $"**第 1/{TotalBatchTrades} 次交易**：{speciesName}{receive}\n\n" +
                             $"⚠️ **重要提示：**请在所有 {TotalBatchTrades} 次交易完成前保持在线！";
                }
                else
                {
                    message = $"准备进行第 {BatchTradeNumber}/{TotalBatchTrades} 次交易：{speciesName}{receive}";
                }
            }
            else
            {
                message = $"正在初始化交易{receive}，请准备好。";
            }

            EmbedHelper.SendTradeInitializingEmbedAsync(Trader, speciesName, Code, IsMysteryEgg, message).ConfigureAwait(false);
        }
        else if (Data is PB7)
        {
            var (thefile, lgcodeembed) = CreateLGLinkCodeSpriteEmbed(LGCode);
            Trader.SendFileAsync(thefile, $"正在初始化交易{receive}，请准备好。你的密码是", embed: lgcodeembed).ConfigureAwait(false);
        }
        else
        {
            EmbedHelper.SendTradeInitializingEmbedAsync(Trader, speciesName, Code, IsMysteryEgg).ConfigureAwait(false);
        }
    }

    public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        // Ensure periodic updates are stopped (extra safety check)
        StopPeriodicUpdates();

        var name = Info.TrainerName;
        var trainer = string.IsNullOrEmpty(name) ? string.Empty : $" {name}";

        if (Data is PB7 && LGCode != null && LGCode.Count != 0)
        {
            var batchInfo = TotalBatchTrades > 1 ? $"（第 {BatchTradeNumber}/{TotalBatchTrades} 次交易）" : "";
            var message = $"我正在等你{trainer}{batchInfo}！我的 IGN 是 **{routine.InGameName}**。";
            Trader.SendMessageAsync(message).ConfigureAwait(false);
        }
        else
        {
            string? additionalMessage = null;
            if (TotalBatchTrades > 1)
            {
                if (BatchTradeNumber == 1)
                {
                    additionalMessage = $"开始批量交易（共 {TotalBatchTrades} 只宝可梦）。**请先选择第一只！**";
                }
                else
                {
                    var speciesName = IsMysteryEgg ? "神秘蛋" : SpeciesName.GetSpeciesName(Data.Species, 2);
                    additionalMessage = $"第 {BatchTradeNumber}/{TotalBatchTrades} 次交易：当前交换 {speciesName}。**请选择下一只宝可梦！**";
                }
            }

            EmbedHelper.SendTradeSearchingEmbedAsync(Trader, trainer, routine.InGameName, additionalMessage).ConfigureAwait(false);
        }
    }

    public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
    {
        OnFinish?.Invoke(routine);
        StopPeriodicUpdates();

        var reason = msg.ToLocalizedString();
        var cancelMessage = TotalBatchTrades > 1
            ? $"批量交易已取消：{reason}。剩余的交易均已终止。"
            : reason;

        EmbedHelper.SendTradeCanceledEmbedAsync(Trader, cancelMessage).ConfigureAwait(false);
    }

    public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
    {
        // Only stop updates and invoke OnFinish for single trades or the last trade in a batch
        if (TotalBatchTrades <= 1 || BatchTradeNumber == TotalBatchTrades)
        {
            OnFinish?.Invoke(routine);
            StopPeriodicUpdates();
        }

        var tradedToUser = Data.Species;

        // Create different messages based on whether this is a single trade or part of a batch
        string message;
        if (TotalBatchTrades > 1)
        {
            if (BatchTradeNumber == TotalBatchTrades)
            {
                // Final trade in the batch - this is now called only once at the very end
                message = $"✅ **全部 {TotalBatchTrades} 次交易已成功完成！** 感谢你的参与！";
            }
            else
            {
                // Mid-batch trade
                var speciesName = IsMysteryEgg ? "神秘蛋" : SpeciesName.GetSpeciesName(Data.Species, 2);
                message = $"✅ 已完成第 {BatchTradeNumber}/{TotalBatchTrades} 次交易（{speciesName}）。\n" +
                         $"正在准备第 {BatchTradeNumber + 1}/{TotalBatchTrades} 次交易…";
            }
        }
        else
        {
            // Standard single trade message
            message = tradedToUser != 0 ? "交易完成，祝你玩得愉快！" : "交易完成！";
        }

        Trader.SendMessageAsync(message).ConfigureAwait(false);

        // For single trades only, return the Pokemon immediately
        // Batch trades will have their Pokemon returned separately via SendNotification
        if (result is not null && Hub.Config.Discord.ReturnPKMs && TotalBatchTrades <= 1)
        {
            Trader.SendPKMAsync(result, "这是你刚才交换的宝可梦！").ConfigureAwait(false);
        }
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
    {
        // Add batch context to notifications if applicable
        if (TotalBatchTrades > 1 && !message.Contains("交易") && !message.Contains("batch"))
        {
            message = $"第 {BatchTradeNumber}/{TotalBatchTrades} 次交易：{message}";
        }

        EmbedHelper.SendNotificationEmbedAsync(Trader, message).ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
    {
        if (message.ExtraInfo is SeedSearchResult r)
        {
            SendNotificationZ3(r);
            return;
        }

        var msg = message.Summary;
        if (message.Details.Count > 0)
            msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));
        Trader.SendMessageAsync(msg).ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
    {
        // Always send the Pokemon if requested, regardless of trade type
        if (result.Species != 0 && (Hub.Config.Discord.ReturnPKMs || info.Type == PokeTradeType.Dump))
        {
            Trader.SendPKMAsync(result, message).ConfigureAwait(false);
        }
    }

    private void SendNotificationZ3(SeedSearchResult r)
    {
        var lines = r.ToString();

        var embed = new EmbedBuilder { Color = Color.LighterGrey };
        embed.AddField(x =>
        {
            x.Name = $"种子：{r.Seed:X16}";
            x.Value = lines;
            x.IsInline = false;
        });
        var msg = $"以下是 `{r.Seed:X16}` 的详细信息：";
        Trader.SendMessageAsync(msg, embed: embed.Build()).ConfigureAwait(false);
    }

    public static (string, Embed) CreateLGLinkCodeSpriteEmbed(List<Pictocodes> lgcode)
    {
        int codecount = 0;
        List<System.Drawing.Image> spritearray = [];
        foreach (Pictocodes cd in lgcode)
        {
            var showdown = new ShowdownSet(cd.ToString());
            var sav = BlankSaveFile.Get(EntityContext.Gen7b, "pip");
            PKM pk = sav.GetLegalFromSet(showdown).Created;
            System.Drawing.Image png = pk.Sprite();
            var destRect = new Rectangle(-40, -65, 137, 130);
            var destImage = new Bitmap(137, 130);
            destImage.SetResolution(png.HorizontalResolution, png.VerticalResolution);
            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(png, destRect, 0, 0, png.Width, png.Height, GraphicsUnit.Pixel);
            }
            png = destImage;
            spritearray.Add(png);
            codecount++;
        }
        int outputImageWidth = spritearray[0].Width + 20;

        int outputImageHeight = spritearray[0].Height - 65;

        Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(outputImage))
        {
            graphics.DrawImage(spritearray[0], new Rectangle(0, 0, spritearray[0].Width, spritearray[0].Height),
                new Rectangle(new Point(), spritearray[0].Size), GraphicsUnit.Pixel);
            graphics.DrawImage(spritearray[1], new Rectangle(50, 0, spritearray[1].Width, spritearray[1].Height),
                new Rectangle(new Point(), spritearray[1].Size), GraphicsUnit.Pixel);
            graphics.DrawImage(spritearray[2], new Rectangle(100, 0, spritearray[2].Width, spritearray[2].Height),
                new Rectangle(new Point(), spritearray[2].Size), GraphicsUnit.Pixel);
        }
        System.Drawing.Image finalembedpic = outputImage;
        var filename = $"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png";
        finalembedpic.Save(filename);
        filename = System.IO.Path.GetFileName($"{System.IO.Directory.GetCurrentDirectory()}//finalcode.png");
        Embed returnembed = new EmbedBuilder().WithTitle($"{lgcode[0]}, {lgcode[1]}, {lgcode[2]}").WithImageUrl($"attachment://{filename}").Build();
        return (filename, returnembed);
    }

    public void Dispose()
    {
        StopPeriodicUpdates();
        GC.SuppressFinalize(this);
    }

    ~DiscordTradeNotifier()
    {
        Dispose();
    }
}
