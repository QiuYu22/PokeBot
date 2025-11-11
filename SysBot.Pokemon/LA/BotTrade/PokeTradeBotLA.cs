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
using static SysBot.Pokemon.PokeDataOffsetsLA;

namespace SysBot.Pokemon;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class PokeTradeBotLA(PokeTradeHub<PA8> Hub, PokeBotState Config) : PokeRoutineExecutor8LA(Config), ICountBot, ITradeBot
{
    private readonly TradeSettings TradeSettings = Hub.Config.Trade;

    private readonly TradeAbuseSettings AbuseSettings = Hub.Config.TradeAbuse;

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
    private readonly FolderSettings DumpSetting = Hub.Config.Folder;

    /// <summary>
    /// Synchronized start for multiple bots.
    /// </summary>
    public bool ShouldWaitAtBarrier { get; private set; }

    /// <summary>
    /// Tracks failed synchronized starts to attempt to re-sync.
    /// </summary>
    public int FailedBarrier { get; private set; }

    // Cached offsets that stay the same per session.
    private ulong BoxStartOffset;

    private ulong SoftBanOffset;

    private ulong OverworldOffset;

    private ulong TradePartnerNIDOffset;

    // Cached offsets that stay the same per trade.
    private ulong TradePartnerOfferedOffset;

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

            Log("正在识别主机的训练家数据。");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            RecentTrainerCache.SetRecentTrainer(sav);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
            OnConnectionSuccess();
            Log($"正在启动 {nameof(PokeTradeBotLA)} 主循环。");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            OnConnectionError(e);
            throw;
        }

        Log($"结束 {nameof(PokeTradeBotLA)} 循环。");
        await HardStop().ConfigureAwait(false);
    }

    public override Task HardStop()
    {
        UpdateBarrier(false);
        return CleanExit(CancellationToken.None);
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
            Log("正在重新启动主循环。");
            await MainLoop(t).ConfigureAwait(false);
        }
    }

    private async Task InnerLoop(SAV8LA sav, CancellationToken token)
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

    private async Task DoNothing(CancellationToken token)
    {
        int waitCounter = 0;
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
        {
            if (waitCounter == 0)
                Log("当前无任务，等待新的任务指派。");
            waitCounter++;
            if (waitCounter % 10 == 0 && Hub.Config.AntiIdle)
                await Click(B, 1_000, token).ConfigureAwait(false);
            else
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }
    }

    private async Task DoTrades(SAV8LA sav, CancellationToken token)
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

            string tradetype = $"（{detail.Type}）";
            Log($"开始进行下一次 {type}{tradetype} 机器人交易，正在获取数据...");
            Hub.Config.Stream.StartTrade(this, detail, Hub);
            Hub.Queues.StartTrade(this, detail);

            await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
        }
    }

    private Task WaitForQueueStep(int waitCounter, CancellationToken token)
    {
        if (waitCounter == 0)
        {
            // Updates the assets.
            Hub.Config.Stream.IdleAssets(this);
            Log("暂无待处理内容，正在等待新用户...");
        }

        const int interval = 10;
        if (waitCounter % interval == interval - 1 && Hub.Config.AntiIdle)
            return Click(B, 1_000, token);
        return Task.Delay(1_000, token);
    }

    protected virtual (PokeTradeDetail<PA8>? detail, uint priority) GetTradeData(PokeRoutineType type)
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

    private async Task PerformTrade(SAV8LA sav, PokeTradeDetail<PA8> detail, PokeRoutineType type, uint priority, CancellationToken token)
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

    private async Task HandleAbortedBatchTrade(PokeTradeDetail<PA8> detail, PokeRoutineType type, uint priority, PokeTradeResult result, CancellationToken token)
    {
        detail.IsProcessing = false;

        // Always remove from UsersInQueue on abort
        Hub.Queues.Info.Remove(new TradeEntry<PA8>(detail, detail.Trainer.ID, type, detail.Trainer.TrainerName, detail.UniqueTradeID));

        if (detail.TotalBatchTrades > 1)
        {
            // Release the batch claim on failure
            BatchTracker.ReleaseBatch(detail.Trainer.ID, detail.UniqueTradeID);

            if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
            {
                detail.IsRetry = true;
                Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
                detail.SendNotification(this, "糟糕！批量交易发生异常，我会重新为你排队再试一次。");
            }
            else
            {
                detail.SendNotification(this, $"批量交易失败：{result}");
                detail.TradeCanceled(this, result);
                await ExitTrade(false, token).ConfigureAwait(false);
            }
        }
        else
        {
            HandleAbortedTrade(detail, type, priority, result);
        }
    }

    private void HandleAbortedTrade(PokeTradeDetail<PA8> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
    {
        detail.IsProcessing = false;
        if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
        {
            detail.IsRetry = true;
            Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
            detail.SendNotification(this, "糟糕！发生了异常，我会重新为你排队再试一次。");
        }
        else
        {
            detail.SendNotification(this, $"糟糕！发生了异常。正在取消本次交易：{result}。");
            detail.TradeCanceled(this, result);
        }
    }

    private async Task<PokeTradeResult> PerformBatchTrade(SAV8LA sav, PokeTradeDetail<PA8> poke, CancellationToken token)
    {
        int completedTrades = 0;
        var startingDetail = poke;
        var originalTrainerID = startingDetail.Trainer.ID;

        var tradesToProcess = poke.BatchTrades ?? [poke.TradeData];
        var totalBatchTrades = tradesToProcess.Count;

        void SendCollectedPokemonAndCleanup()
        {
            var allReceived = BatchTracker.GetReceivedPokemon(originalTrainerID);
            if (allReceived.Count > 0)
            {
                poke.SendNotification(this, $"发送你中断前交易给我的 {allReceived.Count} 只宝可梦。");

                Log($"正在返回给训练家 {originalTrainerID} 的 {allReceived.Count} 只宝可梦。");

                // Send each Pokemon directly instead of calling TradeFinished
                for (int j = 0; j < allReceived.Count; j++)
                {
                    var pokemon = allReceived[j];
                    var speciesName = SpeciesName.GetSpeciesName(pokemon.Species, 2);
                    Log($"  - 正在返回：{speciesName}（校验和：{pokemon.Checksum:X8}）");

                    // Send the Pokemon directly to the notifier
                    poke.SendNotification(this, pokemon, $"宝可梦你交易给我的：{speciesName}");
                    Thread.Sleep(500);
                }
            }
            else
            {
                Log($"未找到要返回给训练家 {originalTrainerID} 的宝可梦。");
            }

            BatchTracker.ClearReceivedPokemon(originalTrainerID);
            BatchTracker.ReleaseBatch(originalTrainerID, startingDetail.UniqueTradeID);
            poke.IsProcessing = false;
            Hub.Queues.Info.Remove(new TradeEntry<PA8>(poke, originalTrainerID, PokeRoutineType.Batch, poke.Trainer.TrainerName, poke.UniqueTradeID));
        }

        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        Hub.Config.Stream.EndEnterCode(this);

        if (await CheckIfSoftBanned(SoftBanOffset, token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            SendCollectedPokemonAndCleanup();
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }

        Log("正在与 Simona 对话以开始交易。");
        await Click(A, 1_000, token).ConfigureAwait(false);
        await Click(A, 0_600, token).ConfigureAwait(false);
        await Click(A, 1_500, token).ConfigureAwait(false);

        Log("正在选择连线交换。");
        await Click(DRIGHT, 0_500, token).ConfigureAwait(false);
        await Click(A, 1_500, token).ConfigureAwait(false);
        await Click(A, 2_000, token).ConfigureAwait(false);

        if (poke.Type != PokeTradeType.Random)
            Hub.Config.Stream.StartEnterCode(this);
        await Task.Delay(Hub.Config.Timings.ExtraTimeOpenCodeEntry, token).ConfigureAwait(false);

        var code = poke.Code;
        Log($"正在输入连线交换密码：{code:0000 0000}...");
        await EnterLinkCode(code, Hub.Config, token).ConfigureAwait(false);

        WaitAtBarrierIfApplicable(token);
        await Click(PLUS, 1_000, token).ConfigureAwait(false);

        // Cache trade partner info after first successful connection
        TradePartnerLA? cachedTradePartner = null;

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
                poke.SendNotification(this, $"交易 {completedTrades} 已完成！**请勿立即提供宝可梦** - 正在准备你的下一只宝可梦（{completedTrades + 1}/{totalBatchTrades}）...");

                // Wait for trade animation to fully complete
                await Task.Delay(5_000, token).ConfigureAwait(false);

                // Prepare the next Pokemon with AutoOT if needed
                if (toSend.Species != 0)
                {
                    if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT && cachedTradePartner != null)
                    {
                        toSend = await ApplyAutoOT(toSend, cachedTradePartner, sav, token);
                        tradesToProcess[currentTradeIndex] = toSend; // Update the list
                    }
                    await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                }

                // Give time for the Pokemon to be properly set
                await Task.Delay(1_000, token).ConfigureAwait(false);

                // NOW tell the user they can offer
                poke.SendNotification(this, $"**准备就绪！** 你现在可以提供你的宝可梦进行第 {currentTradeIndex + 1}/{totalBatchTrades} 次交易。");

                // Additional delay to ensure we're ready to detect offers
                await Task.Delay(2_000, token).ConfigureAwait(false);
            }

            // For first trade only - search for partner
            if (currentTradeIndex == 0)
            {
                poke.TradeSearching(this);

                var partnerFound = await WaitForTradePartner(token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                {
                    if (startingDetail.TotalBatchTrades > 1)
                        poke.SendNotification(this, "批量交易已取消。流程被中断。");
                    SendCollectedPokemonAndCleanup();
                    await ExitTrade(false, token).ConfigureAwait(false);
                    return PokeTradeResult.RoutineCancel;
                }

                if (!partnerFound)
                {
                    if (startingDetail.TotalBatchTrades > 1)
                        poke.SendNotification(this, "未找到交易对象。正在取消批量交易。");
                    else
                        poke.SendNotification(this, "未找到交易对象。正在取消本次交易。");
                    SendCollectedPokemonAndCleanup();
                    await ExitTrade(false, token).ConfigureAwait(false);
                    return PokeTradeResult.NoTrainerFound;
                }

                Hub.Config.Stream.EndEnterCode(this);

                await Task.Delay(1_000 + Hub.Config.Timings.ExtraTimeOpenBox, token).ConfigureAwait(false);

                var tradePartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
                var trainerNID = await GetTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);
                tradePartner.NID = trainerNID;
                cachedTradePartner = tradePartner; // Cache for subsequent trades
                RecordUtil<PokeTradeBotLA>.Record($"开始\t{trainerNID:X16}\t{tradePartner.TrainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");

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
                }

                var partnerCheck = CheckPartnerReputation(this, poke, trainerNID, tradePartner.TrainerName, AbuseSettings, token);
                if (partnerCheck != PokeTradeResult.Success)
                {
                    if (startingDetail.TotalBatchTrades > 1)
                        poke.SendNotification(this, "交易对象检查失败。正在取消批量交易。");
                    SendCollectedPokemonAndCleanup();
                    await ExitTrade(false, token).ConfigureAwait(false);
                    return partnerCheck;
                }

                Log($"已找到连线交换对象：{tradePartner.TrainerName}-{tradePartner.TID7}（ID：{trainerNID}）");
                poke.SendNotification(this, $"已找到连线交换对象：{tradePartner.TrainerName}。**TID**：{tradePartner.TID7} **SID**：{tradePartner.SID7}");

                // Apply AutoOT for first trade if needed
                if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
                {
                    toSend = await ApplyAutoOT(toSend, tradePartner, sav, token);
                    poke.TradeData = toSend;
                    if (toSend.Species != 0)
                        await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
                }
            }

            // Wait for user to offer a Pokemon
            if (currentTradeIndex == 0)
            {
                poke.SendNotification(this, $"请提供你的宝可梦进行第 1/{totalBatchTrades} 次交易。");
            }

            var offering = await ReadUntilChanged(TradePartnerOfferedOffset, [0x3], 25_000, 1_000, true, true, token).ConfigureAwait(false);
            if (!offering)
            {
                if (startingDetail.TotalBatchTrades > 1)
                    poke.SendNotification(this, $"在第 {currentTradeIndex + 1}/{totalBatchTrades} 次交易后交易伙伴过慢。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            PA8? offered = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 3_000, 0_050, BoxFormatSlotSize, token).ConfigureAwait(false);

            if (offered == null)
            {
                Log("无法读取对方提供的宝可梦，但将继续交易。");
                offered = new PA8();
            }

            // Get trade partner info for subsequent trades (already have it for first trade)
            var trainer = new PartnerDataHolder(0, "", "");
            if (cachedTradePartner != null)
            {
                trainer = new PartnerDataHolder(0, cachedTradePartner.TrainerName, cachedTradePartner.TID7);
            }

            (toSend, PokeTradeResult update) = await GetEntityToSend(sav, poke, offered, toSend, trainer, token).ConfigureAwait(false);
            if (update != PokeTradeResult.Success)
            {
                if (startingDetail.TotalBatchTrades > 1)
                    poke.SendNotification(this, $"在第 {currentTradeIndex + 1}/{totalBatchTrades} 次交易后更新检查失败。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return update;
            }

            Log($"正在确认第 {currentTradeIndex + 1}/{totalBatchTrades} 次交易。");
            var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
            if (tradeResult != PokeTradeResult.Success)
            {
                if (startingDetail.TotalBatchTrades > 1)
                    poke.SendNotification(this, $"在第 {currentTradeIndex + 1}/{totalBatchTrades} 次交易后交易确认失败。正在取消剩余交易。");
                if (tradeResult == PokeTradeResult.TrainerLeft)
                    Log("交易因对方离开而被取消。");
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return tradeResult;
            }

            if (token.IsCancellationRequested)
            {
                if (startingDetail.TotalBatchTrades > 1)
                    poke.SendNotification(this, "剩余批量交易已取消。流程被中断。");
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.RoutineCancel;
            }

            var received = await ReadPokemon(BoxStartOffset, BoxFormatSlotSize, token).ConfigureAwait(false);

            if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
            {
                if (startingDetail.TotalBatchTrades > 1)
                    poke.SendNotification(this, $"在第 {currentTradeIndex + 1}/{totalBatchTrades} 次交易后交易伙伴未完成。正在取消剩余交易。");
                Log("玩家未完成交换。");
                SendCollectedPokemonAndCleanup();
                await ExitTrade(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            Log("玩家已完成交换。");
            UpdateCountsAndExport(poke, received, toSend);
            LogSuccessfulTrades(poke, cachedTradePartner?.NID ?? 0, cachedTradePartner?.TrainerName ?? "未知");

            BatchTracker.AddReceivedPokemon(originalTrainerID, received);
            completedTrades = currentTradeIndex + 1;
            Log($"已将接收的宝可梦 {received.Species}（校验和：{received.Checksum:X8}）加入训练家 {originalTrainerID} 的批量记录（交易 {completedTrades}/{totalBatchTrades}）。");

            if (completedTrades == totalBatchTrades)
            {
                // Get all collected Pokemon before cleaning anything up
                var allReceived = BatchTracker.GetReceivedPokemon(originalTrainerID);
                Log($"批量交易完成。为训练家 {originalTrainerID} 存储了 {allReceived.Count} 只宝可梦。");

                // First send notification that trades are complete
                poke.SendNotification(this, "所有批量交易已完成！感谢你的交易！");

                // Send back all received Pokemon if ReturnPKMs is enabled
                if (Hub.Config.Discord.ReturnPKMs && allReceived.Count > 0)
                {
                    poke.SendNotification(this, $"这是你交易给我的 {allReceived.Count} 只宝可梦：");

                    // Send each Pokemon directly instead of calling TradeFinished
                    for (int j = 0; j < allReceived.Count; j++)
                    {
                        var pokemon = allReceived[j];
                        var speciesName = SpeciesName.GetSpeciesName(pokemon.Species, 2);
                        Log($"  - 正在返回：{speciesName}（校验和：{pokemon.Checksum:X8}）");

                        // Send the Pokemon directly to the notifier
                        poke.SendNotification(this, pokemon, $"宝可梦你交易给我的：{speciesName}");
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
                await ExitTrade(false, token).ConfigureAwait(false);
                poke.IsProcessing = false;
                break;
            }
        }

        // Ensure we exit properly even if the loop breaks unexpectedly
        await ExitTrade(false, token).ConfigureAwait(false);
        poke.IsProcessing = false;
        return PokeTradeResult.Success;
    }

    private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV8LA sav, PokeTradeDetail<PA8> poke, CancellationToken token)
    {
        // Check if trade was canceled by user
        if (poke.IsCanceled)
        {
            Log($"训练家 {poke.Trainer.TrainerName} 取消了此次交易。");
            poke.TradeCanceled(this, PokeTradeResult.UserCanceled);
            return PokeTradeResult.UserCanceled;
        }

        // Update Barrier Settings
        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        Hub.Config.Stream.EndEnterCode(this);

        if (await CheckIfSoftBanned(SoftBanOffset, token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        var toSend = poke.TradeData;
        if (toSend.Species != 0)
            await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);

        if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }

        // Speak to the NPC to start a trade.
        Log("正在与 Simona 对话以开始交易。");
        await Click(A, 1_000, token).ConfigureAwait(false);
        await Click(A, 0_600, token).ConfigureAwait(false);
        await Click(A, 1_500, token).ConfigureAwait(false);

        Log("正在选择连线交换。");
        await Click(DRIGHT, 0_500, token).ConfigureAwait(false);
        await Click(A, 1_500, token).ConfigureAwait(false);
        await Click(A, 2_000, token).ConfigureAwait(false);

        // Loading code entry.
        if (poke.Type != PokeTradeType.Random)
            Hub.Config.Stream.StartEnterCode(this);
        await Task.Delay(Hub.Config.Timings.ExtraTimeOpenCodeEntry, token).ConfigureAwait(false);

        var code = poke.Code;
        Log($"正在输入连线交换密码：{code:0000 0000}...");
        await EnterLinkCode(code, Hub.Config, token).ConfigureAwait(false);

        // Wait for Barrier to trigger all bots simultaneously.
        WaitAtBarrierIfApplicable(token);
        await Click(PLUS, 1_000, token).ConfigureAwait(false);

        poke.TradeSearching(this);

        // Wait for a Trainer...
        var partnerFound = await WaitForTradePartner(token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.RoutineCancel;
        }
        if (!partnerFound)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.NoTrainerFound;
        }

        Hub.Config.Stream.EndEnterCode(this);

        // Some more time to fully enter the trade.
        await Task.Delay(1_000 + Hub.Config.Timings.ExtraTimeOpenBox, token).ConfigureAwait(false);

        var tradePartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
        var trainerNID = await GetTradePartnerNID(TradePartnerNIDOffset, token).ConfigureAwait(false);
        tradePartner.NID = trainerNID;
        RecordUtil<PokeTradeBotLA>.Record($"开始\t{trainerNID:X16}\t{tradePartner.TrainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");
        Log($"已找到连线交换对象：{tradePartner.TrainerName}-{tradePartner.TID7}（ID：{trainerNID}）");
        poke.SendNotification(this, $"已找到连线交换对象：{tradePartner.TrainerName}。**TID**：{tradePartner.TID7} **SID**：{tradePartner.SID7}。等待对方提供宝可梦…");

        var tradeCodeStorage = new TradeCodeStorage();
        var existingTradeDetails = tradeCodeStorage.GetTradeDetails(poke.Trainer.ID);

        bool shouldUpdateOT = existingTradeDetails?.OT != tradePartner.TrainerName;
        bool shouldUpdateTID = existingTradeDetails?.TID != int.Parse(tradePartner.TID7);
        bool shouldUpdateSID = existingTradeDetails?.SID != int.Parse(tradePartner.SID7);

        if (shouldUpdateOT || shouldUpdateTID || shouldUpdateSID)
        {
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, shouldUpdateOT ? tradePartner.TrainerName : existingTradeDetails.OT, shouldUpdateTID ? int.Parse(tradePartner.TID7) : existingTradeDetails.TID, shouldUpdateSID ? int.Parse(tradePartner.SID7) : existingTradeDetails.SID);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8604 // Possible null reference argument.
        }

        var partnerCheck = CheckPartnerReputation(this, poke, trainerNID, tradePartner.TrainerName, AbuseSettings, token);
        if (partnerCheck != PokeTradeResult.Success)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return partnerCheck;
        }

        if (poke.Type == PokeTradeType.Dump)
        {
            var result = await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);
            await ExitTrade(false, token).ConfigureAwait(false);
            return result;
        }

        // Watch their status to indicate they have offered a Pokémon as well.
        var offering = await ReadUntilChanged(TradePartnerOfferedOffset, [0x3], 25_000, 1_000, true, true, token).ConfigureAwait(false);
        if (!offering)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        /* Removing this as of PKHeX 5.18.25 which broke functionality of reading Alphas
        Log("Checking offered Pokémon.");

        // If we got to here, we can read their offered Pokémon.

        // Wait for user input... Needs to be different from the previously offered Pokémon.
        var offered = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 3_000, 0_050, BoxFormatSlotSize, token).ConfigureAwait(false);
        if (offered == null || offered.Species == 0 || !offered.ChecksumValid)
        {
            Log("Trade ended because trainer offer was rescinded too quickly.");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerOfferCanceledQuick;
        }
        */

        // Try to read the offered Pokémon but continue regardless of result
        PA8? offered = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 3_000, 0_050, BoxFormatSlotSize, token).ConfigureAwait(false);

        // Continue with the trade even if null or invalid
        if (offered == null)
        {
            Log("无法读取对方提供的宝可梦，但将继续交易。");
            offered = new PA8(); // Create empty PA8 to avoid null reference exceptions
        }

        var trainer = new PartnerDataHolder(0, tradePartner.TrainerName, tradePartner.TID7);
        (toSend, PokeTradeResult update) = await GetEntityToSend(sav, poke, offered, toSend, trainer, token).ConfigureAwait(false);
        if (update != PokeTradeResult.Success)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return update;
        }

        if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
        {
            toSend = await ApplyAutoOT(toSend, tradePartner, sav, token);
        }

        Log("正在确认交易。");
        var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
        if (tradeResult != PokeTradeResult.Success)
        {
            if (tradeResult == PokeTradeResult.TrainerLeft)
                Log("交易因对方离开而被取消。");
            await ExitTrade(false, token).ConfigureAwait(false);
            return tradeResult;
        }

        if (token.IsCancellationRequested)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.RoutineCancel;
        }

        // Trade was Successful!
        var received = await ReadPokemon(BoxStartOffset, BoxFormatSlotSize, token).ConfigureAwait(false);

        // Pokémon in b1s1 is same as the one they were supposed to receive (was never sent).
        if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
        {
            Log("玩家未完成交换。");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        // As long as we got rid of our inject in b1s1, assume the trade went through.
        Log("玩家已完成交换。");
        poke.TradeFinished(this, received);

        // Only log if we completed the trade.
        UpdateCountsAndExport(poke, received, toSend);

        // Log for Trade Abuse tracking.
            LogSuccessfulTrades(poke, trainerNID, tradePartner.TrainerName);

        await ExitTrade(false, token).ConfigureAwait(false);
        return PokeTradeResult.Success;
    }

    private void UpdateCountsAndExport(PokeTradeDetail<PA8> poke, PA8 received, PA8 toSend)
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
            if (poke.Type is PokeTradeType.Specific or PokeTradeType.Clone or PokeTradeType.FixOT)
                DumpPokemon(DumpSetting.DumpFolder, tradedFolder, toSend); // sent to partner
        }
    }

    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PA8> detail, CancellationToken token)
    {
        // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
        var oldEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);

        await Click(A, 3_000, token).ConfigureAwait(false);
        for (int i = 0; i < Hub.Config.Trade.TradeConfiguration.MaxTradeConfirmTime; i++)
        {
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return PokeTradeResult.TrainerLeft;
            if (await IsUserBeingShifty(detail, token).ConfigureAwait(false))
                return PokeTradeResult.SuspiciousActivity;
            await Click(A, 1_000, token).ConfigureAwait(false);

            // EC is detectable at the start of the animation.
            var newEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);
            if (!newEC.SequenceEqual(oldEC))
            {
                await Task.Delay(30_000, token).ConfigureAwait(false);
                return PokeTradeResult.Success;
            }
        }
        if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            return PokeTradeResult.TrainerLeft;

        // If we don't detect a B1S1 change, the trade didn't go through in that time.
        return PokeTradeResult.TrainerTooSlow;
    }

    protected virtual async Task<bool> WaitForTradePartner(CancellationToken token)
    {
        Log("正在等待交易伙伴...");
        int ctr = (Hub.Config.Trade.TradeConfiguration.TradeWaitTime * 1_000) - 2_000;
        await Task.Delay(2_000, token).ConfigureAwait(false);
        while (ctr > 0)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            var (valid, offset) = await ValidatePointerAll(Offsets.TradePartnerStatusPointer, token).ConfigureAwait(false);
            ctr -= 1_000;
            if (!valid)
                continue;
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 4, token).ConfigureAwait(false);
            if (BitConverter.ToInt32(data, 0) != 2)
                continue;
            TradePartnerOfferedOffset = offset;
            return true;
        }
        return false;
    }

    private async Task ExitTrade(bool unexpected, CancellationToken token)
    {
        if (unexpected)
            Log("检测到异常行为，正在恢复位置。");

        int ctr = 120_000;
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
        {
            if (ctr < 0)
            {
                await RestartGameLA(token).ConfigureAwait(false);
                return;
            }

            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return;

            var (valid, _) = await ValidatePointerAll(Offsets.TradePartnerStatusPointer, token).ConfigureAwait(false);
            await Click(valid ? A : B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return;

            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                return;

            ctr -= 3_000;
        }
    }

    // These don't change per session, and we access them frequently, so set these each time we start.
    private async Task InitializeSessionOffsets(CancellationToken token)
    {
        Log("正在缓存会话偏移...");
        BoxStartOffset = await SwitchConnection.PointerAll(Offsets.BoxStartPokemonPointer, token).ConfigureAwait(false);
        SoftBanOffset = await SwitchConnection.PointerAll(Offsets.SoftbanPointer, token).ConfigureAwait(false);
        OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
        TradePartnerNIDOffset = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerNIDPointer, token).ConfigureAwait(false);
    }

    // todo: future
    protected virtual async Task<bool> IsUserBeingShifty(PokeTradeDetail<PA8> detail, CancellationToken token)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return false;
    }

    private async Task RestartGameLA(CancellationToken token)
    {
        await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
        await InitializeSessionOffsets(token).ConfigureAwait(false);
    }

    private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PA8> detail, CancellationToken token)
    {
        int ctr = 0;
        var time = TimeSpan.FromSeconds(Hub.Config.Trade.TradeConfiguration.MaxDumpTradeTime);
        var start = DateTime.Now;

        var pkprev = new PA8();
        var bctr = 0;
        while (ctr < Hub.Config.Trade.TradeConfiguration.MaxDumpsPerTrade && DateTime.Now - start < time)
        {
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                break;
            if (bctr++ % 3 == 0)
                await Click(B, 0_100, token).ConfigureAwait(false);

            // Wait for user input... Needs to be different from the previously offered Pokémon.
            var pk = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 3_000, 0_050, BoxFormatSlotSize, token).ConfigureAwait(false);
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
            Log($"展示的宝可梦合法性：{(la.Valid ? "合法" : "不合法")}。");

            ctr++;
            var msg = Hub.Config.Trade.TradeConfiguration.DumpTradeLegalityCheck ? verbose : $"文件 {ctr}";

            // Extra information about trainer data for people requesting with their own trainer data.
            var ot = pk.OriginalTrainerName;
            var ot_gender = pk.OriginalTrainerGender == 0 ? "男" : "女";
            var tid = pk.GetDisplayTID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringTID());
            var sid = pk.GetDisplaySID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringSID());
            msg += $"\n**训练家数据**\n```OT：{ot}\n训练家性别：{ot_gender}\nTID：{tid}\nSID：{sid}```";

            msg += pk.IsShiny ? "\n**该宝可梦为闪光！**" : string.Empty;
            detail.SendNotification(this, pk, msg);
        }

        Log($"在处理 {ctr} 只宝可梦后结束导出循环。");
        if (ctr == 0)
            return PokeTradeResult.TrainerTooSlow;

        TradeSettings.CountStatsSettings.AddCompletedDumps();
        detail.Notifier.SendNotification(this, detail, $"已导出 {ctr} 只宝可梦。");
        detail.Notifier.TradeFinished(this, detail, detail.TradeData); // blank PA8
        return PokeTradeResult.Success;
    }

    private async Task<TradePartnerLA> GetTradePartnerInfo(CancellationToken token)
    {
        var id = await SwitchConnection.PointerPeek(4, Offsets.LinkTradePartnerTIDPointer, token).ConfigureAwait(false);
        var name = await SwitchConnection.PointerPeek(TradePartnerLA.MaxByteLengthStringObject, Offsets.LinkTradePartnerNamePointer, token).ConfigureAwait(false);
        var traderOffset = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerTIDPointer, token).ConfigureAwait(false);
        var idbytes = await SwitchConnection.ReadBytesAbsoluteAsync(traderOffset + 0x04, 4, token).ConfigureAwait(false);

        return new TradePartnerLA(id, name, idbytes);
    }

    protected virtual async Task<(PA8 toSend, PokeTradeResult check)> GetEntityToSend(SAV8LA sav, PokeTradeDetail<PA8> poke, PA8 offered, PA8 toSend, PartnerDataHolder partnerID, CancellationToken token)
    {
        return poke.Type switch
        {
            PokeTradeType.Random => await HandleRandomLedy(sav, poke, offered, toSend, partnerID, token).ConfigureAwait(false),
            PokeTradeType.Clone => await HandleClone(sav, poke, offered, token).ConfigureAwait(false),
            PokeTradeType.FixOT => await HandleFixOT(sav, poke, offered, partnerID, token).ConfigureAwait(false),
            _ => (toSend, PokeTradeResult.Success),
        };
    }

    private async Task<(PA8 toSend, PokeTradeResult check)> HandleClone(SAV8LA sav, PokeTradeDetail<PA8> poke, PA8 offered, CancellationToken token)
    {
        if (Hub.Config.Discord.ReturnPKMs)
            poke.SendNotification(this, offered, $"这是你展示的宝可梦 - {GameInfo.GetStrings("zh").Species[offered.Species]}");

        var la = new LegalityAnalysis(offered);
        if (!la.Valid)
        {
            Log($"来自 {poke.Trainer.TrainerName} 的克隆请求检测到不合法的宝可梦：{GameInfo.GetStrings("zh").Species[offered.Species]}。");
            if (DumpSetting.Dump)
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);

            var report = la.Report();
            Log(report);
            poke.SendNotification(this, "根据 PKHeX 的合法性检测，此宝可梦不合法，无法进行克隆。即将结束交易。");
            poke.SendNotification(this, report);

            return (offered, PokeTradeResult.IllegalTrade);
        }

        var clone = offered.Clone();
        if (Hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        poke.SendNotification(this, $"**克隆了你的 {GameInfo.GetStrings("zh").Species[clone.Species]}！**\n现在按 B 取消你的宝可梦并交换一只你不想要的宝可梦给我。");
        Log($"已克隆 {(Species)clone.Species}。等待用户更换宝可梦...");

        if (!await CheckCloneChangedOffer(token).ConfigureAwait(false))
        {
            // They get one more chance.
            poke.SendNotification(this, "**快换掉当前宝可梦，否则我就要离开了！**");
            if (!await CheckCloneChangedOffer(token).ConfigureAwait(false))
            {
                Log("交易伙伴没有更换宝可梦。");
                return (offered, PokeTradeResult.TrainerTooSlow);
            }
        }

        // If we got to here, we can read their offered Pokémon.
        var pk2 = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 5_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
        if (pk2 is null || SearchUtil.HashByDetails(pk2) == SearchUtil.HashByDetails(offered))
        {
            Log("交易伙伴没有更换宝可梦。");
            return (offered, PokeTradeResult.TrainerTooSlow);
        }

        await SetBoxPokemonAbsolute(BoxStartOffset, clone, token, sav).ConfigureAwait(false);

        return (clone, PokeTradeResult.Success);
    }

    private async Task<bool> CheckCloneChangedOffer(CancellationToken token)
    {
        // Watch their status to indicate they canceled, then offered a new Pokémon.
        var hovering = await ReadUntilChanged(TradePartnerOfferedOffset, [0x2], 25_000, 1_000, true, true, token).ConfigureAwait(false);
        if (!hovering)
        {
            Log("交易伙伴没有更换最初的提供。");
            await ExitTrade(false, token).ConfigureAwait(false);
            return false;
        }
        var offering = await ReadUntilChanged(TradePartnerOfferedOffset, [0x3], 25_000, 1_000, true, true, token).ConfigureAwait(false);
        if (!offering)
        {
            await ExitTrade(false, token).ConfigureAwait(false);
            return false;
        }
        return true;
    }

    private async Task<(PA8 toSend, PokeTradeResult check)> HandleRandomLedy(SAV8LA sav, PokeTradeDetail<PA8> poke, PA8 offered, PA8 toSend, PartnerDataHolder partner, CancellationToken token)
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
                    msg += $"\nID：{partner.TrainerOnlineID}";
                if (!string.IsNullOrWhiteSpace(AbuseSettings.LedyAbuseEchoMention))
                    msg = $"{AbuseSettings.LedyAbuseEchoMention} {msg}";
                EchoUtil.Echo(msg);

                return (toSend, PokeTradeResult.SuspiciousActivity);
            }

            toSend = trade.Receive;
            poke.TradeData = toSend;

            poke.SendNotification(this, "正在注入你请求的宝可梦。");
            await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
        }
        else if (config.LedyQuitIfNoMatch)
        {
            var nickname = offered.IsNicknamed ? $"（昵称：\"{offered.Nickname}\"）" : string.Empty;
            poke.SendNotification(this, $"未找到与所提供的 {GameInfo.GetStrings("zh").Species[offered.Species]}{nickname} 对应的请求。");
            return (toSend, PokeTradeResult.TrainerRequestBad);
        }

        return (toSend, PokeTradeResult.Success);
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
        Log($"屏障同步在 {timeoutAfter} 秒后超时。继续。");
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
            Log($"已加入屏障。计数：{Hub.BotSync.Barrier.ParticipantCount}");
        }
        else
        {
            Hub.BotSync.Barrier.RemoveParticipant();
            Log($"已离开屏障。计数：{Hub.BotSync.Barrier.ParticipantCount}");
        }
    }

    private async Task<(PA8 toSend, PokeTradeResult check)> HandleFixOT(SAV8LA sav, PokeTradeDetail<PA8> poke, PA8 offered, PartnerDataHolder partner, CancellationToken token)
    {
        var adOT = TradeExtensions<PA8>.HasAdName(offered, out _);
        var laInit = new LegalityAnalysis(offered);
        if (!adOT && laInit.Valid)
        {
            poke.SendNotification(this, "未在昵称或 OT 中检测到广告信息，且宝可梦合法。结束交易。");
            return (offered, PokeTradeResult.TrainerRequestBad);
        }

        var clone = (PA8)offered.Clone();
        if (Hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        string shiny = string.Empty;
        if (!TradeExtensions<PA8>.ShinyLockCheck(offered.Species, TradeExtensions<PA8>.FormOutput(offered.Species, offered.Form, out _), $"{(Ball)offered.Ball}"))
            shiny = $"\n闪光：{(offered.ShinyXor == 0 ? "方块闪" : offered.IsShiny ? "星星闪" : "否")}";
        else shiny = "\n闪光：否";

        var name = partner.TrainerName;
        var ball = $"\n精灵球：{(Ball)offered.Ball}";
        var extraInfo = $"OT：{name}{ball}{shiny}";
        var set = ShowdownParsing.GetShowdownText(offered).Split('\n').ToList();
        set.Remove(set.Find(x => x.Contains("Shiny")) ?? "");
        set.InsertRange(1, extraInfo.Split('\n'));

        if (!laInit.Valid)
        {
            Log($"FixOT 请求检测到来自 {name} 的不合法宝可梦：{(Species)offered.Species}");
            var report = laInit.Report();
            Log(laInit.Report());
            poke.SendNotification(this, $"**展示的宝可梦不合法，正在尝试重新生成…**\n\n```{report}```");
            if (DumpSetting.Dump)
                DumpPokemon(DumpSetting.DumpFolder, "hacked", offered);
        }

        if (clone.FatefulEncounter)
        {
            clone.SetDefaultNickname(laInit);
            var info = new SimpleTrainerInfo { Gender = clone.OriginalTrainerGender, Language = clone.Language, OT = name, TID16 = clone.TID16, SID16 = clone.SID16, Generation = 8 };
            var mg = EncounterEvent.GetAllEvents().Where(x => x.Species == clone.Species && x.Form == clone.Form && x.IsShiny == clone.IsShiny && x.OriginalTrainerName == clone.OriginalTrainerName).ToList();
            if (mg.Count > 0)
                clone = TradeExtensions<PA8>.CherishHandler(mg.First(), info);
            else clone = (PA8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }
        else
        {
            clone = (PA8)sav.GetLegal(AutoLegalityWrapper.GetTemplate(new ShowdownSet(string.Join("\n", set))), out _);
        }

        var la = new LegalityAnalysis(clone);
        clone = (PA8)TradeExtensions<PA8>.TrashBytes(clone, la);
        clone.ResetPartyStats();

        la = new LegalityAnalysis(clone);
        if (!la.Valid)
        {
            poke.SendNotification(this, "根据 PKHeX 的合法性检测，此宝可梦仍然不合法，无法修复。结束交易。");
            return (clone, PokeTradeResult.IllegalTrade);
        }

        TradeExtensions<PA8>.HasAdName(offered, out string detectedAd);
        var actionText = !laInit.Valid ? "**已合法化" : "**已修正昵称/OT 的";
        poke.SendNotification(this, $"{actionText} {(Species)clone.Species}**（检测到广告：{detectedAd}）！请立即确认交易！");
        Log($"{(!laInit.Valid ? "已合法化" : "已修正昵称/OT")} {(Species)clone.Species}！");

        if (await CheckCloneChangedOffer(token).ConfigureAwait(false))
        {
            // They get one more chance.
            poke.SendNotification(this, "**请发送最初展示的那只宝可梦，否则我就要离开了！**");
            Log($"{name} 更换了提供的宝可梦。");

            if (!await CheckCloneChangedOffer(token).ConfigureAwait(false))
            {
                Log("交易伙伴更换了提供的宝可梦。");
                return (offered, PokeTradeResult.TrainerTooSlow);
            }
        }

        await SetBoxPokemonAbsolute(BoxStartOffset, clone, token, sav).ConfigureAwait(false);
        return (clone, PokeTradeResult.Success);
    }

    // based on https://github.com/Muchacho13Scripts/SysBot.NET/commit/f7879386f33bcdbd95c7a56e7add897273867106
    // and https://github.com/berichan/SysBot.PLA/commit/84042d4716007dc6ff3100ad4be4a483d622ccf8
    private async Task<PA8> ApplyAutoOT(PA8 toSend, TradePartnerLA tradePartner, SAV8LA sav, CancellationToken token)
    {
        // Special handling for Pokémon GO
        if (toSend.Version == GameVersion.GO)
        {
            var goClone = toSend.Clone();
            goClone.OriginalTrainerName = tradePartner.TrainerName;

            // Update OT trash to match the new OT name
            ClearOTTrash(goClone, tradePartner.TrainerName);

            if (!toSend.ChecksumValid)
                goClone.RefreshChecksum();

            Log("已为 GO 来源的宝可梦仅应用 OT 名称。");
            await SetBoxPokemonAbsolute(BoxStartOffset, goClone, token, sav).ConfigureAwait(false);
            return goClone;
        }

        if (toSend is IHomeTrack pk && pk.HasTracker)
        {
            Log("检测到 HOME 追踪器，无法应用自动 OT。");
            return toSend;
        }

        // Current handler cannot be past gen OT
        if (toSend.Generation != toSend.Format)
        {
            Log("无法应用交易对象信息：当前持有者与原世代不符。");
            return toSend;
        }

        // Check if the Pokémon is from a Mystery Gift
        bool isMysteryGift = toSend.FatefulEncounter;
        var cln = toSend.Clone();

        if (isMysteryGift)
        {
            Log("检测到神秘礼物。仅应用 OT 信息并保留语言设置。");
            // Only set OT-related info for Mystery Gifts without preset OT/TID/SID
            cln.OriginalTrainerGender = tradePartner.Gender;
            cln.TrainerTID7 = uint.Parse(tradePartner.TID7);
            cln.TrainerSID7 = uint.Parse(tradePartner.SID7);
            cln.OriginalTrainerName = tradePartner.TrainerName;
        }
        else
        {
            // Apply all trade partner details for non-Mystery Gift Pokémon
            cln.OriginalTrainerGender = tradePartner.Gender;
            cln.TrainerTID7 = uint.Parse(tradePartner.TID7);
            cln.TrainerSID7 = uint.Parse(tradePartner.SID7);
            cln.Language = tradePartner.Language;
            cln.OriginalTrainerName = tradePartner.TrainerName;
        }

        ClearOTTrash(cln, tradePartner.TrainerName);

        if (!toSend.IsNicknamed)
            cln.ClearNickname();

        if (toSend.IsShiny)
            cln.PID = (uint)((cln.TID16 ^ cln.SID16 ^ (cln.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (cln.PID & 0xFFFF);

        if (!toSend.ChecksumValid)
            cln.RefreshChecksum();

        var tradela = new LegalityAnalysis(cln);
        if (tradela.Valid)
        {
            Log("宝可梦合法，正在应用自动 OT。");
            await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
            return cln;
        }
        else
        {
            Log("宝可梦不合法，无法应用自动 OT。");
            return toSend;
        }
    }

    private static void ClearOTTrash(PA8 pokemon, string trainerName)
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
