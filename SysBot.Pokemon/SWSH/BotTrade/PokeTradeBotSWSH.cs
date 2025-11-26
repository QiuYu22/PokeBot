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
using static SysBot.Pokemon.PokeDataOffsetsSWSH;
using static SysBot.Pokemon.TradeHub.SpecialRequests;

namespace SysBot.Pokemon;

public class PokeTradeBotSWSH(PokeTradeHub<PK8> hub, PokeBotState config) : PokeRoutineExecutor8SWSH(config), ICountBot, ITradeBot
{
    public static ISeedSearchHandler<PK8> SeedChecker { get; set; } = new NoSeedSearchHandler<PK8>();

    private readonly TradeSettings TradeSettings = hub.Config.Trade;

    private readonly TradeAbuseSettings AbuseSettings = hub.Config.TradeAbuse;

    public event EventHandler<Exception>? ConnectionError;

    public event EventHandler? ConnectionSuccess;

    private void OnConnectionError(Exception ex)
    {
        ConnectionError?.Invoke(this, ex);
    }

    private void OnConnectionSuccess()
    {
        ConnectionSuccess?.Invoke(this, EventArgs.Empty);
    }

    public ICountSettings Counts => TradeSettings;

    /// <summary>
    /// Folder to dump received trade data to.
    /// </summary>
    /// <remarks>If null, will skip dumping.</remarks>
    private readonly FolderSettings DumpSetting = hub.Config.Folder;

    /// <summary>
    /// Synchronized start for multiple bots.
    /// </summary>
    public bool ShouldWaitAtBarrier { get; private set; }

    /// <summary>
    /// Tracks failed synchronized starts to attempt to re-sync.
    /// </summary>
    public int FailedBarrier { get; private set; }

    // Cached offsets that stay the same per session.
    private ulong OverworldOffset;

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            await InitializeHardware(hub.Config.Trade, token).ConfigureAwait(false);

            Log("正在识别主机的训练家数据。");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            RecentTrainerCache.SetRecentTrainer(sav);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
            OnConnectionSuccess();
            Log($"开始主 {nameof(PokeTradeBotSWSH)} 循环。");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            OnConnectionError(e);
            throw;
        }

        Log($"结束 {nameof(PokeTradeBotSWSH)} 循环。");
        await HardStop().ConfigureAwait(false);
    }

    public override Task HardStop()
    {
        UpdateBarrier(false);
        return CleanExit(CancellationToken.None);
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        hub.Queues.Info.CleanStuckTrades();
        await Task.Delay(2_000, t).ConfigureAwait(false);
        await ReOpenGame(hub.Config, t).ConfigureAwait(false);
        await HardStop().ConfigureAwait(false);
        await Task.Delay(2_000, t).ConfigureAwait(false);
        if (!t.IsCancellationRequested)
        {
            Log("正在重启主循环。");
            await MainLoop(t).ConfigureAwait(false);
        }
    }

    private async Task InnerLoop(SAV8SWSH sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Config.IterateNextRoutine();
            var task = Config.CurrentRoutineType switch
            {
                PokeRoutineType.Idle => DoNothing(token),
                PokeRoutineType.SurpriseTrade => DoSurpriseTrades(sav, token),
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
                var attempts = hub.Config.Timings.ReconnectAttempts;
                var delay = hub.Config.Timings.ExtraReconnectDelay;
                var protocol = Config.Connection.Protocol;
                if (!await TryReconnect(attempts, delay, protocol, token).ConfigureAwait(false))
                    return;
            }
        }
    }

    private async Task DoNothing(CancellationToken token)
    {
        int waitCounter = 0;
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
        {
            if (waitCounter == 0)
                Log("无任务分配。等待新任务分配。");
            waitCounter++;
            if (waitCounter % 10 == 0 && hub.Config.AntiIdle)
                await Click(B, 1_000, token).ConfigureAwait(false);
            else
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }
    }

    private async Task DoTrades(SAV8SWSH sav, CancellationToken token)
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
            hub.Config.Stream.StartTrade(this, detail, hub);
            hub.Queues.StartTrade(this, detail);

            await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
        }
    }

    private Task WaitForQueueStep(int waitCounter, CancellationToken token)
    {
        if (waitCounter == 0)
        {
            // Updates the assets.
            hub.Config.Stream.IdleAssets(this);
            Log("无任务需要检查，等待新用户...");
        }

        const int interval = 10;
        if (waitCounter % interval == interval - 1 && hub.Config.AntiIdle)
            return Click(B, 1_000, token);
        return Task.Delay(1_000, token);
    }

    protected virtual (PokeTradeDetail<PK8>? detail, uint priority) GetTradeData(PokeRoutineType type)
    {
        string botName = Connection.Name;

        // First check the specific type's queue
        if (hub.Queues.TryDequeue(type, out var detail, out var priority, botName))
        {
            return (detail, priority);
        }

        // If we're doing FlexTrade, also check the Batch queue
        if (type == PokeRoutineType.FlexTrade)
        {
            if (hub.Queues.TryDequeue(PokeRoutineType.Batch, out detail, out priority, botName))
            {
                return (detail, priority);
            }
        }

        if (hub.Queues.TryDequeueLedy(out detail))
        {
            return (detail, PokeTradePriorities.TierFree);
        }
        return (null, PokeTradePriorities.TierFree);
    }

    private async Task<PokeTradeResult> PerformBatchTrade(SAV8SWSH sav, PokeTradeDetail<PK8> poke, CancellationToken token)
    {
        int completedTrades = 0;
        var startingDetail = poke;
        var originalTrainerID = startingDetail.Trainer.ID;
        byte[] lastOffered = new byte[8]; // Track the last offered Pokemon between trades

        var tradesToProcess = poke.BatchTrades ?? [poke.TradeData];
        var totalBatchTrades = tradesToProcess.Count;

        // Helper method to send collected Pokémon back to the user before early return
        void SendCollectedPokemonAndCleanup()
        {
            var allReceived = BatchTracker.GetReceivedPokemon(originalTrainerID);
            if (allReceived.Count > 0)
            {
                poke.SendNotification(this, $"正在发送中断前你交给我的 {allReceived.Count} 只宝可梦。");

                Log($"正在返还 {allReceived.Count} 只宝可梦给训练家 {originalTrainerID}。");

                for (int j = 0; j < allReceived.Count; j++)
                {
                    var pokemon = allReceived[j];
                    var speciesName = SpeciesName.GetSpeciesName(pokemon.Species, 2);
                    Log($"  - 返还: {speciesName} (校验和: {pokemon.Checksum:X8})");

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
            hub.Queues.Info.Remove(new TradeEntry<PK8>(poke, originalTrainerID, PokeRoutineType.Batch, poke.Trainer.TrainerName, poke.UniqueTradeID));
        }

        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        hub.Config.Stream.EndEnterCode(this);

        await EnsureConnectedToYComm(OverworldOffset, hub.Config, token).ConfigureAwait(false);
        if (await CheckIfSoftBanned(token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            SendCollectedPokemonAndCleanup();
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }

        while (await CheckIfSearchingForLinkTradePartner(token).ConfigureAwait(false))
        {
            Log("仍在搜索，正在重置机器人位置。");
            await ResetTradePosition(token).ConfigureAwait(false);
        }

        // Initial connection setup (only done once)
        Log("正在打开 Y-Comm 菜单。");
        await Click(Y, 2_000, token).ConfigureAwait(false);

        Log("正在选择连接交易。");
        await Click(A, 1_500, token).ConfigureAwait(false);

        Log("正在选择连接交易密码。");
        await Click(DDOWN, 500, token).ConfigureAwait(false);

        for (int i = 0; i < 2; i++)
            await Click(A, 1_500, token).ConfigureAwait(false);

        if (GameLang != LanguageID.English && GameLang != LanguageID.Spanish)
            await Click(A, 1_500, token).ConfigureAwait(false);

        if (poke.Type != PokeTradeType.Random)
            hub.Config.Stream.StartEnterCode(this);
        await Task.Delay(hub.Config.Timings.ExtraTimeOpenCodeEntry, token).ConfigureAwait(false);

        var code = poke.Code;
        Log($"正在输入连接交易密码: {code:0000 0000}...");
        await EnterLinkCode(code, hub.Config, token).ConfigureAwait(false);

        WaitAtBarrierIfApplicable(token);
        await Click(PLUS, 1_000, token).ConfigureAwait(false);

        hub.Config.Stream.EndEnterCode(this);

        // Wait for initial connection
        var delay_count = 0;
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            if (delay_count++ >= 5)
            {
                SendCollectedPokemonAndCleanup();
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverPostLinkCode;
            }

            for (int i = 0; i < 5; i++)
                await Click(A, 0_800, token).ConfigureAwait(false);
        }

        // Wait for and find trade partner
        poke.SendNotification(this, $"请提供你的宝可梦进行交易 1/{totalBatchTrades}。");
        poke.TradeSearching(this);
        await Task.Delay(0_500, token).ConfigureAwait(false);

        var partnerFound = await WaitForTradePartnerOffer(token).ConfigureAwait(false);
        if (!partnerFound || token.IsCancellationRequested)
        {
            SendCollectedPokemonAndCleanup();
            await ResetTradePosition(token).ConfigureAwait(false);
            return PokeTradeResult.NoTrainerFound;
        }

        await Task.Delay(5_500 + hub.Config.Timings.ExtraTimeOpenBox, token).ConfigureAwait(false);

        // Get and cache trade partner info
        var trainerName = await GetTradePartnerName(TradeMethod.LinkTrade, token).ConfigureAwait(false);
        var trainerTID = await GetTradePartnerTID7(TradeMethod.LinkTrade, token).ConfigureAwait(false);
        var trainerSID = await GetTradePartnerSID7(TradeMethod.LinkTrade, token).ConfigureAwait(false);
        var trainerNID = await GetTradePartnerNID(token).ConfigureAwait(false);

        Log($"找到连接交易伙伴: {trainerName}-{trainerTID} (ID: {trainerNID})");
        poke.SendNotification(this, $"找到连接交易伙伴: {trainerName}。**TID**: {trainerTID} **SID**: {trainerSID}");

        // Initialize lastOffered state for tracking between trades
        lastOffered = await Connection.ReadBytesAsync(LinkTradePartnerPokemonOffset, 8, token).ConfigureAwait(false);

        // Partner reputation check
        var partnerCheck = CheckPartnerReputation(this, poke, trainerNID, trainerName, AbuseSettings, token);
        if (partnerCheck != PokeTradeResult.Success)
        {
            poke.SendNotification(this, "检测到可疑活动。正在取消批量交易。");
            SendCollectedPokemonAndCleanup();
            await ExitTrade(false, token).ConfigureAwait(false);
            return partnerCheck;
        }

        // Update trade code storage
        var tradeCodeStorage = new TradeCodeStorage();
        var existingTradeDetails = tradeCodeStorage.GetTradeDetails(poke.Trainer.ID);
        bool shouldUpdateOT = existingTradeDetails?.OT != trainerName;
        bool shouldUpdateTID = existingTradeDetails?.TID != int.Parse(trainerTID);
        bool shouldUpdateSID = existingTradeDetails?.SID != int.Parse(trainerSID);

        if (shouldUpdateOT || shouldUpdateTID || shouldUpdateSID)
        {
            string? ot = shouldUpdateOT ? trainerName : existingTradeDetails?.OT;
            int? tid = shouldUpdateTID ? int.Parse(trainerTID) : existingTradeDetails?.TID;
            int? sid = shouldUpdateSID ? int.Parse(trainerSID) : existingTradeDetails?.SID;

            if (ot != null && tid.HasValue && sid.HasValue)
            {
                tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, ot, tid.Value, sid.Value);
            }
        }

        // Main batch trade loop
        for (int i = 0; i < totalBatchTrades; i++)
        {
            var currentTradeIndex = i;
            var toSend = tradesToProcess[currentTradeIndex];
            poke.TradeData = toSend;
            poke.Notifier.UpdateBatchProgress(currentTradeIndex + 1, toSend, poke.UniqueTradeID);

            // Apply AutoOT if needed
            if (hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
            {
                toSend = await ApplyAutoOT(toSend, trainerName, sav, token);
                tradesToProcess[currentTradeIndex] = toSend;
            }

            // Prepare the Pokémon
            if (toSend.Species != 0)
                await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);

            // For subsequent trades, we need to wait for the previous trade to complete
            if (currentTradeIndex > 0)
            {
                // Wait for the trade animation to complete and return to the trade screen
                await Task.Delay(8_000, token).ConfigureAwait(false);

                // Check if we're still connected
                if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                {
                    poke.SendNotification(this, "连接丢失。对方训练家可能已断开连接。");
                    SendCollectedPokemonAndCleanup();
                    return PokeTradeResult.TrainerLeft;
                }

                poke.SendNotification(this, $"**准备好了！** 你现在可以报价交易 {currentTradeIndex + 1}/{totalBatchTrades} 的宝可梦了。");
            }

            // Ensure we're in the box
            if (!await IsInBox(token).ConfigureAwait(false))
            {
                // Try to get back to box
                for (int retry = 0; retry < 3; retry++)
                {
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    if (await IsInBox(token).ConfigureAwait(false))
                        break;
                }

                if (!await IsInBox(token).ConfigureAwait(false))
                {
                    poke.SendNotification(this, $"交易 {completedTrades + 1}/{totalBatchTrades} 后进入盒子失败。正在取消剩余交易。");
                    SendCollectedPokemonAndCleanup();
                    await ExitTrade(true, token).ConfigureAwait(false);
                    return PokeTradeResult.RecoverOpenBox;
                }
            }

            // Read the offered Pokémon
            var offered = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
            var oldEC = await Connection.ReadBytesAsync(LinkTradePartnerPokemonOffset, 4, token).ConfigureAwait(false);

            if (offered == null)
            {
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 未提供宝可梦。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            // Validate the trade
            var trainer = new PartnerDataHolder(0, trainerName, "");
            var update = await GetEntityToSend(sav, poke, offered, oldEC, toSend, trainer, null, token).ConfigureAwait(false);
            if (update.check != PokeTradeResult.Success)
            {
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 检查失败。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return update.check;
            }
            toSend = update.toSend;

            // Confirm and execute the trade
            Log($"正在确认交易 {currentTradeIndex + 1}/{totalBatchTrades}。");
            var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
            if (tradeResult != PokeTradeResult.Success)
            {
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 确认失败。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return tradeResult;
            }

            if (token.IsCancellationRequested)
            {
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.RoutineCancel;
            }

            // Read the received Pokémon
            var received = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
            if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
            {
                poke.SendNotification(this, $"伙伴未完成交易 {currentTradeIndex + 1}/{totalBatchTrades}。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            // Trade was successful!
            UpdateCountsAndExport(poke, received, toSend);
            LogSuccessfulTrades(poke, 0, trainerName);

            BatchTracker.AddReceivedPokemon(originalTrainerID, received);
            completedTrades = currentTradeIndex + 1;
            Log($"已将收到的宝可梦 {received.Species} (校验和: {received.Checksum:X8}) 添加到批量追踪器，训练家 {originalTrainerID} (交易 {completedTrades}/{totalBatchTrades})");

            // Store the last offered Pokemon state for the next trade
            if (completedTrades < totalBatchTrades)
            {
                _ = await Connection.ReadBytesAsync(LinkTradePartnerPokemonOffset, 8, token).ConfigureAwait(false);
            }

            // Check if all trades are complete
            if (completedTrades == totalBatchTrades)
            {
                var allReceived = BatchTracker.GetReceivedPokemon(originalTrainerID);
                Log($"批量交易完成。训练家 {originalTrainerID} 存储了 {allReceived.Count} 只宝可梦");

                poke.SendNotification(this, "所有批量交易已完成！感谢你的交易！");

                if (hub.Config.Discord.ReturnPKMs && allReceived.Count > 0)
                {
                    poke.SendNotification(this, $"这是你交给我的 {allReceived.Count} 只宝可梦:");

                    for (int j = 0; j < allReceived.Count; j++)
                    {
                        var pokemon = allReceived[j];
                        var speciesName = SpeciesName.GetSpeciesName(pokemon.Species, 2);
                        Log($"  - 返还: {speciesName} (校验和: {pokemon.Checksum:X8})");

                        poke.SendNotification(this, pokemon, $"你交给我的宝可梦: {speciesName}");
                        await Task.Delay(500, token).ConfigureAwait(false);
                    }
                }

                if (allReceived.Count > 0)
                {
                    poke.TradeFinished(this, allReceived[^1]);
                }
                else
                {
                    poke.TradeFinished(this, received);
                }

                hub.Queues.CompleteTrade(this, startingDetail);
                BatchTracker.ClearReceivedPokemon(originalTrainerID);
                break;
            }
            else
            {
                // Notify about the next trade
                poke.SendNotification(this, $"交易 {completedTrades} 完成！正在准备交易 {completedTrades + 1}/{totalBatchTrades}...");
            }
        }

        // Only exit the trade after all trades are complete
        await ExitTrade(false, token).ConfigureAwait(false);
        poke.IsProcessing = false;
        return PokeTradeResult.Success;
    }

    private async Task PerformTrade(SAV8SWSH sav, PokeTradeDetail<PK8> detail, PokeRoutineType type, uint priority, CancellationToken token)
    {
        PokeTradeResult result;
        try
        {
            if (detail.Type == PokeTradeType.Batch)
                result = await PerformBatchTrade(sav, detail, token).ConfigureAwait(false);
            else
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

    private async Task HandleAbortedBatchTrade(PokeTradeDetail<PK8> detail, PokeRoutineType type, uint priority, PokeTradeResult result, CancellationToken token)
    {
        detail.IsProcessing = false;

        // Always remove from UsersInQueue on abort
        hub.Queues.Info.Remove(new TradeEntry<PK8>(detail, detail.Trainer.ID, type, detail.Trainer.TrainerName, detail.UniqueTradeID));

        if (detail.TotalBatchTrades > 1)
        {
            // Release the batch claim on failure
            BatchTracker.ReleaseBatch(detail.Trainer.ID, detail.UniqueTradeID);

            if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
            {
                detail.IsRetry = true;
                hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
                detail.SendNotification(this, "哎呀！你的批量交易过程中出了点问题。我会把你重新排队再试一次。");
            }
            else
            {
                detail.SendNotification(this, $"批量交易失败: {result}");
                detail.TradeCanceled(this, result);
                await ExitTrade(false, token).ConfigureAwait(false);
            }
        }
        else
        {
            HandleAbortedTrade(detail, type, priority, result);
        }
    }

    private void HandleAbortedTrade(PokeTradeDetail<PK8> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
    {
        detail.IsProcessing = false;
        if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
        {
            detail.IsRetry = true;
            hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
            detail.SendNotification(this, "哎呀！出了点问题。我会把你重新排队再试一次。");
        }
        else
        {
            detail.SendNotification(this, $"哎呀！出了点问题。取消交易: {result}。");
            detail.TradeCanceled(this, result);
        }
    }

    private async Task DoSurpriseTrades(SAV8SWSH sav, CancellationToken token)
    {
        await SetCurrentBox(0, token).ConfigureAwait(false);
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.SurpriseTrade)
        {
            var pkm = hub.Ledy.Pool.GetRandomSurprise();
            await EnsureConnectedToYComm(OverworldOffset, hub.Config, token).ConfigureAwait(false);
            var _ = await PerformSurpriseTrade(sav, pkm, token).ConfigureAwait(false);
        }
    }

    private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV8SWSH sav, PokeTradeDetail<PK8> poke, CancellationToken token)
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
        await EnsureConnectedToYComm(OverworldOffset, hub.Config, token).ConfigureAwait(false);
        hub.Config.Stream.EndEnterCode(this);

        if (await CheckIfSoftBanned(token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        var toSend = poke.TradeData;
        if (toSend.Species != 0)
            await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);

        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }

        while (await CheckIfSearchingForLinkTradePartner(token).ConfigureAwait(false))
        {
            Log("仍在搜索，正在重置机器人位置。");
            await ResetTradePosition(token).ConfigureAwait(false);
        }

        Log("正在打开 Y-Comm 菜单。");
        await Click(Y, 2_000, token).ConfigureAwait(false);

        Log("正在选择连接交易。");
        await Click(A, 1_500, token).ConfigureAwait(false);

        Log("正在选择连接交易密码。");
        await Click(DDOWN, 500, token).ConfigureAwait(false);

        for (int i = 0; i < 2; i++)
            await Click(A, 1_500, token).ConfigureAwait(false);

        // All other languages require an extra A press at this menu.
        if (GameLang != LanguageID.English && GameLang != LanguageID.Spanish)
            await Click(A, 1_500, token).ConfigureAwait(false);

        // Loading Screen
        if (poke.Type != PokeTradeType.Random)
            hub.Config.Stream.StartEnterCode(this);
        await Task.Delay(hub.Config.Timings.ExtraTimeOpenCodeEntry, token).ConfigureAwait(false);

        var code = poke.Code;
        Log($"正在输入连接交易密码: {code:0000 0000}...");
        await EnterLinkCode(code, hub.Config, token).ConfigureAwait(false);

        // Wait for Barrier to trigger all bots simultaneously.
        WaitAtBarrierIfApplicable(token);
        await Click(PLUS, 1_000, token).ConfigureAwait(false);

        hub.Config.Stream.EndEnterCode(this);

        // Confirming and return to overworld.
        var delay_count = 0;
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            if (delay_count++ >= 5)
            {
                // Too many attempts, recover out of the trade.
                await ExitTrade(true, token).ConfigureAwait(false);
                return PokeTradeResult.RecoverPostLinkCode;
            }

            for (int i = 0; i < 5; i++)
                await Click(A, 0_800, token).ConfigureAwait(false);
        }

        poke.TradeSearching(this);
        await Task.Delay(0_500, token).ConfigureAwait(false);

        // Wait for a Trainer...
        var partnerFound = await WaitForTradePartnerOffer(token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
        {
            return PokeTradeResult.RoutineCancel;
        }
        if (!partnerFound)
        {
            await ResetTradePosition(token).ConfigureAwait(false);
            return PokeTradeResult.NoTrainerFound;
        }

        // Select Pokémon
        // pkm already injected to b1s1
        await Task.Delay(5_500 + hub.Config.Timings.ExtraTimeOpenBox, token).ConfigureAwait(false); // necessary delay to get to the box properly

        var trainerName = await GetTradePartnerName(TradeMethod.LinkTrade, token).ConfigureAwait(false);
        var trainerTID = await GetTradePartnerTID7(TradeMethod.LinkTrade, token).ConfigureAwait(false);
        var trainerSID = await GetTradePartnerSID7(TradeMethod.LinkTrade, token).ConfigureAwait(false);
        var trainerNID = await GetTradePartnerNID(token).ConfigureAwait(false);
        RecordUtil<PokeTradeBotSWSH>.Record($"Initiating\t{trainerNID:X16}\t{trainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");
        Log($"找到连接交易伙伴: {trainerName}。**TID**: {trainerTID}  **SID**: {trainerSID}。等待宝可梦...");

        var tradeCodeStorage = new TradeCodeStorage();
        var existingTradeDetails = tradeCodeStorage.GetTradeDetails(poke.Trainer.ID);

        bool shouldUpdateOT = existingTradeDetails?.OT != trainerName;
        bool shouldUpdateTID = existingTradeDetails?.TID != int.Parse(trainerTID);
        bool shouldUpdateSID = existingTradeDetails?.SID != int.Parse(trainerSID);

        if (shouldUpdateOT || shouldUpdateTID || shouldUpdateSID)
        {
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, shouldUpdateOT ? trainerName : existingTradeDetails.OT, shouldUpdateTID ? int.Parse(trainerTID) : existingTradeDetails.TID, shouldUpdateSID ? int.Parse(trainerSID) : existingTradeDetails.SID);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8604 // Possible null reference argument.
        }

        var partnerCheck = CheckPartnerReputation(this, poke, trainerNID, trainerName, AbuseSettings, token);
        if (partnerCheck != PokeTradeResult.Success)
        {
            await ExitSeedCheckTrade(token).ConfigureAwait(false);
            return partnerCheck;
        }

        if (!await IsInBox(token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverOpenBox;
        }

        if (hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
        {
            toSend = await ApplyAutoOT(toSend, trainerName, sav, token);
        }

        // Confirm Box 1 Slot 1
        if (poke.Type == PokeTradeType.Specific)
        {
            for (int i = 0; i < 5; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);
        }

        poke.SendNotification(this, $"找到连接交易伙伴: {trainerName}。**TID**: {trainerTID} **SID**: {trainerSID}。等待宝可梦...");

        if (poke.Type == PokeTradeType.Dump)
            return await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);

        // After reading the offered Pokemon:
        var offered = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 25_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
        var oldEC = await Connection.ReadBytesAsync(LinkTradePartnerPokemonOffset, 4, token).ConfigureAwait(false);
        if (offered is null)
        {
            await ExitSeedCheckTrade(token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        SpecialTradeType itemReq = SpecialTradeType.None;
        if (poke.Type == PokeTradeType.Seed)
            itemReq = CheckItemRequest(ref offered, this, poke, trainerName, sav);
        if (itemReq == SpecialTradeType.FailReturn)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.IllegalTrade;
        }

        if (poke.Type == PokeTradeType.Seed && itemReq == SpecialTradeType.None)
        {
            // Immediately exit, we aren't trading anything.
            poke.SendNotification(this, "没有持有道具或有效请求！取消此交易。");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerRequestBad;
        }

        var trainer = new PartnerDataHolder(trainerNID, trainerName, trainerTID);
        (toSend, PokeTradeResult update) = await GetEntityToSend(sav, poke, offered, oldEC, toSend, trainer, poke.Type == PokeTradeType.Seed ? itemReq : null, token).ConfigureAwait(false);
        if (update != PokeTradeResult.Success)
        {
            if (itemReq != SpecialTradeType.None)
            {
                poke.SendNotification(this, "你的请求不合法。请尝试不同的宝可梦或请求。");
            }
            await ExitTrade(false, token).ConfigureAwait(false);
            return update;
        }

        // Check if partner offered a Pokemon that will evolve
        if (hub.Config.Trade.TradeConfiguration.DisallowTradeEvolve && TradeEvolutions.WillTradeEvolve(offered.Species, offered.Form, offered.HeldItem, toSend.Species))
        {
            Log("交易已取消，因为训练家提供的宝可梦会在交易后进化。");
            poke.SendNotification(this, "交易已取消。您不能交易会进化的宝可梦。为防止这种情况，请给您的宝可梦持有不变之石，或换一只宝可梦交易。");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TradeEvolveNotAllowed;
        }

        var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
        if (tradeResult != PokeTradeResult.Success)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return tradeResult;
        }

        if (token.IsCancellationRequested)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.RoutineCancel;
        }

        // Trade was Successful! Now we can send the success notifications
        var received = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
        if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
        {
            Log($"交易未完成。用户未交易他们的宝可梦。");
            RecordUtil<PokeTradeBotSWSH>.Record($"Cancelled\t{trainerNID:X16}\t{trainerName}\t{poke.Trainer.TrainerName}\t{poke.ID}\t{toSend.Species}\t{toSend.EncryptionConstant:X8}\t{offered.Species}\t{offered.EncryptionConstant:X8}");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        // Now that we confirmed the trade was successful, send the appropriate notification
        if (itemReq == SpecialTradeType.WonderCard)
            poke.SendNotification(this, "分发成功！");
        else if (itemReq != SpecialTradeType.None && itemReq != SpecialTradeType.Shinify)
            poke.SendNotification(this, "特殊请求成功！");
        else if (itemReq == SpecialTradeType.Shinify)
            poke.SendNotification(this, "闪光化成功！感谢你成为社区的一员！");

        // Continue with the rest of the successful trade logic
        Log($"交易完成。从用户收到 {GameInfo.GetStrings("zh").Species[received.Species]}，发送 {GameInfo.GetStrings("zh").Species[toSend.Species]}。");
        poke.TradeFinished(this, received);
        RecordUtil<PokeTradeBotSWSH>.Record($"Finished\t{trainerNID:X16}\t{trainerName}\t{poke.Trainer.TrainerName}\t{poke.ID}\t{toSend.Species}\t{toSend.EncryptionConstant:X8}\t{received.Species}\t{received.EncryptionConstant:X8}");

        // Only log if we completed the trade.
        UpdateCountsAndExport(poke, received, toSend);

        // Log for Trade Abuse tracking.
        LogSuccessfulTrades(poke, trainerNID, trainerName);

        await ExitTrade(false, token).ConfigureAwait(false);
        return PokeTradeResult.Success;
    }

    protected virtual Task<bool> WaitForTradePartnerOffer(CancellationToken token)
    {
        Log("正在等待训练家...");
        return WaitForPokemonChanged(LinkTradePartnerPokemonOffset, hub.Config.Trade.TradeConfiguration.TradeWaitTime * 1_000, 0_200, token);
    }

    private void UpdateCountsAndExport(PokeTradeDetail<PK8> poke, PK8 received, PK8 toSend)
    {
        var counts = TradeSettings;
        if (poke.Type == PokeTradeType.Random)
            counts.CountStatsSettings.AddCompletedDistribution();
        else if (poke.Type == PokeTradeType.FixOT)
            counts.CountStatsSettings.AddCompletedFixOTs();
        else if (poke.Type == PokeTradeType.Clone)
            counts.CountStatsSettings.AddCompletedClones();
        else
            counts.CountStatsSettings.AddCompletedTrade();

        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
        {
            var subfolder = poke.Type.ToString().ToLower();
            var service = poke.Notifier.GetType().ToString().ToLower();
            var tradedFolder = service.Contains("twitch") ? Path.Combine("traded", "twitch") : service.Contains("discord") ? Path.Combine("traded", "discord") : "traded";
            DumpPokemon(DumpSetting.DumpFolder, subfolder, received); // received by bot
            if (poke.Type is PokeTradeType.Specific or PokeTradeType.Clone or PokeTradeType.FixOT)
                DumpPokemon(DumpSetting.DumpFolder, tradedFolder, toSend); // sent to partner
        }
    }

    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PK8> detail, CancellationToken token)
    {
        // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
        var oldEC = await Connection.ReadBytesAsync(BoxStartOffset, 8, token).ConfigureAwait(false);

        await Click(A, 3_000, token).ConfigureAwait(false);
        for (int i = 0; i < hub.Config.Trade.TradeConfiguration.MaxTradeConfirmTime; i++)
        {
            // If we are in a Trade Evolution/Pokédex Entry and the Trade Partner quits, we land on the Overworld
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return PokeTradeResult.TrainerLeft;
            if (await IsUserBeingShifty(detail, token).ConfigureAwait(false))
                return PokeTradeResult.SuspiciousActivity;
            await Click(A, 1_000, token).ConfigureAwait(false);

            // EC is detectable at the start of the animation.
            var newEC = await Connection.ReadBytesAsync(BoxStartOffset, 8, token).ConfigureAwait(false);
            if (!newEC.SequenceEqual(oldEC))
            {
                await Task.Delay(25_000, token).ConfigureAwait(false);
                return PokeTradeResult.Success;
            }
        }

        if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            return PokeTradeResult.TrainerLeft;

        return PokeTradeResult.Success;
    }

    protected virtual async Task<(PK8 toSend, PokeTradeResult check)> GetEntityToSend(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, byte[] oldEC, PK8 toSend, PartnerDataHolder partnerID, SpecialTradeType? stt, CancellationToken token)
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

    private async Task<(PK8 toSend, PokeTradeResult check)> JustInject(SAV8SWSH sav, PK8 offered, CancellationToken token)
    {
        await Click(A, 0_800, token).ConfigureAwait(false);
        await SetBoxPokemon(offered, 0, 0, token, sav).ConfigureAwait(false);

        for (int i = 0; i < 5; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);

        return (offered, PokeTradeResult.Success);
    }

    private async Task<(PK8 toSend, PokeTradeResult check)> HandleClone(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, byte[] oldEC, CancellationToken token)
    {
        if (hub.Config.Discord.ReturnPKMs)
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
        if (hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        poke.SendNotification(this, $"**已克隆你的 {GameInfo.GetStrings("zh").Species[clone.Species]}！**\n现在按 B 取消你的报价，然后交给我一只你不要的宝可梦。");
        Log($"已克隆 {GameInfo.GetStrings("zh").Species[clone.Species]}。等待用户更换宝可梦...");

        // Separate this out from WaitForPokemonChanged since we compare to old EC from original read.
        var partnerFound = await ReadUntilChanged(LinkTradePartnerPokemonOffset, oldEC, 15_000, 0_200, false, token).ConfigureAwait(false);

        if (!partnerFound)
        {
            poke.SendNotification(this, "**嘿，立即更换，否则我要离开了！！！**");

            // They get one more chance.
            partnerFound = await ReadUntilChanged(LinkTradePartnerPokemonOffset, oldEC, 15_000, 0_200, false, token).ConfigureAwait(false);
        }

        var pk2 = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 3_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
        if (!partnerFound || pk2 == null || SearchUtil.HashByDetails(pk2) == SearchUtil.HashByDetails(offered))
        {
            Log("交易伙伴未更换宝可梦。");
            return (offered, PokeTradeResult.TrainerTooSlow);
        }

        await Click(A, 0_800, token).ConfigureAwait(false);
        await SetBoxPokemon(clone, 0, 0, token, sav).ConfigureAwait(false);

        for (int i = 0; i < 5; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);

        return (clone, PokeTradeResult.Success);
    }

    private async Task<(PK8 toSend, PokeTradeResult check)> HandleRandomLedy(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, PK8 toSend, PartnerDataHolder partner, CancellationToken token)
    {
        // Allow the trade partner to do a Ledy swap.
        var config = hub.Config.Distribution;
        var trade = hub.Ledy.GetLedyTrade(offered, partner.TrainerOnlineID, config.LedySpecies);
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
            await Click(A, 0_800, token).ConfigureAwait(false);
            await SetBoxPokemon(toSend, 0, 0, token, sav).ConfigureAwait(false);
            await Task.Delay(2_500, token).ConfigureAwait(false);
        }
        else if (config.LedyQuitIfNoMatch)
        {
            var nickname = offered.IsNicknamed ? $" (Nickname: \"{offered.Nickname}\")" : string.Empty;
            poke.SendNotification(this, $"未找到与所提供的 {GameInfo.GetStrings("zh").Species[offered.Species]}{nickname} 匹配的宝可梦。");
            return (toSend, PokeTradeResult.TrainerRequestBad);
        }

        for (int i = 0; i < 5; i++)
        {
            if (await IsUserBeingShifty(poke, token).ConfigureAwait(false))
                return (toSend, PokeTradeResult.SuspiciousActivity);
            await Click(A, 0_500, token).ConfigureAwait(false);
        }

        return (toSend, PokeTradeResult.Success);
    }

    // For pointer offsets that don't change per session are accessed frequently, so set these each time we start.
    private async Task InitializeSessionOffsets(CancellationToken token)
    {
        Log("正在缓存会话偏移量...");
        OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
    }

    protected virtual async Task<bool> IsUserBeingShifty(PokeTradeDetail<PK8> detail, CancellationToken token)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return false;
    }

    private async Task RestartGameSWSH(CancellationToken token)
    {
        await ReOpenGame(hub.Config, token).ConfigureAwait(false);
        await InitializeSessionOffsets(token).ConfigureAwait(false);
    }

    private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PK8> detail, CancellationToken token)
    {
        int ctr = 0;
        var time = TimeSpan.FromSeconds(hub.Config.Trade.TradeConfiguration.MaxDumpTradeTime);
        var start = DateTime.Now;
        var pkprev = new PK8();
        var bctr = 0;
        while (ctr < hub.Config.Trade.TradeConfiguration.MaxDumpsPerTrade && DateTime.Now - start < time)
        {
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                break;
            if (bctr++ % 3 == 0)
                await Click(B, 0_100, token).ConfigureAwait(false);

            var pk = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 3_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
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
            var msg = hub.Config.Trade.TradeConfiguration.DumpTradeLegalityCheck ? verbose : $"文件 {ctr}";

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
        await ExitSeedCheckTrade(token).ConfigureAwait(false);
        if (ctr == 0)
            return PokeTradeResult.TrainerTooSlow;

        TradeSettings.CountStatsSettings.AddCompletedDumps();
        detail.Notifier.SendNotification(this, detail, $"已转储 {ctr} 只宝可梦。");
        detail.Notifier.TradeFinished(this, detail, detail.TradeData); // blank pk8
        return PokeTradeResult.Success;
    }

    private async Task<PokeTradeResult> PerformSurpriseTrade(SAV8SWSH sav, PK8 pkm, CancellationToken token)
    {
        // General Bot Strategy:
        // 1. Inject to b1s1
        // 2. Send out Trade
        // 3. Clear received PKM to skip the trade animation
        // 4. Repeat

        // Inject to b1s1
        if (await CheckIfSoftBanned(token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        Log("开始下一次魔法交换。正在获取数据...");
        await SetBoxPokemon(pkm, 0, 0, token, sav).ConfigureAwait(false);

        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }

        if (await CheckIfSearchingForSurprisePartner(token).ConfigureAwait(false))
        {
            Log("仍在搜索，正在重置机器人位置。");
            await ResetTradePosition(token).ConfigureAwait(false);
        }

        Log("正在打开 Y-Comm 菜单。");
        await Click(Y, 1_500, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        Log("正在选择魔法交换。");
        await Click(DDOWN, 0_500, token).ConfigureAwait(false);
        await Click(A, 2_000, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        await Task.Delay(0_750, token).ConfigureAwait(false);

        if (!await IsInBox(token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverPostLinkCode;
        }

        Log($"正在选择宝可梦: {pkm.FileName}");

        // Box 1 Slot 1; no movement required.
        await Click(A, 0_700, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        Log("正在确认...");
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            await Click(A, 0_800, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        // Let Surprise Trade be sent out before checking if we're back to the Overworld.
        await Task.Delay(3_000, token).ConfigureAwait(false);

        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverReturnOverworld;
        }

        // Wait 30 Seconds for Trainer...
        Log("正在等待魔法交换伙伴...");

        // Wait for an offer...
        var oldEC = await Connection.ReadBytesAsync(SurpriseTradeSearchOffset, 4, token).ConfigureAwait(false);
        var partnerFound = await ReadUntilChanged(SurpriseTradeSearchOffset, oldEC, hub.Config.Trade.TradeConfiguration.TradeWaitTime * 1_000, 0_200, false, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        if (!partnerFound)
        {
            await ResetTradePosition(token).ConfigureAwait(false);
            return PokeTradeResult.NoTrainerFound;
        }

        // Let the game flush the results and de-register from the online surprise trade queue.
        await Task.Delay(7_000, token).ConfigureAwait(false);

        var TrainerName = await GetTradePartnerName(TradeMethod.SurpriseTrade, token).ConfigureAwait(false);
        var TrainerTID = await GetTradePartnerTID7(TradeMethod.SurpriseTrade, token).ConfigureAwait(false);
        var SurprisePoke = await ReadSurpriseTradePokemon(token).ConfigureAwait(false);

        Log($"找到魔法交易伙伴: {TrainerName}-{TrainerTID}，宝可梦: {GameInfo.GetStrings("zh").Species[SurprisePoke.Species]}");

        // Clear out the received trade data; we want to skip the trade animation.
        // The box slot locks have been removed prior to searching.

        await Connection.WriteBytesAsync(BitConverter.GetBytes(SurpriseTradeSearch_Empty), SurpriseTradeSearchOffset, token).ConfigureAwait(false);
        await Connection.WriteBytesAsync(PokeTradeBotUtil.EMPTY_SLOT, SurpriseTradePartnerPokemonOffset, token).ConfigureAwait(false);

        // Let the game recognize our modifications before finishing this loop.
        await Task.Delay(5_000, token).ConfigureAwait(false);

        // Clear the Surprise Trade slot locks! We'll skip the trade animation and reuse the slot on later loops.
        // Write 8 bytes of FF to set both Int32's to -1. Regular locks are [Box32][Slot32]

        await Connection.WriteBytesAsync(BitConverter.GetBytes(ulong.MaxValue), SurpriseTradeLockBox, token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            Log("交易完成！");
        else
            await ExitTrade(true, token).ConfigureAwait(false);

        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            DumpPokemon(DumpSetting.DumpFolder, "surprise", SurprisePoke);
        TradeSettings.CountStatsSettings.AddCompletedSurprise();

        return PokeTradeResult.Success;
    }

    private async Task<PokeTradeResult> EndSeedCheckTradeAsync(PokeTradeDetail<PK8> detail, PK8 pk, CancellationToken token)
    {
        await ExitSeedCheckTrade(token).ConfigureAwait(false);

        detail.TradeFinished(this, pk);

        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            DumpPokemon(DumpSetting.DumpFolder, "seed", pk);

        // Send results from separate thread; the bot doesn't need to wait for things to be calculated.
#pragma warning disable 4014
        Task.Run(() =>
        {
            try
            {
                ReplyWithSeedCheckResults(detail, pk);
            }
            catch (Exception ex)
            {
                detail.SendNotification(this, $"无法计算种子: {ex.Message}\r\n{ex.StackTrace}");
            }
        }, token);
#pragma warning restore 4014

        TradeSettings.CountStatsSettings.AddCompletedSeedCheck();

        return PokeTradeResult.Success;
    }

    private void ReplyWithSeedCheckResults(PokeTradeDetail<PK8> detail, PK8 result)
    {
        detail.SendNotification(this, "正在计算你的种子...");

        if (result.IsShiny)
        {
            Log("这只宝可梦已经是闪光的！"); // 不需要检查下一个闪光帧
            detail.SendNotification(this, "这只宝可梦已经是闪光的！未进行团体战种子计算。");

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, "seed", result);

            detail.TradeFinished(this, result);
            return;
        }

        SeedChecker.CalculateAndNotify(result, detail, hub.Config.SeedCheckSWSH, this);
        Log("种子计算完成。");
    }

    private void WaitAtBarrierIfApplicable(CancellationToken token)
    {
        if (!ShouldWaitAtBarrier)
            return;
        var opt = hub.Config.Distribution.SynchronizeBots;
        if (opt == BotSyncOption.NoSync)
            return;

        var timeoutAfter = hub.Config.Distribution.SynchronizeTimeout;
        if (FailedBarrier == 1) // failed last iteration
            timeoutAfter *= 2; // try to re-sync in the event things are too slow.

        var result = hub.BotSync.Barrier.SignalAndWait(TimeSpan.FromSeconds(timeoutAfter), token);

        if (result)
        {
            FailedBarrier = 0;
            return;
        }

        FailedBarrier++;
        Log($"同步屏障在 {timeoutAfter} 秒后超时。继续执行。");
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
            hub.BotSync.Barrier.AddParticipant();
            Log($"已加入同步屏障。计数: {hub.BotSync.Barrier.ParticipantCount}");
        }
        else
        {
            hub.BotSync.Barrier.RemoveParticipant();
            Log($"已离开同步屏障。计数: {hub.BotSync.Barrier.ParticipantCount}");
        }
    }

    private async Task<bool> WaitForPokemonChanged(uint offset, int waitms, int waitInterval, CancellationToken token)
    {
        // check EC and checksum; some pkm may have same EC if shown sequentially
        var oldEC = await Connection.ReadBytesAsync(offset, 8, token).ConfigureAwait(false);
        return await ReadUntilChanged(offset, oldEC, waitms, waitInterval, false, token).ConfigureAwait(false);
    }

    private async Task ExitTrade(bool unexpected, CancellationToken token)
    {
        if (unexpected)
            Log("异常行为，正在恢复位置。");

        int attempts = 0;
        int softBanAttempts = 0;
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            var screenID = await GetCurrentScreen(token).ConfigureAwait(false);
            if (screenID == CurrentScreen_Softban)
            {
                softBanAttempts++;
                if (softBanAttempts > 10)
                    await RestartGameSWSH(token).ConfigureAwait(false);
            }

            attempts++;
            if (attempts >= 15)
                break;

            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
        }
    }

    private async Task ExitSeedCheckTrade(CancellationToken token)
    {
        // Seed Check Bot doesn't show anything, so it can skip the first B press.
        int attempts = 0;
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            attempts++;
            if (attempts >= 15)
                break;

            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
        }

        await Task.Delay(3_000, token).ConfigureAwait(false);
    }

    private async Task ResetTradePosition(CancellationToken token)
    {
        Log("正在重置机器人位置。");

        // Shouldn't ever be used while not on overworld.
        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            await ExitTrade(true, token).ConfigureAwait(false);

        // Ensure we're searching before we try to reset a search.
        if (!await CheckIfSearchingForLinkTradePartner(token).ConfigureAwait(false))
            return;

        await Click(Y, 2_000, token).ConfigureAwait(false);
        for (int i = 0; i < 5; i++)
            await Click(A, 1_500, token).ConfigureAwait(false);

        // Extra A press for Japanese.
        if (GameLang == LanguageID.Japanese)
            await Click(A, 1_500, token).ConfigureAwait(false);
        await Click(B, 1_500, token).ConfigureAwait(false);
        await Click(B, 1_500, token).ConfigureAwait(false);
    }

    private async Task<bool> CheckIfSearchingForLinkTradePartner(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(LinkTradeSearchingOffset, 1, token).ConfigureAwait(false);
        return data[0] == 1; // changes to 0 when found
    }

    private async Task<bool> CheckIfSearchingForSurprisePartner(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(SurpriseTradeSearchOffset, 8, token).ConfigureAwait(false);
        return BitConverter.ToUInt32(data, 0) == SurpriseTradeSearch_Searching;
    }

    private async Task<string> GetTradePartnerName(TradeMethod tradeMethod, CancellationToken token)
    {
        var ofs = GetTrainerNameOffset(tradeMethod);
        var data = await Connection.ReadBytesAsync(ofs, 26, token).ConfigureAwait(false);
        return StringConverter8.GetString(data);
    }

    private async Task<string> GetTradePartnerTID7(TradeMethod tradeMethod, CancellationToken token)
    {
        var ofs = GetTrainerTIDSIDOffset(tradeMethod);
        var data = await Connection.ReadBytesAsync(ofs, 8, token).ConfigureAwait(false);

        var tidsid = BitConverter.ToUInt32(data, 0);
        return $"{tidsid % 1_000_000:000000}";
    }

    // Thanks Secludely https://github.com/Secludedly/ZE-FusionBot/commit/f064d9eaf11ba2b2a0a79fa4c7ec5bf6dacf780c
    private async Task<string> GetTradePartnerSID7(TradeMethod tradeMethod, CancellationToken token)
    {
        var ofs = GetTrainerTIDSIDOffset(tradeMethod);
        var data = await Connection.ReadBytesAsync(ofs, 8, token).ConfigureAwait(false);

        var tidsid = BitConverter.ToUInt32(data, 0);
        return $"{tidsid / 1_000_000:0000}";
    }

    public async Task<ulong> GetTradePartnerNID(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(LinkTradePartnerNIDOffset, 8, token).ConfigureAwait(false);
        return BitConverter.ToUInt64(data, 0);
    }

    private async Task<(PK8 toSend, PokeTradeResult check)> HandleFixOT(SAV8SWSH sav, PokeTradeDetail<PK8> poke, PK8 offered, PartnerDataHolder partner, CancellationToken token)
    {
        if (hub.Config.Discord.ReturnPKMs)
            poke.SendNotification(this, offered, $"这是你展示给我的 - {GameInfo.GetStrings("zh").Species[offered.Species]}");

        var adOT = TradeExtensions<PK8>.HasAdName(offered, out _);
        var laInit = new LegalityAnalysis(offered);
        if (!adOT && laInit.Valid)
        {
            poke.SendNotification(this, "在昵称或 OT 中未检测到广告，且宝可梦合法。退出交易。");
            return (offered, PokeTradeResult.TrainerRequestBad);
        }

        var clone = offered.Clone();
        if (hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        string shiny = string.Empty;
        if (!TradeExtensions<PK8>.ShinyLockCheck(offered.Species, TradeExtensions<PK8>.FormOutput(offered.Species, offered.Form, out _), $"{(Ball)offered.Ball}"))
            shiny = $"\nShiny: {(offered.ShinyXor == 0 ? "Square" : offered.IsShiny ? "Star" : "No")}";
        else shiny = "\nShiny: No";

        var name = partner.TrainerName;
        var ball = $"\n{(Ball)offered.Ball}";
        var extraInfo = $"OT: {name}{ball}{shiny}";
        var set = ShowdownParsing.GetShowdownText(offered).Split('\n').ToList();
        var shinyRes = set.Find(x => x.Contains("Shiny"));
        if (shinyRes != null)
            set.Remove(shinyRes);
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
            var info = new SimpleTrainerInfo { Gender = clone.OriginalTrainerGender, Language = clone.Language, OT = name, TID16 = clone.TID16, SID16 = clone.SID16, Generation = 8 };
            var mg = EncounterEvent.GetAllEvents().Where(x => x.Species == clone.Species && x.Form == clone.Form && x.IsShiny == clone.IsShiny && x.OriginalTrainerName == clone.OriginalTrainerName).ToList();
            if (mg.Count > 0)
                clone = TradeExtensions<PK8>.CherishHandler(mg.First(), info);
            else clone = (PK8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }
        else
        {
            clone = (PK8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }

        var la = new LegalityAnalysis(clone);
        clone = (PK8)TradeExtensions<PK8>.TrashBytes(clone, la);
        clone.ResetPartyStats();

        la = new LegalityAnalysis(clone);
        if (!la.Valid)
        {
            poke.SendNotification(this, "根据 PKHeX 的合法性检查，此宝可梦不合法。我无法修复它。退出交易。");
            return (clone, PokeTradeResult.IllegalTrade);
        }

        TradeExtensions<PK8>.HasAdName(offered, out string detectedAd);
        poke.SendNotification(this, $"{(!laInit.Valid ? "**已合法化" : "**已修复昵称/OT：")} {(Species)clone.Species}**（检测到广告: {detectedAd}）！现在请确认交易！");
        Log($"{(!laInit.Valid ? "已合法化" : "已修复昵称/OT：")} {(Species)clone.Species}！");

        await Click(A, 0_800, token).ConfigureAwait(false);
        await SetBoxPokemon(clone, 0, 0, token, sav).ConfigureAwait(false);
        await Click(A, 0_500, token).ConfigureAwait(false);
        poke.SendNotification(this, "现在请确认交易！");

        await Task.Delay(6_000, token).ConfigureAwait(false);
        var pk2 = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 1_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
        bool changed = pk2 == null || clone.Species != pk2.Species || offered.OriginalTrainerName != pk2.OriginalTrainerName;
        if (changed)
        {
            Log($"{name} 更换了展示的宝可梦（{(Species)clone.Species}）{(pk2 != null ? $" 换成 {(Species)pk2.Species}" : "")}");
            poke.SendNotification(this, "**请提供最初展示的宝可梦，否则我要离开了！**");
            var timer = 10_000;
            while (changed)
            {
                pk2 = await ReadUntilPresent(LinkTradePartnerPokemonOffset, 2_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
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

        await Click(A, 0_500, token).ConfigureAwait(false);
        for (int i = 0; i < 5; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);

        return (clone, PokeTradeResult.Success);
    }

    private async Task<PK8> ApplyAutoOT(PK8 toSend, string trainerName, SAV8SWSH sav, CancellationToken token)
    {
        // Special handling for Pokémon GO
        if (toSend.Version == GameVersion.GO)
        {
            var goClone = toSend.Clone();

            // Check if GO Pokemon has a home tracker
            if (toSend is IHomeTrack { HasTracker: true })
            {
                // Can only change OT name if it has a home tracker
                goClone.OriginalTrainerName = trainerName;
                ClearOTTrash(goClone, trainerName);

                if (!toSend.ChecksumValid)
                    goClone.RefreshChecksum();

                Log("仅将 OT 名称应用到来自 GO 的宝可梦（有 HOME 追踪器）。");
                await SetBoxPokemon(goClone, 0, 0, token, sav).ConfigureAwait(false);
                return goClone;
            }
            else
            {
                // No home tracker: can apply OT, TID, and SID
                var goData = await Connection.ReadBytesAsync(LinkTradePartnerNameOffset - 0x8, 8, token).ConfigureAwait(false);
                var goTidSid = BitConverter.ToUInt32(goData, 0);

                goClone.OriginalTrainerName = trainerName;
                goClone.OriginalTrainerGender = goData[6];
                goClone.TrainerTID7 = goTidSid % 1_000_000;
                goClone.TrainerSID7 = goTidSid / 1_000_000;

                ClearOTTrash(goClone, trainerName);

                if (toSend.IsShiny)
                    goClone.PID = (uint)((goClone.TID16 ^ goClone.SID16 ^ (goClone.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (goClone.PID & 0xFFFF);

                if (!toSend.ChecksumValid)
                    goClone.RefreshChecksum();

                Log("已将 OT、TID 和 SID 应用到来自 GO 的宝可梦（无 HOME 追踪器）。");
                await SetBoxPokemon(goClone, 0, 0, token, sav).ConfigureAwait(false);
                return goClone;
            }
        }

        if (toSend is IHomeTrack pk && pk.HasTracker)
        {
            Log("检测到 Home 追踪器。无法应用 AutoOT。");
            return toSend;
        }

        // Check if the Pokémon is from a Mystery Gift
        bool isMysteryGift = toSend.FatefulEncounter;

        var data = await Connection.ReadBytesAsync(LinkTradePartnerNameOffset - 0x8, 8, token).ConfigureAwait(false);
        var tidsid = BitConverter.ToUInt32(data, 0);
        var cln = toSend.Clone();

        if (isMysteryGift)
        {
            Log("检测到神秘礼物。仅应用 OT 信息，保留语言设置。");
            // Only set OT-related info for Mystery Gifts without preset OT/TID/SID
            cln.OriginalTrainerGender = data[6];
            cln.TrainerTID7 = tidsid % 1_000_000;
            cln.TrainerSID7 = tidsid / 1_000_000;
            cln.OriginalTrainerName = trainerName;
        }
        else
        {
            // Apply all trade partner details for non-Mystery Gift Pokémon
            cln.OriginalTrainerGender = data[6];
            cln.TrainerTID7 = tidsid % 1_000_000;
            cln.TrainerSID7 = tidsid / 1_000_000;
            cln.Language = data[5];
            cln.OriginalTrainerName = trainerName;
        }

        ClearOTTrash(cln, trainerName);

        if (!toSend.IsNicknamed)
            cln.ClearNickname();

        if (toSend.IsShiny)
            cln.PID = (uint)((cln.TID16 ^ cln.SID16 ^ (cln.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (cln.PID & 0xFFFF);

        if (!toSend.ChecksumValid)
            cln.RefreshChecksum();

        var tradeswsh = new LegalityAnalysis(cln);
        if (tradeswsh.Valid)
        {
            Log("宝可梦有效，使用交易伙伴信息。正在交换详情。");
            await SetBoxPokemon(cln, 0, 0, token, sav).ConfigureAwait(false);
            return cln;
        }
        else
        {
            Log("使用交易伙伴信息后宝可梦无效。");
            return toSend;
        }
    }

    private static void ClearOTTrash(PK8 pokemon, string trainerName)
    {
        Span<byte> trash = pokemon.OriginalTrainerTrash;
        trash.Clear();
        int maxLength = trash.Length / 2;
        int actualLength = Math.Min(trainerName.Length, maxLength);
        for (int i = 0; i < actualLength; i++)
        {
            char value = trainerName[i];
            trash[i * 2] = (byte)value;
            trash[(i * 2) + 1] = (byte)(value >> 8);
        }
        if (actualLength < maxLength)
        {
            trash[actualLength * 2] = 0x00;
            trash[(actualLength * 2) + 1] = 0x00;
        }
    }
}
