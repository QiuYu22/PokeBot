using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using SysBot.Base.Util;
using SysBot.Pokemon.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSV;
using static SysBot.Pokemon.TradeHub.SpecialRequests;

namespace SysBot.Pokemon;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class PokeTradeBotSV(PokeTradeHub<PK9> Hub, PokeBotState Config) : PokeRoutineExecutor9SV(Config), ICountBot, ITradeBot
{
    public readonly TradeAbuseSettings AbuseSettings = Hub.Config.TradeAbuse;

    /// <summary>
    /// Folder to dump received trade data to.
    /// </summary>
    /// <remarks>If null, will skip dumping.</remarks>
    private readonly FolderSettings DumpSetting = Hub.Config.Folder;

    private readonly TradeSettings TradeSettings = Hub.Config.Trade;

    // Cached offsets that stay the same per session.
    private ulong BoxStartOffset;

    private ulong ConnectedOffset;

    private uint DisplaySID;

    private uint DisplayTID;

    // Track the last Pokémon we were offered since it persists between trades.
    private byte[] lastOffered = new byte[8];

    // Stores whether the last trade was Distribution with fixed code, in which case we don't need to re-enter the code.
#pragma warning disable CS0414 // Field is assigned but its value is never used - Reserved for future implementation
    private bool LastTradeDistributionFixed;
#pragma warning restore CS0414

    // Store the current save's OT and TID/SID for comparison.
    private string OT = string.Empty;

    private ulong OverworldOffset;

    private ulong PortalOffset;

    // Stores whether we returned all the way to the overworld, which repositions the cursor.
    private bool StartFromOverworld = true;

    private ulong TradePartnerNIDOffset;

    private ulong TradePartnerOfferedOffset;

    public event EventHandler<Exception>? ConnectionError;

    public event EventHandler? ConnectionSuccess;

    public ICountSettings Counts => TradeSettings;

    /// <summary>
    /// Tracks failed synchronized starts to attempt to re-sync.
    /// </summary>
    public int FailedBarrier { get; private set; }

    /// <summary>
    /// Synchronized start for multiple bots.
    /// </summary>
    public bool ShouldWaitAtBarrier { get; private set; }

    public override Task HardStop()
    {
        UpdateBarrier(false);
        return CleanExit(CancellationToken.None);
    }

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            Hub.Queues.Info.CleanStuckTrades();
            await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

            Log("正在识别主机的训练家数据。");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            OT = sav.OT;
            DisplaySID = sav.DisplaySID;
            DisplayTID = sav.DisplayTID;
            RecentTrainerCache.SetRecentTrainer(sav);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
            OnConnectionSuccess();

            // Force the bot to go through all the motions again on its first pass.
            StartFromOverworld = true;
            LastTradeDistributionFixed = false;

            Log($"开始主 {nameof(PokeTradeBotSV)} 循环。");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            OnConnectionError(e);
            throw;
        }

        Log($"结束 {nameof(PokeTradeBotSV)} 循环。");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        Hub.Queues.Info.CleanStuckTrades();
        await Task.Delay(2_000, t).ConfigureAwait(false);
        await ReOpenGame(Hub.Config, t).ConfigureAwait(false);
        await HardStop().ConfigureAwait(false);
        await Task.Delay(2_000, t).ConfigureAwait(false);
        if (!t.IsCancellationRequested)
        {
            Log("正在重启主循环。");
            await MainLoop(t).ConfigureAwait(false);
        }
    }

    protected virtual async Task<(PK9 toSend, PokeTradeResult check)> GetEntityToSend(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, byte[] oldEC, PK9 toSend, PartnerDataHolder partnerID, SpecialTradeType? stt, CancellationToken token)
    {
        return poke.Type switch
        {
            PokeTradeType.Random => await HandleRandomLedy(sav, poke, offered, toSend, partnerID, token).ConfigureAwait(false),
            PokeTradeType.Clone => await HandleClone(sav, poke, offered, oldEC, token).ConfigureAwait(false),
            PokeTradeType.FixOT => await HandleFixOT(sav, poke, offered, partnerID, token).ConfigureAwait(false),
            PokeTradeType.Seed when stt is not SpecialTradeType.WonderCard => await HandleClone(sav, poke, offered, oldEC, token).ConfigureAwait(false),
            PokeTradeType.Seed when stt is SpecialTradeType.WonderCard => await JustInject(sav, offered, token).ConfigureAwait(false),
            _ => (toSend, PokeTradeResult.Success),
        };
    }

    protected virtual (PokeTradeDetail<PK9>? detail, uint priority) GetTradeData(PokeRoutineType type)
    {
        string botName = Connection.Name;

        // First check the specific type's queue
        if (Hub.Queues.TryDequeue(type, out var detail, out var priority, botName))
        {
            return (detail, priority);
        }

        // If we're doing FlexTrade, also check the Batch queue
        if (type == PokeRoutineType.FlexTrade)
        {
            if (Hub.Queues.TryDequeue(PokeRoutineType.Batch, out detail, out priority, botName))
            {
                return (detail, priority);
            }
        }

        if (Hub.Queues.TryDequeueLedy(out detail))
        {
            return (detail, PokeTradePriorities.TierFree);
        }
        return (null, PokeTradePriorities.TierFree);
    }

    // Upon connecting, their Nintendo ID will instantly update.
    protected virtual async Task<bool> WaitForTradePartner(CancellationToken token)
    {
        Log("正在等待训练家...");
        int ctr = (Hub.Config.Trade.TradeConfiguration.TradeWaitTime * 1_000) - 2_000;
        await Task.Delay(2_000, token).ConfigureAwait(false);
        while (ctr > 0)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            ctr -= 1_000;
            var newNID = await GetTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);
            if (newNID != 0)
            {
                TradePartnerOfferedOffset = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerPokemonPointer, token).ConfigureAwait(false);
                return true;
            }

            // Fully load into the box.
            await Task.Delay(1_000, token).ConfigureAwait(false);
        }
        return false;
    }

    private async Task<PK9> ApplyAutoOT(PK9 toSend, TradeMyStatus tradePartner, SAV9SV sav, CancellationToken token)
    {
        if (toSend.Version == GameVersion.GO)
        {
            var goClone = toSend.Clone();
            goClone.OriginalTrainerName = tradePartner.OT;

            ClearOTTrash(goClone, tradePartner);

            if (!toSend.ChecksumValid)
                goClone.RefreshChecksum();

            Log("已将 OT 名称应用到来自 GO 的宝可梦。");
            await SetBoxPokemonAbsolute(BoxStartOffset, goClone, token, sav).ConfigureAwait(false);
            return goClone;
        }

        if (toSend is IHomeTrack pk && pk.HasTracker)
        {
            Log("检测到 Home 追踪器。无法应用 AutoOT。");
            return toSend;
        }

        if (toSend.Generation != toSend.Format)
        {
            Log("无法应用交易伙伴信息: 当前持有者不能是不同世代的 OT。");
            return toSend;
        }

        bool isMysteryGift = toSend.FatefulEncounter;
        var cln = toSend.Clone();

        if (isMysteryGift)
        {
            Log("检测到神秘礼物。仅应用 OT 信息，保留语言设置。");
            cln.OriginalTrainerGender = (byte)tradePartner.Gender;
            cln.TrainerTID7 = (uint)Math.Abs(tradePartner.DisplayTID);
            cln.TrainerSID7 = (uint)Math.Abs(tradePartner.DisplaySID);
            cln.OriginalTrainerName = tradePartner.OT;
        }
        else
        {
            cln.OriginalTrainerGender = (byte)tradePartner.Gender;
            cln.TrainerTID7 = (uint)Math.Abs(tradePartner.DisplayTID);
            cln.TrainerSID7 = (uint)Math.Abs(tradePartner.DisplaySID);
            cln.Language = tradePartner.Language;
            cln.OriginalTrainerName = tradePartner.OT;
        }

        ClearOTTrash(cln, tradePartner);

        ushort species = toSend.Species;
        GameVersion version;
        switch (species)
        {
            case (ushort)Species.Koraidon:
            case (ushort)Species.GougingFire:
            case (ushort)Species.RagingBolt:
                version = GameVersion.SL;
                Log("朱版独占宝可梦，将版本改为朱。");
                break;

            case (ushort)Species.Miraidon:
            case (ushort)Species.IronCrown:
            case (ushort)Species.IronBoulder:
                version = GameVersion.VL;
                Log("紫版独占宝可梦，将版本改为紫。");
                break;

            default:
                version = (GameVersion)tradePartner.Game;
                break;
        }
        cln.Version = version;

        if (!toSend.IsNicknamed)
            cln.ClearNickname();

        if (toSend.IsShiny)
            cln.PID = (uint)((cln.TID16 ^ cln.SID16 ^ (cln.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (cln.PID & 0xFFFF);

        if (!toSend.ChecksumValid)
            cln.RefreshChecksum();

        var tradeSV = new LegalityAnalysis(cln);
        if (tradeSV.Valid)
        {
            Log("宝可梦有效，使用交易伙伴信息 (AutoOT)。");
            await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            return cln;
        }
        else
        {
            Log("交易宝可梦无法应用 AutoOT。");
            return toSend;
        }
    }

    private static void ClearOTTrash(PK9 pokemon, TradeMyStatus tradePartner)
    {
        Span<byte> trash = pokemon.OriginalTrainerTrash;
        trash.Clear();
        string name = tradePartner.OT;
        int maxLength = trash.Length / 2;
        int actualLength = Math.Min(name.Length, maxLength);
        for (int i = 0; i < actualLength; i++)
        {
            char value = name[i];
            trash[i * 2] = (byte)value;
            trash[(i * 2) + 1] = (byte)(value >> 8);
        }
        if (actualLength < maxLength)
        {
            trash[actualLength * 2] = 0x00;
            trash[(actualLength * 2) + 1] = 0x00;
        }
    }

    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PK9> detail, CancellationToken token)
    {
        // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
        var oldEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);

        await Click(A, 3_000, token).ConfigureAwait(false);
        for (int i = 0; i < Hub.Config.Trade.TradeConfiguration.MaxTradeConfirmTime; i++)
        {
            // We can fall out of the box if the user offers, then quits.
            if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
                return PokeTradeResult.TrainerLeft;

            await Click(A, 1_000, token).ConfigureAwait(false);

            // EC is detectable at the start of the animation.
            var newEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);
            if (!newEC.SequenceEqual(oldEC))
            {
                // Check if partner offered a Pokemon that will evolve
                if (Hub.Config.Trade.TradeConfiguration.DisallowTradeEvolve)
                {
                    var offered = await ReadUntilPresent(TradePartnerOfferedOffset, 2_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
                    if (offered != null && TradeEvolutions.WillTradeEvolve(offered.Species, offered.Form, offered.HeldItem, detail.TradeData.Species))
                    {
                        Log("交易已取消，因为训练家提供的宝可梦会在交易后进化。");
                        detail.SendNotification(this, "交易已取消。您不能交易会进化的宝可梦。为防止这种情况，请给您的宝可梦持有不变之石，或换一只宝可梦交易。");
                        return PokeTradeResult.TradeEvolveNotAllowed;
                    }
                }

                await Task.Delay(25_000, token).ConfigureAwait(false);
                return PokeTradeResult.Success;
            }
        }

        // If we don't detect a B1S1 change, the trade didn't go through in that time.
        return PokeTradeResult.TrainerTooSlow;
    }

    // Should be used from the overworld. Opens X menu, attempts to connect online, and enters the Portal.
    // The cursor should be positioned over Link Trade.
    private async Task<bool> ConnectAndEnterPortal(CancellationToken token)
    {
        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            await RecoverToOverworld(token).ConfigureAwait(false);

        Log("正在打开宝可入口站。");

        // Open the X Menu.
        await Click(X, 1_000, token).ConfigureAwait(false);

        // Handle the news popping up.
        if (await SwitchConnection.IsProgramRunning(LibAppletWeID, token).ConfigureAwait(false))
        {
            Log("检测到新闻，加载完成后将关闭！");
            await Task.Delay(5_000, token).ConfigureAwait(false);
            await Click(B, 2_000, token).ConfigureAwait(false);
        }

        // Scroll to the bottom of the Main Menu, so we don't need to care if Picnic is unlocked.
        await Click(DRIGHT, 0_300, token).ConfigureAwait(false);
        await PressAndHold(DDOWN, 1_000, 1_000, token).ConfigureAwait(false);
        await Click(DUP, 0_200, token).ConfigureAwait(false);
        await Click(DUP, 0_200, token).ConfigureAwait(false);
        await Click(DUP, 0_200, token).ConfigureAwait(false);
        await Click(A, 1_000, token).ConfigureAwait(false);

        return await SetUpPortalCursor(token).ConfigureAwait(false);
    }

    private async Task<bool> ConnectToOnline(PokeTradeHubConfig config, CancellationToken token)
    {
        int attemptCount = 0;
        const int maxAttempt = 5;
        const int waitTime = 10; // time in minutes to wait after max attempts

        while (true) // Loop until a successful connection is made or the task is canceled
        {
            if (token.IsCancellationRequested)
            {
                Log("连接尝试已取消。");
                break;
            }
            try
            {
                if (await IsConnectedOnline(ConnectedOffset, token).ConfigureAwait(false))
                {
                    Log("连接建立成功。");
                    break; // Exit the loop if connected successfully
                }

                if (attemptCount >= maxAttempt)
                {
                    Log($"连接失败，已尝试 {maxAttempt} 次。假定为软封禁。将等待 {waitTime} 分钟后重试。");
                    // Waiting process
                    await Click(B, 0_500, token).ConfigureAwait(false);
                    await Click(B, 0_500, token).ConfigureAwait(false);
                    Log($"等待 {waitTime} 分钟后尝试重新连接。");
                    await Task.Delay(TimeSpan.FromMinutes(waitTime), token).ConfigureAwait(false);
                    Log("正在尝试重新打开游戏。");
                    await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                    attemptCount = 0; // Reset attempt count
                }

                attemptCount++;
                Log($"第 {attemptCount}/{maxAttempt} 次尝试: 正在连接网络...");

                // Connection attempt logic
                await Click(X, 3_000, token).ConfigureAwait(false);
                await Click(L, 5_000 + config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);

                // Wait a bit before rechecking the connection status
                await Task.Delay(5000, token).ConfigureAwait(false); // Wait 5 seconds before rechecking

                if (attemptCount < maxAttempt)
                {
                    Log("正在重新检查在线连接状态...");
                    // Wait and recheck logic
                    await Click(B, 0_500, token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log($"连接尝试期间发生异常: {ex.Message}");

                if (attemptCount >= maxAttempt)
                {
                    Log($"由于异常，连接失败，已尝试 {maxAttempt} 次。将等待 {waitTime} 分钟后重试。");
                    await Task.Delay(TimeSpan.FromMinutes(waitTime), token).ConfigureAwait(false);
                    Log("正在尝试重新打开游戏。");
                    await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                    attemptCount = 0;
                }
            }
        }

        // Final steps after connection is established
        await Task.Delay(3_000 + config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);

        return true;
    }

    private async Task DoNothing(CancellationToken token)
    {
        Log("无任务分配。等待新任务分配。");
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
            await Task.Delay(1_000, token).ConfigureAwait(false);
    }

    private async Task DoTrades(SAV9SV sav, CancellationToken token)
    {
        var type = Config.CurrentRoutineType;
        int waitCounter = 0;
        await SetCurrentBox(0, token).ConfigureAwait(false);
        while (!token.IsCancellationRequested && Config.NextRoutineType == type)
        {
            var (detail, priority) = GetTradeData(type);
            if (detail is null)
            {
                await WaitForQueueStep(waitCounter++, token).ConfigureAwait(false);
                continue;
            }
            waitCounter = 0;

            detail.IsProcessing = true;
            string tradetype = $" ({detail.Type})";
            Log($"开始下一个 {type}{tradetype} 机器人交易。正在获取数据...");
            Hub.Config.Stream.StartTrade(this, detail, Hub);
            Hub.Queues.StartTrade(this, detail);

            await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
        }
    }

    private async Task ExitTradeToPortal(bool unexpected, CancellationToken token)
    {
        await Task.Delay(1_000, token).ConfigureAwait(false);
        if (await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
            return;

        if (unexpected)
            Log("异常行为，正在恢复到宝可入口站。");

        // Ensure we're not in the box first.
        // Takes a long time for the Portal to load up, so once we exit the box, wait 5 seconds.
        Log("正在离开盒子...");
        var attempts = 0;
        while (await IsInBox(PortalOffset, token).ConfigureAwait(false))
        {
            await Click(B, 1_000, token).ConfigureAwait(false);
            if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                break;
            }

            await Click(A, 1_000, token).ConfigureAwait(false);
            if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                break;
            }

            await Click(B, 1_000, token).ConfigureAwait(false);
            if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                break;
            }

            // Didn't make it out of the box for some reason.
            if (++attempts > 20)
            {
                Log("退出盒子失败，正在重启游戏。");
                if (!await RecoverToOverworld(token).ConfigureAwait(false))
                    await RestartGameSV(token).ConfigureAwait(false);
                await ConnectAndEnterPortal(token).ConfigureAwait(false);
                return;
            }
        }

        // Wait for the portal to load.
        Log("等待宝可入口站加载...");
        attempts = 0;
        while (!await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            if (await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
                break;

            // Didn't make it into the portal for some reason.
            if (++attempts > 40)
            {
                Log("加载宝可入口站失败，正在重启游戏。");
                if (!await RecoverToOverworld(token).ConfigureAwait(false))
                    await RestartGameSV(token).ConfigureAwait(false);
                await ConnectAndEnterPortal(token).ConfigureAwait(false);
                return;
            }
        }
    }

    private async Task<TradeMyStatus> GetTradePartnerFullInfo(CancellationToken token)
    {
        // We're able to see both users' MyStatus, but one of them will be ourselves.
        var trader_info = await GetTradePartnerMyStatus(Offsets.Trader1MyStatusPointer, token).ConfigureAwait(false);
        if (trader_info.OT == OT && trader_info.DisplaySID == DisplaySID && trader_info.DisplayTID == DisplayTID) // This one matches ourselves.
            trader_info = await GetTradePartnerMyStatus(Offsets.Trader2MyStatusPointer, token).ConfigureAwait(false);
        return trader_info;
    }

    private void HandleAbortedTrade(PokeTradeDetail<PK9> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
    {
        // Skip processing if we've already handled the notification (e.g., NoTrainerFound)
        if (result == PokeTradeResult.NoTrainerFound)
            return;

        detail.IsProcessing = false;
        if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
        {
            detail.IsRetry = true;
            Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
            detail.SendNotification(this, "哎呀！出了点问题。我会把你重新排队再试一次。");
        }
        else
        {
            detail.SendNotification(this, $"哎呀！出了点问题。取消交易: {result}。");
            detail.TradeCanceled(this, result);
        }
    }

    private async Task<(PK9 toSend, PokeTradeResult check)> HandleClone(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, byte[] oldEC, CancellationToken token)
    {
        if (Hub.Config.Discord.ReturnPKMs)
            poke.SendNotification(this, offered, $"这是你展示给我的 - {GameInfo.GetStrings("zh").Species[offered.Species]}");

        var la = new LegalityAnalysis(offered);
        if (!la.Valid)
        {
            Log($"克隆请求（来自 {poke.Trainer.TrainerName}）检测到非法宝可梦: {GameInfo.GetStrings("zh").Species[offered.Species]}。");
            if (DumpSetting.Dump)
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

            var report = la.Report();
            Log(report);
            poke.SendNotification(this, "根据 PKHeX 的合法性检查，此宝可梦不合法。我被禁止克隆它。退出交易。");
            poke.SendNotification(this, report);

            return (offered, PokeTradeResult.IllegalTrade);
        }

        var clone = offered.Clone();
        if (Hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        poke.SendNotification(this, $"**已克隆你的 {GameInfo.GetStrings("zh").Species[clone.Species]}！**\n现在按 B 取消你的报价，然后交给我一只你不要的宝可梦。");
        Log($"已克隆 {GameInfo.GetStrings("zh").Species[clone.Species]}。等待用户更换宝可梦...");

        // Separate this out from WaitForPokemonChanged since we compare to old EC from original read.
        var partnerFound = await ReadUntilChanged(TradePartnerOfferedOffset, oldEC, 15_000, 0_200, false, true, token).ConfigureAwait(false);
        if (!partnerFound)
        {
            poke.SendNotification(this, "**嘿，立即更换，否则我要离开了！！！**");

            // They get one more chance.
            partnerFound = await ReadUntilChanged(TradePartnerOfferedOffset, oldEC, 15_000, 0_200, false, true, token).ConfigureAwait(false);
        }
        // Check if the user has cancelled the trade
        if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
        {
            Log("用户取消了交易。正在退出...");
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return (offered, PokeTradeResult.TrainerTooSlow);
        }
        var pk2 = await ReadUntilPresent(TradePartnerOfferedOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
        if (!partnerFound || pk2 is null || SearchUtil.HashByDetails(pk2) == SearchUtil.HashByDetails(offered))
        {
            Log("交易伙伴未更换宝可梦。");
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return (offered, PokeTradeResult.TrainerTooSlow);
        }

        await Click(A, 0_800, token).ConfigureAwait(false);
        await SetBoxPokemonAbsolute(BoxStartOffset, clone, token, sav).ConfigureAwait(false);

        return (clone, PokeTradeResult.Success);
    }

    private async Task<(PK9 toSend, PokeTradeResult check)> HandleFixOT(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, PartnerDataHolder partnerID, CancellationToken token)
    {
        if (Hub.Config.Discord.ReturnPKMs)
            poke.SendNotification(this, offered, $"这是你展示给我的 - {GameInfo.GetStrings("zh").Species[offered.Species]}");

        var adOT = TradeExtensions<PK9>.HasAdName(offered, out _);
        var laInit = new LegalityAnalysis(offered);
        if (!adOT && laInit.Valid)
        {
            poke.SendNotification(this, "在昵称或 OT 中未检测到广告，且宝可梦合法。退出交易。");
            return (offered, PokeTradeResult.TrainerRequestBad);
        }

        var clone = (PK9)offered.Clone();
        if (Hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        string shiny = string.Empty;
        if (!TradeExtensions<PK9>.ShinyLockCheck(offered.Species, TradeExtensions<PK9>.FormOutput(offered.Species, offered.Form, out _), $"{(Ball)offered.Ball}"))
            shiny = $"\nShiny: {(offered.ShinyXor == 0 ? "Square" : offered.IsShiny ? "Star" : "No")}";
        else shiny = "\nShiny: No";

        var name = partnerID.TrainerName;
        var ball = $"\n{(Ball)offered.Ball}";
        var extraInfo = $"OT: {name}{ball}{shiny}";
        var set = ShowdownParsing.GetShowdownText(offered).Split('\n').ToList();
        set.Remove(set.Find(x => x.Contains("Shiny")) ?? "");
        set.InsertRange(1, extraInfo.Split('\n'));

        if (!laInit.Valid)
        {
            Log($"FixOT 请求检测到来自 {name} 的非法宝可梦: {(Species)offered.Species}");
            var report = laInit.Report();
            Log(laInit.Report());
            poke.SendNotification(this, $"**展示的宝可梦不合法。正在尝试重新生成...**\n\n```{report}```");
            if (DumpSetting.Dump)
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
        }

        if (clone.FatefulEncounter)
        {
            clone.SetDefaultNickname(laInit);
            var info = new SimpleTrainerInfo { Gender = clone.OriginalTrainerGender, Language = clone.Language, OT = name, TID16 = clone.TID16, SID16 = clone.SID16, Generation = 9 };
            var mg = EncounterEvent.GetAllEvents().Where(x => x.Species == clone.Species && x.Form == clone.Form && x.IsShiny == clone.IsShiny && x.OriginalTrainerName == clone.OriginalTrainerName).ToList();
            if (mg.Count > 0)
                clone = TradeExtensions<PK9>.CherishHandler(mg.First(), info);
            else clone = (PK9)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }
        else
        {
            clone = (PK9)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }

        var la = new LegalityAnalysis(clone);
        clone = (PK9)TradeExtensions<PK9>.TrashBytes(clone, la);
        clone.ResetPartyStats();

        la = new LegalityAnalysis(clone);
        if (!la.Valid)
        {
            poke.SendNotification(this, "根据 PKHeX 的合法性检查，此宝可梦不合法。我无法修复它。退出交易。");
            return (clone, PokeTradeResult.IllegalTrade);
        }

        TradeExtensions<PK9>.HasAdName(offered, out string detectedAd);
        poke.SendNotification(this, $"{(!laInit.Valid ? "**已合法化" : "**已修复昵称/OT：")} {(Species)clone.Species}**（检测到广告: {detectedAd}）！现在请确认交易！");
        Log($"{(!laInit.Valid ? "已合法化" : "已修复昵称/OT：")} {(Species)clone.Species}！");

        // Wait for a bit in case trading partner tries to switch out.
        await Task.Delay(2_000, token).ConfigureAwait(false);

        var pk2 = await ReadUntilPresent(TradePartnerOfferedOffset, 15_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);
        bool changed = pk2 is null || pk2.Species != offered.Species || offered.OriginalTrainerName != pk2.OriginalTrainerName;
        if (changed)
        {
            // They get one more chance.
            poke.SendNotification(this, "**请提供最初展示的宝可梦，否则我要离开了！**");

            var timer = 10_000;
            while (changed)
            {
                pk2 = await ReadUntilPresent(TradePartnerOfferedOffset, 2_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
                changed = pk2 == null || clone.Species != pk2.Species || offered.OriginalTrainerName != pk2.OriginalTrainerName;
                await Task.Delay(1_000, token).ConfigureAwait(false);
                timer -= 1_000;

                if (timer <= 0)
                    break;
            }
        }

        if (changed)
        {
            poke.SendNotification(this, "宝可梦被替换且未换回。退出交易。");
            Log("交易伙伴不愿意送出他们的广告宝可梦。");
            return (offered, PokeTradeResult.TrainerTooSlow);
        }

        await Click(A, 0_800, token).ConfigureAwait(false);
        await SetBoxPokemonAbsolute(BoxStartOffset, clone, token, sav).ConfigureAwait(false);

        return (clone, PokeTradeResult.Success);
    }

    private async Task<(PK9 toSend, PokeTradeResult check)> HandleRandomLedy(SAV9SV sav, PokeTradeDetail<PK9> poke, PK9 offered, PK9 toSend, PartnerDataHolder partner, CancellationToken token)
    {
        // Allow the trade partner to do a Ledy swap.
        var config = Hub.Config.Distribution;
        var trade = Hub.Ledy.GetLedyTrade(offered, partner.TrainerOnlineID, config.LedySpecies);
        if (trade != null)
        {
            if (trade.Type == LedyResponseType.AbuseDetected)
            {
                var msg = $"检测到 {partner.TrainerName} 滥用 Ledy 交易。";
                if (AbuseSettings.EchoNintendoOnlineIDLedy)
                    msg += $"\nID: {partner.TrainerOnlineID}";
                if (!string.IsNullOrWhiteSpace(AbuseSettings.LedyAbuseEchoMention))
                    msg = $"{AbuseSettings.LedyAbuseEchoMention} {msg}";
                EchoUtil.Echo(msg);

                return (toSend, PokeTradeResult.SuspiciousActivity);
            }

            toSend = trade.Receive;
            poke.TradeData = toSend;

            poke.SendNotification(this, "正在注入请求的宝可梦。");
            await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
        }
        else if (config.LedyQuitIfNoMatch)
        {
            var nickname = offered.IsNicknamed ? $" (Nickname: \"{offered.Nickname}\")" : string.Empty;
            poke.SendNotification(this, $"未找到与所提供的 {GameInfo.GetStrings("zh").Species[offered.Species]}{nickname} 匹配的宝可梦。");
            return (toSend, PokeTradeResult.TrainerRequestBad);
        }

        return (toSend, PokeTradeResult.Success);
    }

    // These don't change per session and we access them frequently, so set these each time we start.
    private async Task InitializeSessionOffsets(CancellationToken token)
    {
        Log("正在缓存会话偏移量...");
        BoxStartOffset = await SwitchConnection.PointerAll(Offsets.BoxStartPokemonPointer, token).ConfigureAwait(false);
        OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
        PortalOffset = await SwitchConnection.PointerAll(Offsets.PortalBoxStatusPointer, token).ConfigureAwait(false);
        ConnectedOffset = await SwitchConnection.PointerAll(Offsets.IsConnectedPointer, token).ConfigureAwait(false);
        TradePartnerNIDOffset = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerNIDPointer, token).ConfigureAwait(false);
    }

    private async Task InnerLoop(SAV9SV sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Config.IterateNextRoutine();
            var task = Config.CurrentRoutineType switch
            {
                PokeRoutineType.Idle => DoNothing(token),
                _ => DoTrades(sav, token),
            };
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (SocketException e)
            {
                if (e.StackTrace != null)
                    Connection.LogError(e.StackTrace);
                var attempts = Hub.Config.Timings.ReconnectAttempts;
                var delay = Hub.Config.Timings.ExtraReconnectDelay;
                var protocol = Config.Connection.Protocol;
                if (!await TryReconnect(attempts, delay, protocol, token).ConfigureAwait(false))
                    return;
            }
        }
    }

    private async Task<(PK9 toSend, PokeTradeResult check)> JustInject(SAV9SV sav, PK9 offered, CancellationToken token)
    {
        await Click(A, 0_800, token).ConfigureAwait(false);
        await SetBoxPokemonAbsolute(BoxStartOffset, offered, token, sav).ConfigureAwait(false);

        for (int i = 0; i < 5; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);

        return (offered, PokeTradeResult.Success);
    }

    private void OnConnectionError(Exception ex)
    {
        ConnectionError?.Invoke(this, ex);
    }

    private void OnConnectionSuccess()
    {
        ConnectionSuccess?.Invoke(this, EventArgs.Empty);
    }

    private async Task<PokeTradeResult> PerformBatchTrade(SAV9SV sav, PokeTradeDetail<PK9> poke, CancellationToken token)
    {
        int completedTrades = 0;
        var startingDetail = poke;
        var originalTrainerID = startingDetail.Trainer.ID;

        var tradesToProcess = poke.BatchTrades ?? [poke.TradeData];
        var totalBatchTrades = tradesToProcess.Count;

        // Cache trade partner info after first successful connection
        TradeMyStatus? cachedTradePartnerInfo = null;

        void SendCollectedPokemonAndCleanup()
        {
            var allReceived = BatchTracker.GetReceivedPokemon(originalTrainerID);
            if (allReceived.Count > 0)
            {
                poke.SendNotification(this, $"正在发送中断前你交给我的 {allReceived.Count} 只宝可梦。");

                Log($"正在返还 {allReceived.Count} 只宝可梦给训练家 {originalTrainerID}。");

                // Send each Pokemon directly instead of calling TradeFinished
                for (int j = 0; j < allReceived.Count; j++)
                {
                    var pokemon = allReceived[j];
                    var speciesName = SpeciesName.GetSpeciesName(pokemon.Species, 2);
                    Log($"  - Returning: {speciesName} (Checksum: {pokemon.Checksum:X8})");

                    // Send the Pokemon directly to the notifier
                    poke.SendNotification(this, pokemon, $"你交给我的宝可梦: {speciesName}");
                    Thread.Sleep(500);
                }
            }
            else
            {
                Log($"未找到要返还给训练家 {originalTrainerID} 的宝可梦。");
            }

            BatchTracker.ClearReceivedPokemon(originalTrainerID);
            BatchTracker.ReleaseBatch(originalTrainerID, startingDetail.UniqueTradeID);
            poke.IsProcessing = false;
            Hub.Queues.Info.Remove(new TradeEntry<PK9>(poke, originalTrainerID, PokeRoutineType.Batch, poke.Trainer.TrainerName, poke.UniqueTradeID));
        }

        for (int i = 0; i < totalBatchTrades; i++)
        {
            var currentTradeIndex = i;
            var toSend = tradesToProcess[currentTradeIndex];

            poke.TradeData = toSend;
            poke.Notifier.UpdateBatchProgress(currentTradeIndex + 1, toSend, poke.UniqueTradeID);

            if (currentTradeIndex == 0)
            {
                // First trade - prepare Pokemon before searching for partner
                if (toSend.Species != 0)
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
            }
            else
            {
                // Subsequent trades - we're already in the trade screen
                // FIRST: Prepare the Pokemon BEFORE allowing user to offer
                poke.SendNotification(this, $"交易 {completedTrades} 完成！**请先不要报价** - 正在准备下一只宝可梦（{completedTrades + 1}/{totalBatchTrades}）...");

                // Wait for trade animation to fully complete
                await Task.Delay(5_000, token).ConfigureAwait(false);

                // Prepare the next Pokemon with AutoOT if needed
                if (toSend.Species != 0)
                {
                    if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT && cachedTradePartnerInfo != null)
                    {
                        toSend = await ApplyAutoOT(toSend, cachedTradePartnerInfo, sav, token);
                        tradesToProcess[currentTradeIndex] = toSend; // Update the list
                    }
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                }

                // Give time for the Pokemon to be properly set
                await Task.Delay(1_000, token).ConfigureAwait(false);

                // NOW tell the user they can offer
                poke.SendNotification(this, $"**准备好了！** 你现在可以报价交易 {currentTradeIndex + 1}/{totalBatchTrades} 的宝可梦了。");

                // Store the last offered state before allowing new offers
                lastOffered = await SwitchConnection.ReadBytesAbsoluteAsync(TradePartnerOfferedOffset, 8, token).ConfigureAwait(false);

                // Additional delay to ensure we're ready to detect offers
                await Task.Delay(5_000, token).ConfigureAwait(false);
            }

            // For first trade only - search for partner
            if (currentTradeIndex == 0)
            {
                await Click(A, 0_500, token).ConfigureAwait(false);
                await Click(A, 0_500, token).ConfigureAwait(false);

                await ClearTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);

                WaitAtBarrierIfApplicable(token);
                await Click(A, 1_000, token).ConfigureAwait(false);

                poke.TradeSearching(this);
                var partnerFound = await WaitForTradePartner(token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                {
                    StartFromOverworld = true;
                    LastTradeDistributionFixed = false;
                    await ExitTradeToPortal(false, token).ConfigureAwait(false);
                    poke.SendNotification(this, "正在取消批量交易。程序已被中断。");
                    SendCollectedPokemonAndCleanup();
                    return PokeTradeResult.RoutineCancel;
                }

                if (!partnerFound)
                {
                    poke.IsProcessing = false;
                    poke.SendNotification(this, "未找到交易伙伴。正在取消批量交易。");
                    poke.TradeCanceled(this, PokeTradeResult.NoTrainerFound);
                    SendCollectedPokemonAndCleanup();

                    if (!await RecoverToPortal(token).ConfigureAwait(false))
                    {
                        Log("恢复到宝可入口站失败。");
                        await RecoverToOverworld(token).ConfigureAwait(false);
                    }
                    return PokeTradeResult.NoTrainerFound;
                }

                Hub.Config.Stream.EndEnterCode(this);

                var cnt = 0;
                while (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
                {
                    await Task.Delay(0_500, token).ConfigureAwait(false);
                    if (++cnt > 20)
                    {
                        await Click(A, 1_000, token).ConfigureAwait(false);
                        SendCollectedPokemonAndCleanup();

                        if (!await RecoverToPortal(token).ConfigureAwait(false))
                        {
                            Log("恢复到宝可入口站失败。");
                            await RecoverToOverworld(token).ConfigureAwait(false);
                        }
                        poke.SendNotification(this, "进入交易箱失败。正在取消批量交易。");
                        return PokeTradeResult.RecoverOpenBox;
                    }
                }
                await Task.Delay(3_000 + Hub.Config.Timings.ExtraTimeOpenBox, token).ConfigureAwait(false);

                // Get trade partner info and verify
                var tradePartnerFullInfo = await GetTradePartnerFullInfo(token).ConfigureAwait(false);
                cachedTradePartnerInfo = tradePartnerFullInfo; // Cache for subsequent trades
                var tradePartner = new TradePartnerSV(tradePartnerFullInfo);
                var trainerNID = await GetTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);
                RecordUtil<PokeTradeBotSV>.Record($"Initiating\t{trainerNID:X16}\t{tradePartner.TrainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");

                var tradeCodeStorage = new TradeCodeStorage();
                var existingTradeDetails = tradeCodeStorage.GetTradeDetails(poke.Trainer.ID);

                bool shouldUpdateOT = existingTradeDetails?.OT != tradePartner.TrainerName;
                bool shouldUpdateTID = existingTradeDetails?.TID != int.Parse(tradePartner.TID7);
                bool shouldUpdateSID = existingTradeDetails?.SID != int.Parse(tradePartner.SID7);

                if (shouldUpdateOT || shouldUpdateTID || shouldUpdateSID)
                {
                    string? ot = shouldUpdateOT ? tradePartner.TrainerName : existingTradeDetails?.OT;
                    int? tid = shouldUpdateTID ? int.Parse(tradePartner.TID7) : existingTradeDetails?.TID;
                    int? sid = shouldUpdateSID ? int.Parse(tradePartner.SID7) : existingTradeDetails?.SID;

                    if (ot != null && tid.HasValue && sid.HasValue)
                    {
                        tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, ot, tid.Value, sid.Value);
                    }
                    else
                    {
                        Log("OT、TID 或 SID 为空。跳过 UpdateTradeDetails。");
                    }
                }

                var partnerCheck = CheckPartnerReputation(this, poke, trainerNID, tradePartner.TrainerName, AbuseSettings, token);
                if (partnerCheck != PokeTradeResult.Success)
                {
                    poke.SendNotification(this, "交易伙伴验证失败。正在取消批量交易。");
                    SendCollectedPokemonAndCleanup();
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    await ExitTradeToPortal(false, token).ConfigureAwait(false);
                    return partnerCheck;
                }

                var tradeOffered = await ReadUntilChanged(TradePartnerOfferedOffset, lastOffered, 10_000, 0_500, false, true, token).ConfigureAwait(false);
                if (!tradeOffered)
                {
                    poke.SendNotification(this, "交易伙伴响应太慢。正在取消批量交易。");
                    SendCollectedPokemonAndCleanup();
                    await ExitTradeToPortal(false, token).ConfigureAwait(false);
                    return PokeTradeResult.TrainerTooSlow;
                }

                Log($"找到连接交易伙伴: {tradePartner.TrainerName}-{tradePartner.TID7} (ID: {trainerNID})");
                poke.SendNotification(this, $"找到连接交易伙伴: {tradePartner.TrainerName}。**TID**: {tradePartner.TID7} **SID**: {tradePartner.SID7}");

                // Apply AutoOT for first trade if needed
                if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
                {
                    toSend = await ApplyAutoOT(toSend, tradePartnerFullInfo, sav, token).ConfigureAwait(false);
                    poke.TradeData = toSend;
                    if (toSend.Species != 0)
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                }
            }

            // Wait for user to offer a Pokemon
            if (currentTradeIndex == 0)
            {
                poke.SendNotification(this, $"请提供你的宝可梦进行交易 1/{totalBatchTrades}。");
            }

            var offered = await ReadUntilPresent(TradePartnerOfferedOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
            var oldEC = await SwitchConnection.ReadBytesAbsoluteAsync(TradePartnerOfferedOffset, 8, token).ConfigureAwait(false);
            if (offered == null || offered.Species == 0 || !offered.ChecksumValid)
            {
                Log("交易结束，因为未提供有效的宝可梦。");
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 提供的宝可梦无效。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            // Get trade partner info for subsequent trades
            var trainer = new PartnerDataHolder(0, "", "");
            if (cachedTradePartnerInfo != null)
            {
                var tradePartner = new TradePartnerSV(cachedTradePartnerInfo);
                trainer = new PartnerDataHolder(0, tradePartner.TrainerName, tradePartner.TID7);
            }

            PokeTradeResult update;
            (toSend, update) = await GetEntityToSend(sav, poke, offered, oldEC, toSend, trainer, null, token).ConfigureAwait(false);
            if (update != PokeTradeResult.Success)
            {
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 的更新检查失败。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return update;
            }

            Log($"正在确认交易 {currentTradeIndex + 1}/{totalBatchTrades}。");
            var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
            if (tradeResult != PokeTradeResult.Success)
            {
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 的确认失败。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return tradeResult;
            }

            if (token.IsCancellationRequested)
            {
                StartFromOverworld = true;
                LastTradeDistributionFixed = false;
                poke.SendNotification(this, "正在取消批量交易。程序已被中断。");
                SendCollectedPokemonAndCleanup();
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return PokeTradeResult.RoutineCancel;
            }

            var received = await ReadPokemon(BoxStartOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
            {
                poke.SendNotification(this, $"伙伴未完成交易 {currentTradeIndex + 1}/{totalBatchTrades}。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            UpdateCountsAndExport(poke, received, toSend);

            // Get the trainer NID for logging
            var logTrainerNID = currentTradeIndex == 0 ? await GetTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false) : 0;
            LogSuccessfulTrades(poke, logTrainerNID, trainer.TrainerName);

            BatchTracker.AddReceivedPokemon(originalTrainerID, received);
            completedTrades = currentTradeIndex + 1;
            Log($"已将收到的宝可梦 {received.Species} (校验和: {received.Checksum:X8}) 添加到训练家 {originalTrainerID} 的批量追踪器 (交易 {completedTrades}/{totalBatchTrades})");

            if (completedTrades == totalBatchTrades)
            {
                // Get all collected Pokemon before cleaning anything up
                var allReceived = BatchTracker.GetReceivedPokemon(originalTrainerID);
                Log($"批量交易完成。发现为训练家 {originalTrainerID} 存储了 {allReceived.Count} 只宝可梦");

                // First send notification that trades are complete
                poke.SendNotification(this, "所有批量交易已完成！感谢你的交易！");

                // Send back all received Pokemon if ReturnPKMs is enabled
                if (Hub.Config.Discord.ReturnPKMs && allReceived.Count > 0)
                {
                    poke.SendNotification(this, $"这是你交给我的 {allReceived.Count} 只宝可梦:");

                    // Send each Pokemon directly instead of calling TradeFinished
                    for (int j = 0; j < allReceived.Count; j++)
                    {
                        var pokemon = allReceived[j];
                        var speciesName = SpeciesName.GetSpeciesName(pokemon.Species, 2);
                        Log($"  - Returning: {speciesName} (Checksum: {pokemon.Checksum:X8})");

                        // Send the Pokemon directly to the notifier
                        poke.SendNotification(this, pokemon, $"你交给我的宝可梦: {speciesName}");
                        await Task.Delay(500, token).ConfigureAwait(false);
                    }
                }

                // Now call TradeFinished ONCE for the entire batch with the last received Pokemon
                // This signals that the entire batch trade transaction is complete
                if (allReceived.Count > 0)
                {
                    poke.TradeFinished(this, allReceived[^1]);
                }
                else
                {
                    poke.TradeFinished(this, received);
                }

                // Mark the batch as fully completed and clean up
                Hub.Queues.CompleteTrade(this, startingDetail);
                BatchTracker.ClearReceivedPokemon(originalTrainerID);

                // Exit the trade state to prevent further searching
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
                poke.IsProcessing = false;
                break;
            }

            // Store the last offered Pokemon before moving to next trade
            lastOffered = await SwitchConnection.ReadBytesAbsoluteAsync(TradePartnerOfferedOffset, 8, token).ConfigureAwait(false);
        }

        // Ensure we exit properly even if the loop breaks unexpectedly
        await ExitTradeToPortal(false, token).ConfigureAwait(false);
        poke.IsProcessing = false;
        return PokeTradeResult.Success;
    }

    private async Task PerformTrade(SAV9SV sav, PokeTradeDetail<PK9> detail, PokeRoutineType type, uint priority, CancellationToken token)
    {
        PokeTradeResult result;
        try
        {
            // All trades go through PerformLinkCodeTrade which will handle both regular and batch trades
            result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);

            if (result != PokeTradeResult.Success)
            {
                if (detail.Type == PokeTradeType.Batch)
                    await HandleAbortedBatchTrade(detail, type, priority, result, token).ConfigureAwait(false);
                else
                    HandleAbortedTrade(detail, type, priority, result);
            }
        }
        catch (SocketException socket)
        {
            Log(socket.Message);
            result = PokeTradeResult.ExceptionConnection;
            if (detail.Type == PokeTradeType.Batch)
                await HandleAbortedBatchTrade(detail, type, priority, result, token).ConfigureAwait(false);
            else
                HandleAbortedTrade(detail, type, priority, result);
            throw;
        }
        catch (Exception e)
        {
            Log(e.Message);
            result = PokeTradeResult.ExceptionInternal;
            if (detail.Type == PokeTradeType.Batch)
                await HandleAbortedBatchTrade(detail, type, priority, result, token).ConfigureAwait(false);
            else
                HandleAbortedTrade(detail, type, priority, result);
        }
    }

    private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV9SV sav, PokeTradeDetail<PK9> poke, CancellationToken token)
    {
        // Check if trade was canceled by user
        if (poke.IsCanceled)
        {
            Log($"用户已取消 {poke.Trainer.TrainerName} 的交易。");
            poke.TradeCanceled(this, PokeTradeResult.UserCanceled);
            return PokeTradeResult.UserCanceled;
        }

        // Update Barrier Settings
        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        Hub.Config.Stream.EndEnterCode(this);

        // Handle connection and portal entry
        if (!await EnsureConnectedAndInPortal(token).ConfigureAwait(false))
        {
            return PokeTradeResult.RecoverStart;
        }

        // Enter Link Trade and code
        if (!await EnterLinkTradeAndCode(poke.Code, token).ConfigureAwait(false))
        {
            return PokeTradeResult.RecoverStart;
        }

        StartFromOverworld = false;

        // Route to appropriate trade handling based on trade type
        if (poke.Type == PokeTradeType.Batch)
            return await PerformBatchTrade(sav, poke, token).ConfigureAwait(false);

        return await PerformNonBatchTrade(sav, poke, token).ConfigureAwait(false);
    }

    private async Task<bool> EnsureConnectedAndInPortal(CancellationToken token)
    {
        // StartFromOverworld can be true on first pass or if something went wrong last trade.
        if (StartFromOverworld && !await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            await RecoverToOverworld(token).ConfigureAwait(false);

        if (!StartFromOverworld && !await IsConnectedOnline(ConnectedOffset, token).ConfigureAwait(false))
        {
            await RecoverToOverworld(token).ConfigureAwait(false);
            if (!await ConnectAndEnterPortal(token).ConfigureAwait(false))
            {
                await RecoverToOverworld(token).ConfigureAwait(false);
                return false;
            }
        }
        else if (StartFromOverworld && !await ConnectAndEnterPortal(token).ConfigureAwait(false))
        {
            await RecoverToOverworld(token).ConfigureAwait(false);
            return false;
        }
        return true;
    }

    private async Task<bool> EnterLinkTradeAndCode(int code, CancellationToken token)
    {
        // Assumes we're freshly in the Portal and the cursor is over Link Trade.
        Log("正在选择连接交易。");
        await Click(A, 1_500, token).ConfigureAwait(false);

        // Always clear Link Codes and enter a new one based on the current trade type
        await Click(X, 1_000, token).ConfigureAwait(false);
        await Click(PLUS, 1_000, token).ConfigureAwait(false);
        await Task.Delay(Hub.Config.Timings.ExtraTimeOpenCodeEntry, token).ConfigureAwait(false);

        Log($"正在输入连接交易密码: {code:0000 0000}...");
        await EnterLinkCode(code, Hub.Config, token).ConfigureAwait(false);
        await Click(PLUS, 3_000, token).ConfigureAwait(false);

        return true;
    }

    private async Task<PokeTradeResult> PerformNonBatchTrade(SAV9SV sav, PokeTradeDetail<PK9> poke, CancellationToken token)
    {
        var toSend = poke.TradeData;
        if (toSend.Species != 0)
            await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);

        // Search for a trade partner for a Link Trade.
        await Click(A, 0_500, token).ConfigureAwait(false);
        await Click(A, 0_500, token).ConfigureAwait(false);

        // Clear it so we can detect it loading.
        await ClearTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);

        // Wait for Barrier to trigger all bots simultaneously.
        WaitAtBarrierIfApplicable(token);
        await Click(A, 1_000, token).ConfigureAwait(false);

        // Wait for a Trainer...
        poke.TradeSearching(this);
        var partnerFound = await WaitForTradePartner(token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
        {
            StartFromOverworld = true;
            LastTradeDistributionFixed = false;
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return PokeTradeResult.RoutineCancel;
        }
        if (!partnerFound)
        {
            // Fast-path for no trainer found - handle immediately
            poke.IsProcessing = false;
            poke.SendNotification(this, "未找到交易伙伴。正在取消交易。");
            poke.TradeCanceled(this, PokeTradeResult.NoTrainerFound);

            if (!await RecoverToPortal(token).ConfigureAwait(false))
            {
                Log("恢复到宝可入口站失败。");
                await RecoverToOverworld(token).ConfigureAwait(false);
            }
            return PokeTradeResult.NoTrainerFound;
        }

        Hub.Config.Stream.EndEnterCode(this);

        // Wait until we get into the box.
        var cnt = 0;
        while (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
        {
            await Task.Delay(0_500, token).ConfigureAwait(false);
            if (++cnt > 20) // Didn't make it in after 10 seconds.
            {
                await Click(A, 1_000, token).ConfigureAwait(false); // Ensures we dismiss a popup.
                if (!await RecoverToPortal(token).ConfigureAwait(false))
                {
                    Log("恢复到宝可入口站失败。");
                    await RecoverToOverworld(token).ConfigureAwait(false);
                }
                return PokeTradeResult.RecoverOpenBox;
            }
        }
        await Task.Delay(3_000 + Hub.Config.Timings.ExtraTimeOpenBox, token).ConfigureAwait(false);

        var tradePartnerFullInfo = await GetTradePartnerFullInfo(token).ConfigureAwait(false);
        var tradePartner = new TradePartnerSV(tradePartnerFullInfo);
        var trainerNID = await GetTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);
        RecordUtil<PokeTradeBotSV>.Record($"Initiating\t{trainerNID:X16}\t{tradePartner.TrainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");
        Log($"找到连接交易伙伴: {tradePartner.TrainerName}-{tradePartner.TID7} (ID: {trainerNID})");
        poke.SendNotification(this, $"找到连接交易伙伴: {tradePartner.TrainerName}。TID: {tradePartner.TID7} SID: {tradePartner.SID7} 等待宝可梦...");

        var tradeCodeStorage = new TradeCodeStorage();
        var existingTradeDetails = tradeCodeStorage.GetTradeDetails(poke.Trainer.ID);

        bool shouldUpdateOT = existingTradeDetails?.OT != tradePartner.TrainerName;
        bool shouldUpdateTID = existingTradeDetails?.TID != int.Parse(tradePartner.TID7);
        bool shouldUpdateSID = existingTradeDetails?.SID != int.Parse(tradePartner.SID7);

        if (shouldUpdateOT || shouldUpdateTID || shouldUpdateSID)
        {
            string? ot = shouldUpdateOT ? tradePartner.TrainerName : existingTradeDetails?.OT;
            int? tid = shouldUpdateTID ? int.Parse(tradePartner.TID7) : existingTradeDetails?.TID;
            int? sid = shouldUpdateSID ? int.Parse(tradePartner.SID7) : existingTradeDetails?.SID;

            if (ot != null && tid.HasValue && sid.HasValue)
            {
                tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, ot, tid.Value, sid.Value);
            }
            else
            {
                Log("OT、TID 或 SID 为空。跳过 UpdateTradeDetails。");
            }
        }

        var partnerCheck = CheckPartnerReputation(this, poke, trainerNID, tradePartner.TrainerName, AbuseSettings, token);
        if (partnerCheck != PokeTradeResult.Success)
        {
            await Click(A, 1_000, token).ConfigureAwait(false); // Ensures we dismiss a popup.
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return partnerCheck;
        }

        // Hard check to verify that the offset changed from the last thing offered from the previous trade.
        // This is because box opening times can vary per person, the offset persists between trades, and can also change offset between trades.
        var tradeOffered = await ReadUntilChanged(TradePartnerOfferedOffset, lastOffered, 10_000, 0_500, false, true, token).ConfigureAwait(false);
        if (!tradeOffered)
        {
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        if (poke.Type == PokeTradeType.Dump)
        {
            var result = await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return result;
        }
        if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
        {
            toSend = await ApplyAutoOT(toSend, tradePartnerFullInfo, sav, token);
        }
        // Wait for user input...
        var offered = await ReadUntilPresent(TradePartnerOfferedOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
        var oldEC = await SwitchConnection.ReadBytesAbsoluteAsync(TradePartnerOfferedOffset, 8, token).ConfigureAwait(false);
        if (offered == null || offered.Species == 0 || !offered.ChecksumValid)
        {
            Log("交易结束，因为未提供有效的宝可梦。");
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        SpecialTradeType itemReq = SpecialTradeType.None;
        if (poke.Type == PokeTradeType.Seed)
            itemReq = CheckItemRequest(ref offered, this, poke, tradePartner.TrainerName, sav);
        if (itemReq == SpecialTradeType.FailReturn)
            return PokeTradeResult.IllegalTrade;

        if (poke.Type == PokeTradeType.Seed && itemReq == SpecialTradeType.None)
        {
            // Immediately exit, we aren't trading anything.
            poke.SendNotification(this, "没有持有道具或有效请求！取消此交易。");
            await ExitTradeToPortal(true, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerRequestBad;
        }

        var trainer = new PartnerDataHolder(0, tradePartner.TrainerName, tradePartner.TID7);
        PokeTradeResult update;
        (toSend, update) = await GetEntityToSend(sav, poke, offered, oldEC, toSend, trainer, poke.Type == PokeTradeType.Seed ? itemReq : null, token).ConfigureAwait(false);
        if (update != PokeTradeResult.Success)
        {
            if (itemReq != SpecialTradeType.None)
            {
                poke.SendNotification(this, "你的请求不合法。请尝试不同的宝可梦或请求。");
            }

            return update;
        }

        if (itemReq == SpecialTradeType.WonderCard)
            poke.SendNotification(this, "分发成功！");
        else if (itemReq != SpecialTradeType.None && itemReq != SpecialTradeType.Shinify)
            poke.SendNotification(this, "特殊请求成功！");
        else if (itemReq == SpecialTradeType.Shinify)
            poke.SendNotification(this, "闪光化成功！感谢你成为社区的一员！");

        // Check if the offered Pokemon will evolve upon trade BEFORE confirming
        if (Hub.Config.Trade.TradeConfiguration.DisallowTradeEvolve && TradeEvolutions.WillTradeEvolve(offered.Species, offered.Form, offered.HeldItem, toSend.Species))
        {
            Log("交易已取消，因为训练家提供的宝可梦会在交易后进化。");
            poke.SendNotification(this, "交易已取消。您不能交易会进化的宝可梦。为防止这种情况，请给您的宝可梦持有不变之石，或换一只宝可梦交易。");
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return PokeTradeResult.TradeEvolveNotAllowed;
        }

        Log("正在确认交易。");
        var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
        if (tradeResult != PokeTradeResult.Success)
        {
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return tradeResult;
        }

        if (token.IsCancellationRequested)
        {
            StartFromOverworld = true;
            LastTradeDistributionFixed = false;
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return PokeTradeResult.RoutineCancel;
        }

        // Trade was Successful!
        var received = await ReadPokemon(BoxStartOffset, BoxFormatSlotSize, token).ConfigureAwait(false);

        // Pokémon in b1s1 is same as the one they were supposed to receive (was never sent).
        if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
        {
            Log("用户未完成交易。");
            await ExitTradeToPortal(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        // As long as we got rid of our inject in b1s1, assume the trade went through.
        Log("用户已完成交易。");

        poke.TradeFinished(this, received);

        // Only log if we completed the trade.
        UpdateCountsAndExport(poke, received, toSend);

        // Log for Trade Abuse tracking.
        LogSuccessfulTrades(poke, trainerNID, tradePartner.TrainerName);

        // Sometimes they offered another mon, so store that immediately upon leaving Union Room.
        lastOffered = await SwitchConnection.ReadBytesAbsoluteAsync(TradePartnerOfferedOffset, 8, token).ConfigureAwait(false);

        await ExitTradeToPortal(false, token).ConfigureAwait(false);
        return PokeTradeResult.Success;
    }

    private async Task HandleAbortedBatchTrade(PokeTradeDetail<PK9> detail, PokeRoutineType type, uint priority, PokeTradeResult result, CancellationToken token)
    {
        detail.IsProcessing = false;

        // Always remove from UsersInQueue on abort
        Hub.Queues.Info.Remove(new TradeEntry<PK9>(detail, detail.Trainer.ID, type, detail.Trainer.TrainerName, detail.UniqueTradeID));

        if (detail.TotalBatchTrades > 1)
        {
            // Release the batch claim on failure
            BatchTracker.ReleaseBatch(detail.Trainer.ID, detail.UniqueTradeID);

            if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
            {
                detail.IsRetry = true;
                Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
                detail.SendNotification(this, "哎呀！你的批量交易过程中出了点问题。我会把你重新排队再试一次。");
            }
            else
            {
                detail.SendNotification(this, $"批量交易失败: {result}");
                detail.TradeCanceled(this, result);
                await ExitTradeToPortal(false, token).ConfigureAwait(false);
            }
        }
        else
        {
            HandleAbortedTrade(detail, type, priority, result);
        }
    }

    private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PK9> detail, CancellationToken token)
    {
        int ctr = 0;
        var time = TimeSpan.FromSeconds(Hub.Config.Trade.TradeConfiguration.MaxDumpTradeTime);
        var start = DateTime.Now;

        var pkprev = new PK9();
        var bctr = 0;
        while (ctr < Hub.Config.Trade.TradeConfiguration.MaxDumpsPerTrade && DateTime.Now - start < time)
        {
            if (!await IsInBox(PortalOffset, token).ConfigureAwait(false))
                break;
            if (bctr++ % 3 == 0)
                await Click(B, 0_100, token).ConfigureAwait(false);

            // Wait for user input... Needs to be different from the previously offered Pokémon.
            var pk = await ReadUntilPresent(TradePartnerOfferedOffset, 3_000, 0_050, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (pk == null || pk.Species == 0 || !pk.ChecksumValid || SearchUtil.HashByDetails(pk) == SearchUtil.HashByDetails(pkprev))
                continue;

            // Save the new Pokémon for comparison next round.
            pkprev = pk;

            // Send results from separate thread; the bot doesn't need to wait for things to be calculated.
            if (DumpSetting.Dump)
            {
                var subfolder = detail.Type.ToString().ToLower();
                DumpPokemon(DumpSetting.DumpFolder, subfolder, pk); // received
            }

            var la = new LegalityAnalysis(pk);
            var verbose = $"```{la.Report(true)}```";
            Log($"展示的宝可梦是: {(la.Valid ? "合法" : "非法")}。");

            ctr++;
            var msg = Hub.Config.Trade.TradeConfiguration.DumpTradeLegalityCheck ? verbose : $"文件 {ctr}";

            // Extra information about trainer data for people requesting with their own trainer data.
            var ot = pk.OriginalTrainerName;
            var ot_gender = pk.OriginalTrainerGender == 0 ? "男" : "女";
            var tid = pk.GetDisplayTID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringTID());
            var sid = pk.GetDisplaySID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringSID());
            msg += $"\n**Trainer Data**\n```OT: {ot}\nOTGender: {ot_gender}\nTID: {tid}\nSID: {sid}```";

            // Extra information for shiny eggs, because of people dumping to skip hatching.
            var eggstring = pk.IsEgg ? "蛋" : string.Empty;
            msg += pk.IsShiny ? $"\n**这只宝可梦{eggstring}是闪光的！**" : string.Empty;
            detail.SendNotification(this, pk, msg);
        }

        Log($"处理 {ctr} 只宝可梦后结束转储循环。");
        if (ctr == 0)
            return PokeTradeResult.TrainerTooSlow;

        TradeSettings.CountStatsSettings.AddCompletedDumps();
        detail.Notifier.SendNotification(this, detail, $"已转储 {ctr} 只宝可梦。");
        detail.Notifier.TradeFinished(this, detail, detail.TradeData); // blank PK9
        return PokeTradeResult.Success;
    }

    // If we can't manually recover to overworld, reset the game.
    // Try to avoid pressing A which can put us back in the portal with the long load time.
    private async Task<bool> RecoverToOverworld(CancellationToken token)
    {
        if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            return true;

        Log("正在尝试恢复到大地图。");
        var attempts = 0;
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            attempts++;
            if (attempts >= 30)
                break;

            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                break;

            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                break;

            if (await IsInBox(PortalOffset, token).ConfigureAwait(false))
                await Click(A, 1_000, token).ConfigureAwait(false);
        }

        // We didn't make it for some reason.
        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            Log("恢复到大地图失败，正在重启游戏。");
            await RestartGameSV(token).ConfigureAwait(false);
        }
        await Task.Delay(1_000, token).ConfigureAwait(false);

        // Force the bot to go through all the motions again on its first pass.
        StartFromOverworld = true;
        LastTradeDistributionFixed = false;
        return true;
    }

    // If we didn't find a trainer, we're still in the portal but there can be
    // different numbers of pop-ups we have to dismiss to get back to when we can trade.
    // Rather than resetting to overworld, try to reset out of portal and immediately go back in.
    private async Task<bool> RecoverToPortal(CancellationToken token)
    {
        Log("正在重新定位到宝可入口站。");
        var attempts = 0;
        while (await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
        {
            await Click(B, 2_500, token).ConfigureAwait(false);
            if (++attempts >= 30)
            {
                Log("恢复到宝可入口站失败。");
                return false;
            }
        }

        // Should be in the X menu hovered over Poké Portal.
        await Click(A, 1_000, token).ConfigureAwait(false);

        return await SetUpPortalCursor(token).ConfigureAwait(false);
    }

    private async Task RestartGameSV(CancellationToken token)
    {
        await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
        await InitializeSessionOffsets(token).ConfigureAwait(false);
    }

    // Waits for the Portal to load (slow) and then moves the cursor down to Link Trade.
    private async Task<bool> SetUpPortalCursor(CancellationToken token)
    {
        // Wait for the portal to load.
        var attempts = 0;
        while (!await IsInPokePortal(PortalOffset, token).ConfigureAwait(false))
        {
            await Task.Delay(0_500, token).ConfigureAwait(false);
            if (++attempts > 20)
            {
                Log("加载宝可入口站失败。");
                return false;
            }
        }
        await Task.Delay(2_000 + Hub.Config.Timings.ExtraTimeLoadPortal, token).ConfigureAwait(false);

        // Connect online if not already.
        if (!await ConnectToOnline(Hub.Config, token).ConfigureAwait(false))
        {
            Log("连接网络失败。");
            return false; // Failed, either due to connection or softban.
        }

        // Handle the news popping up.
        if (await SwitchConnection.IsProgramRunning(LibAppletWeID, token).ConfigureAwait(false))
        {
            Log("检测到新闻，加载完成后将关闭！");
            await Task.Delay(5_000, token).ConfigureAwait(false);
            await Click(B, 2_000 + Hub.Config.Timings.ExtraTimeLoadPortal, token).ConfigureAwait(false);
        }

        Log("正在调整宝可入口站中的光标。");

        // Move down to Link Trade.
        await Click(DDOWN, 0_300, token).ConfigureAwait(false);
        await Click(DDOWN, 0_300, token).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Checks if the barrier needs to get updated to consider this bot.
    /// If it should be considered, it adds it to the barrier if it is not already added.
    /// If it should not be considered, it removes it from the barrier if not already removed.
    /// </summary>
    private void UpdateBarrier(bool shouldWait)
    {
        if (ShouldWaitAtBarrier == shouldWait)
            return; // no change required

        ShouldWaitAtBarrier = shouldWait;
        if (shouldWait)
        {
            Hub.BotSync.Barrier.AddParticipant();
            Log($"已加入同步屏障。计数: {Hub.BotSync.Barrier.ParticipantCount}");
        }
        else
        {
            Hub.BotSync.Barrier.RemoveParticipant();
            Log($"已离开同步屏障。计数: {Hub.BotSync.Barrier.ParticipantCount}");
        }
    }

    private void UpdateCountsAndExport(PokeTradeDetail<PK9> poke, PK9 received, PK9 toSend)
    {
        var counts = TradeSettings;
        if (poke.Type == PokeTradeType.Random)
            counts.CountStatsSettings.AddCompletedDistribution();
        else if (poke.Type == PokeTradeType.Clone)
            counts.CountStatsSettings.AddCompletedClones();
        else if (poke.Type == PokeTradeType.FixOT)
            counts.CountStatsSettings.AddCompletedFixOTs();
        else
            counts.CountStatsSettings.AddCompletedTrade();

        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
        {
            var subfolder = poke.Type.ToString().ToLower();
            var service = poke.Notifier.GetType().ToString().ToLower();
            var tradedFolder = service.Contains("twitch") ? Path.Combine("traded", "twitch") : service.Contains("discord") ? Path.Combine("traded", "discord") : "traded";
            DumpPokemon(DumpSetting.DumpFolder, subfolder, received); // received by bot
            if (poke.Type is PokeTradeType.Specific or PokeTradeType.Clone)
                DumpPokemon(DumpSetting.DumpFolder, tradedFolder, toSend); // sent to partner
        }
    }

    private void WaitAtBarrierIfApplicable(CancellationToken token)
    {
        if (!ShouldWaitAtBarrier)
            return;
        var opt = Hub.Config.Distribution.SynchronizeBots;
        if (opt == BotSyncOption.NoSync)
            return;

        var timeoutAfter = Hub.Config.Distribution.SynchronizeTimeout;
        if (FailedBarrier == 1) // failed last iteration
            timeoutAfter *= 2; // try to re-sync in the event things are too slow.

        var result = Hub.BotSync.Barrier.SignalAndWait(TimeSpan.FromSeconds(timeoutAfter), token);

        if (result)
        {
            FailedBarrier = 0;
            return;
        }

        FailedBarrier++;
        Log($"同步屏障在 {timeoutAfter} 秒后超时。继续执行。");
    }

    private Task WaitForQueueStep(int waitCounter, CancellationToken token)
    {
        if (waitCounter == 0)
        {
            // Updates the assets.
            Hub.Config.Stream.IdleAssets(this);
            Log("无任务需要检查，等待新用户...");
        }

        return Task.Delay(1_000, token);
    }
}
