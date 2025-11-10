using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsLGPE;

namespace SysBot.Pokemon;

public class PokeTradeBotLGPE(PokeTradeHub<PB7> Hub, PokeBotState Config) : PokeRoutineExecutor7LGPE(Config), ICountBot, ITradeBot
{
    private readonly TradeSettings TradeSettings = Hub.Config.Trade;

    public readonly TradeAbuseSettings AbuseSettings = Hub.Config.TradeAbuse;

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

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

            Log("正在识别主机的训练家数据。");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            RecentTrainerCache.SetRecentTrainer(sav);

            OnConnectionSuccess();
            Log($"正在启动 {nameof(PokeTradeBotLGPE)} 主循环。");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            OnConnectionError(e);
            throw;
        }

        Log($"结束 {nameof(PokeTradeBotLGPE)} 循环。");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task HardStop()
    {
        UpdateBarrier(false);
        await CleanExit(TradeSettings, CancellationToken.None).ConfigureAwait(false);
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

    public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
    {
        Log("检测到错误，正在重新启动游戏！！");
        await CloseGame(config, token).ConfigureAwait(false);
        await StartGame(config, token).ConfigureAwait(false);
    }

    private async Task InnerLoop(SAV7b sav, CancellationToken token)
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
                Log(e.Message);
                break;
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

    private async Task DoTrades(SAV7b sav, CancellationToken token)
    {
        var type = Config.CurrentRoutineType;
        int waitCounter = 0;
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

    private async Task WaitForQueueStep(int waitCounter, CancellationToken token)
    {
        if (waitCounter == 0)
        {
            // Updates the assets.
            Hub.Config.Stream.IdleAssets(this);
            Log("暂无待处理内容，正在等待新用户...");
        }

        const int interval = 10;
        if (waitCounter % interval == interval - 1 && Hub.Config.AntiIdle)
            await Click(B, 1_000, token).ConfigureAwait(false);
        else
            await Task.Delay(1_000, token).ConfigureAwait(false);
    }

    protected virtual (PokeTradeDetail<PB7>? detail, uint priority) GetTradeData(PokeRoutineType type)
    {
        string botName = Connection.Name;
        if (Hub.Queues.TryDequeue(type, out var detail, out var priority, botName))
            return (detail, priority);
        if (Hub.Queues.TryDequeueLedy(out detail))
            return (detail, PokeTradePriorities.TierFree);
        return (null, PokeTradePriorities.TierFree);
    }

    private async Task PerformTrade(SAV7b sav, PokeTradeDetail<PB7> detail, PokeRoutineType type, uint priority, CancellationToken token)
    {
        PokeTradeResult result;
        try
        {
            detail.IsProcessing = true;

            result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);
            if (result == PokeTradeResult.Success)
                return;
        }
        catch (SocketException socket)
        {
            Log(socket.Message);
            result = PokeTradeResult.ExceptionConnection;
            HandleAbortedTrade(detail, type, priority, result);
            throw; // let this interrupt the trade loop. re-entering the trade loop will recheck the connection.
        }
        catch (Exception e)
        {
            Log(e.Message);
            result = PokeTradeResult.ExceptionInternal;
        }

        HandleAbortedTrade(detail, type, priority, result);
    }

    private void HandleAbortedTrade(PokeTradeDetail<PB7> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
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

    private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV7b sav, PokeTradeDetail<PB7> poke, CancellationToken token)
    {
        // Check if trade was canceled by user
        if (poke.IsCanceled)
        {
            Log($"训练家 {poke.Trainer.TrainerName} 取消了此次交易。");
            poke.TradeCanceled(this, PokeTradeResult.UserCanceled);
            return PokeTradeResult.UserCanceled;
        }

        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        Hub.Config.Stream.EndEnterCode(this);
        var toSend = poke.TradeData;
        if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
        {
            var trainerID = poke.Trainer.ID;
            var tradeCodeStorage1 = new TradeCodeStorage();
            var tradeDetails = tradeCodeStorage1.GetTradeDetails(trainerID);
            if (tradeDetails != null && tradeDetails.TID != 0 && tradeDetails.SID != 0)
            {
                Log($"正在使用训练家信息应用自动 OT：OT：{tradeDetails.OT}，TID：{tradeDetails.TID}，SID：{tradeDetails.SID}");
                var updatedToSend = await ApplyAutoOT(toSend, trainerID);
                if (updatedToSend != null)
                {
                    toSend = updatedToSend;
                    poke.TradeData = updatedToSend;
                }
            }
        }
        if (toSend.Species != 0)
            await WriteBoxPokemon(toSend, 0, 0, token);
        if (!await IsOnOverworldStandard(token))
        {
            await ExitTrade(true, token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }
        await Click(X, 2000, token).ConfigureAwait(false);
        Log("正在打开菜单...");
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
        {
            await Click(B, 2000, token);
            await Click(X, 2000, token);
        }
        Log("正在选择“联机”......");
        await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) == waitingtotradescreen)
        {
            await Click(A, 1000, token);
            if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen2)
            {
                while (!await IsOnOverworldStandard(token))
                {
                    await Click(B, 1000, token);
                }
                await Click(X, 2000, token).ConfigureAwait(false);
                Log("正在打开菜单......");
                while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                {
                    await Click(B, 2000, token);
                    await Click(X, 2000, token);
                }
                Log("正在选择“联机”......");
                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }
        }
        await Task.Delay(2000, token).ConfigureAwait(false);
        Log("正在选择“远程连接”......");

        await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        await Click(A, 10000, token).ConfigureAwait(false);

        await Click(A, 1000, token).ConfigureAwait(false);
        await EnterLinkCodeLG(poke, token);
        poke.TradeSearching(this);
        Log($"正在搜索用户 {poke.Trainer.TrainerName}");
        await Task.Delay(3000, token).ConfigureAwait(false);
        var btimeout = new Stopwatch();
        btimeout.Restart();

        while (await LGIsinwaitingScreen(token))
        {
            await Task.Delay(100, token);
            if (btimeout.ElapsedMilliseconds >= 45_000)
            {
                poke.TradeCanceled(this, PokeTradeResult.NoTrainerFound);
                Log($"未找到 {poke.Trainer.TrainerName}");

                await ExitTrade(false, token);
                Hub.Config.Stream.EndEnterCode(this);
                return PokeTradeResult.NoTrainerFound;
            }
        }
        Log($"已找到 {poke.Trainer.TrainerName}");
        await Task.Delay(10000, token).ConfigureAwait(false);
        var tradepartnersav = new SAV7b();
        var tradepartnersav2 = new SAV7b();
        var tpsarray = await SwitchConnection.ReadBytesAsync(TradePartnerData, 0x168, token);
        tpsarray.CopyTo(tradepartnersav.Blocks.Status.Data);
        var tpsarray2 = await SwitchConnection.ReadBytesAsync(TradePartnerData2, 0x168, token);
        tpsarray2.CopyTo(tradepartnersav2.Blocks.Status.Data);

        var tradeCodeStorage = new TradeCodeStorage();

        if (tradepartnersav.OT != sav.OT)
        {
            uint displaySID = BinaryPrimitives.ReadUInt32LittleEndian(tradepartnersav.Blocks.Status.Data[0..4]) / 1_000_000;
            uint displayTID = BinaryPrimitives.ReadUInt32LittleEndian(tradepartnersav.Blocks.Status.Data[0..4]) % 1_000_000;
            string tid7 = displayTID.ToString("D6");
            string sid7 = displaySID.ToString("D4");

            // Extract gender and language
            byte gender = tradepartnersav.Blocks.Status.Data[5];
            int language = tradepartnersav.Blocks.Status.Data[0x35];

            string genderText = gender == 0 ? "男" : "女";
            string languageText = GetLanguageName(language);

            Log($"已找到连线交换对象：{tradepartnersav.OT}，TID7：{tid7}，SID7：{sid7}，性别：{genderText}，语言：{languageText}，游戏版本：{tradepartnersav.Version}");

            // Save all trainer details in the TradeCodeStorage
            tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, tradepartnersav.OT, int.Parse(tid7), int.Parse(sid7), gender, language);

            // Send notification with trainer details
            poke.SendNotification(this, $"已找到交换对象 - OT：{tradepartnersav.OT}，TID：{tid7}，SID：{sid7}，性别：{genderText}，语言：{languageText}");
        }

        if (tradepartnersav2.OT != sav.OT)
        {
            uint displaySID = BinaryPrimitives.ReadUInt32LittleEndian(tradepartnersav2.Blocks.Status.Data[0..4]) / 1_000_000;
            uint displayTID = BinaryPrimitives.ReadUInt32LittleEndian(tradepartnersav2.Blocks.Status.Data[0..4]) % 1_000_000;
            string tid7 = displayTID.ToString("D6");
            string sid7 = displaySID.ToString("D4");

            // Extract gender and language
            byte gender = tradepartnersav2.Blocks.Status.Data[5];
            int language = tradepartnersav2.Blocks.Status.Data[0x35];

            string genderText = gender == 0 ? "男" : "女";
            string languageText = GetLanguageName(language);

            Log($"已找到连线交换对象：{tradepartnersav2.OT}，TID7：{tid7}，SID7：{sid7}，性别：{genderText}，语言：{languageText}");

            // Save all trainer details in the TradeCodeStorage
            tradeCodeStorage.UpdateTradeDetails(poke.Trainer.ID, tradepartnersav2.OT, int.Parse(tid7), int.Parse(sid7), gender, language);

            // Send notification with trainer details
            poke.SendNotification(this, $"已找到交换对象 - OT：{tradepartnersav2.OT}，TID：{tid7}，SID：{sid7}，性别：{genderText}，语言：{languageText}");
        }


        if (poke.Type == PokeTradeType.Dump)
        {
            var result = await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);
            await ExitTrade(false, token).ConfigureAwait(false);
            return result;
        }
        if (poke.Type == PokeTradeType.Clone)
        {
            var result = await ProcessCloneTradeAsync(poke, sav, token);
            await ExitTrade(false, token);
            return result;
        }
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen)
        {
            await Click(A, 1000, token);
        }
        poke.SendNotification(this, "你有 15 秒时间选择要交换的宝可梦。");
        Log("正在等待交易画面...");

        await Task.Delay(5_000, token).ConfigureAwait(false);
        var tradeResult = await ConfirmAndStartTrading(poke, 0, token);
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
            return PokeTradeResult.ExceptionInternal;
        }

        //trade was successful
        var received = await ReadPokemon(GetSlotOffset(0, 0), token);

        // Pokémon in b1s1 is same as the one they were supposed to receive (was never sent).
        if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
        {
            Log("玩家未完成交换。");
            await ExitTrade(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        // As long as we got rid of our inject in b1s1, assume the trade went through.
        Log("玩家已完成交换。");
        UpdateCountsAndExport(poke, received, toSend);
        poke.TradeFinished(this, received);

        // Still need to wait out the trade animation.
        await Task.Delay(10_000, token).ConfigureAwait(false);

        await ExitTrade(false, token).ConfigureAwait(false);
        return PokeTradeResult.Success;
    }

    private static string GetLanguageName(int languageID)
    {
        return languageID switch
        {
            1 => "日文",
            2 => "英文",
            3 => "法文",
            4 => "意大利文",
            5 => "德文",
            7 => "西班牙文",
            8 => "韩文",
            9 => "简体中文",
            10 => "繁体中文",
            _ => $"未知（{languageID}）"
        };
    }

    private void UpdateCountsAndExport(PokeTradeDetail<PB7> poke, PB7 received, PB7 toSend)
    {
        var counts = TradeSettings;
        if (poke.Type == PokeTradeType.Random)
            counts.CountStatsSettings.AddCompletedDistribution();
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
            if (poke.Type is PokeTradeType.Specific or PokeTradeType.Clone)
                DumpPokemon(DumpSetting.DumpFolder, tradedFolder, toSend); // sent to partner
        }
    }

    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PB7> detail, int slot, CancellationToken token)
    {
        // We'll keep watching B1S1 for a change to indicate a trade started -> should try quitting at that point.
        var oldEC = await Connection.ReadBytesAsync((uint)GetSlotOffset(0, slot), 8, token).ConfigureAwait(false);
        Log("正在确认并启动交易...");
        await Click(A, 3_000, token).ConfigureAwait(false);
        for (int i = 0; i < 10; i++)
        {
            if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen)
                return PokeTradeResult.TrainerLeft;
            await Click(A, 1_500, token).ConfigureAwait(false);
        }

        var tradeCounter = 0;
        Log("正在检查槽位 1 是否收到宝可梦");
        while (true)
        {
            var newEC = await Connection.ReadBytesAsync((uint)GetSlotOffset(0, slot), 8, token).ConfigureAwait(false);
            if (!newEC.SequenceEqual(oldEC))
            {
                Log("检测到槽位 1 发生变化");
                await Task.Delay(15_000, token).ConfigureAwait(false);
                return PokeTradeResult.Success;
            }

            tradeCounter++;

            if (tradeCounter >= Hub.Config.Trade.TradeConfiguration.TradeAnimationMaxDelaySeconds)
            {
                // If we don't detect a B1S1 change, the trade didn't go through in that time.
                Log("未检测到槽位 1 发生变化。");
                await Click(B, 1_000, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            if (await IsOnOverworldStandard(token))
                return PokeTradeResult.TrainerLeft;
            await Task.Delay(1000, token);
        }
    }

    private async Task<PokeTradeResult> ProcessCloneTradeAsync(PokeTradeDetail<PB7> detail, SAV7b sav, CancellationToken token)
    {
        detail.SendNotification(this, "请高亮你想克隆的宝可梦，每次最多 6 只！每次高亮之间有 5 秒时间切换至下一只（前 5 秒从现在开始）。若少于 6 只，请在交易开始前保持同一只宝可梦。");
        await Task.Delay(10_000, token);
        var offereddatac = await SwitchConnection.ReadBytesAsync(OfferedPokemon, 0x104, token);
        var offeredpbmc = new PB7(offereddatac);
        List<PB7> clonelist = [offeredpbmc];
        detail.SendNotification(this, $"已将 {(Species)offeredpbmc.Species} 加入克隆列表");

        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(5_000, token);
            var newoffereddata = await SwitchConnection.ReadBytesAsync(OfferedPokemon, 0x104, token);
            var newofferedpbm = new PB7(newoffereddata);
            if (clonelist.Any(z => SearchUtil.HashByDetails(z) == SearchUtil.HashByDetails(newofferedpbm)))
            {
                continue;
            }
            else
            {
                clonelist.Add(newofferedpbm);
                offeredpbmc = newofferedpbm;
                detail.SendNotification(this, $"已将 {(Species)offeredpbmc.Species} 加入克隆列表");
            }
        }

        var clonestring = new StringBuilder();
        foreach (var k in clonelist)
            clonestring.AppendLine($"{(Species)k.Species}");
        detail.SendNotification(this, clonestring.ToString());

        detail.SendNotification(this, "正在退出交易以注入克隆，请使用相同的连线密码重新连接。");
        await ExitTrade(false, token);
        foreach (var g in clonelist)
        {
            await WriteBoxPokemon(g, 0, clonelist.IndexOf(g), token);
            await Task.Delay(1000, token);
        }
        await Click(X, 2000, token).ConfigureAwait(false);
        Log("正在打开菜单...");
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
        {
            await Click(B, 2000, token);
            await Click(X, 2000, token);
        }
        Log("正在选择“联机”...");
        await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == menuscreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) == waitingtotradescreen)
        {
            await Click(A, 1000, token);
            if (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen || BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == savescreen2)
            {
                while (!await IsOnOverworldStandard(token))
                {
                    await Click(B, 1000, token);
                }
                await Click(X, 2000, token).ConfigureAwait(false);
                Log("正在打开菜单...");
                while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 4, token), 0) != menuscreen)
                {
                    await Click(B, 2000, token);
                    await Click(X, 2000, token);
                }
                Log("正在选择“联机”...");
                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }
        }
        await Task.Delay(2000, token);
        Log("正在选择“远程连接”...");

        await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
        await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
        await Click(A, 10000, token).ConfigureAwait(false);

        await Click(A, 1000, token).ConfigureAwait(false);
        await EnterLinkCodeLG(detail, token);
        detail.TradeSearching(this);
        Log($"正在搜索用户 {detail.Trainer.TrainerName}");
        var btimeout = new Stopwatch();
        while (await LGIsinwaitingScreen(token))
        {
            await Task.Delay(100, token);
            if (btimeout.ElapsedMilliseconds >= 45_000)
            {
                detail.TradeCanceled(this, PokeTradeResult.NoTrainerFound);
                Log($"未找到 {detail.Trainer.TrainerName}");

                await ExitTrade(false, token);
                Hub.Config.Stream.EndEnterCode(this);
                return PokeTradeResult.NoTrainerFound;
            }
        }
        Log($"已找到 {detail.Trainer.TrainerName}");
        await Task.Delay(10000, token);
        var tradepartnersav = new SAV7b();
        var tradepartnersav2 = new SAV7b();
        var tpsarray = await SwitchConnection.ReadBytesAsync(TradePartnerData, 0x168, token);
        tpsarray.CopyTo(tradepartnersav.Blocks.Status.Data);
        var tpsarray2 = await SwitchConnection.ReadBytesAsync(TradePartnerData2, 0x168, token);
        tpsarray2.CopyTo(tradepartnersav2.Blocks.Status.Data);
        if (tradepartnersav.OT != sav.OT)
        {
            Log($"已找到连线交换对象：{tradepartnersav.OT}，TID：{tradepartnersav.DisplayTID}，SID：{tradepartnersav.DisplaySID}，游戏版本：{tradepartnersav.Version}");
            detail.SendNotification(this, $"已找到连线交换对象：{tradepartnersav.OT}，TID：{tradepartnersav.DisplayTID}，SID：{tradepartnersav.DisplaySID}，游戏版本：{tradepartnersav.Version}");
        }
        if (tradepartnersav2.OT != sav.OT)
        {
            Log($"已找到连线交换对象：{tradepartnersav2.OT}，TID：{tradepartnersav2.DisplayTID}，SID：{tradepartnersav2.DisplaySID}");
            detail.SendNotification(this, $"已找到连线交换对象：{tradepartnersav2.OT}，TID：{tradepartnersav2.DisplayTID}，SID：{tradepartnersav2.DisplaySID}，游戏版本：{tradepartnersav.Version}");
        }
        foreach (var t in clonelist)
        {
            for (int q = 0; q < clonelist.IndexOf(t); q++)
            {
                await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token);
                await SetStick(SwitchStick.RIGHT, 0, 0, 1000, token).ConfigureAwait(false);
            }
            while (BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(ScreenOff, 2, token), 0) == Boxscreen)
            {
                await Click(A, 1000, token);
            }
            detail.SendNotification(this, $"正在派送 {(Species)t.Species}。你有 15 秒时间选择要交换的宝可梦。");
            Log("正在等待交易画面...");

            await Task.Delay(10_000, token).ConfigureAwait(false);
            detail.SendNotification(this, "还剩 5 秒进入交易界面，否则将导致交易中断。");
            await Task.Delay(5_000, token);
            var tradeResult = await ConfirmAndStartTrading(detail, clonelist.IndexOf(t), token);
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
            await Task.Delay(30_000, token);
        }
        await ExitTrade(false, token);
        return PokeTradeResult.Success;
    }

    private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PB7> detail, CancellationToken token)
    {
        detail.SendNotification(this, "请高亮你给我展示的宝可梦，你有 30 秒时间。");
        var offereddata = await SwitchConnection.ReadBytesAsync(OfferedPokemon, 0x104, token);
        var offeredpbm = new PB7(offereddata);

        detail.SendNotification(this, offeredpbm, "这是你展示给我的宝可梦。");

        var quicktime = new Stopwatch();
        quicktime.Restart();
        while (quicktime.ElapsedMilliseconds <= 30_000)
        {
            var newoffereddata = await SwitchConnection.ReadBytesAsync(OfferedPokemon, 0x104, token);
            var newofferedpbm = new PB7(newoffereddata);
            if (SearchUtil.HashByDetails(offeredpbm) != SearchUtil.HashByDetails(newofferedpbm))
            {
                detail.SendNotification(this, newofferedpbm, "这是你展示给我的宝可梦。");

                offeredpbm = newofferedpbm;
            }
        }
        detail.SendNotification(this, "时间到！");
        return PokeTradeResult.Success;
    }

    private async Task EnterLinkCodeLG(PokeTradeDetail<PB7> poke, CancellationToken token)
    {
        if (poke.LGPETradeCode == null || !poke.LGPETradeCode.Any())
        {
            poke.LGPETradeCode = [Pictocodes.Pikachu, Pictocodes.Pikachu, Pictocodes.Pikachu];
            Log($"使用默认连线密码：{string.Join(", ", poke.LGPETradeCode)}");
        }
        else
        {
            Log($"正在输入连线密码：{string.Join(", ", poke.LGPETradeCode)}");
        }

        Hub.Config.Stream.StartEnterCode(this);
        var codePosition = 1;
        foreach (Pictocodes pc in poke.LGPETradeCode)
        {
            Log($"正在输入第 {codePosition}/3 个图标：{pc}");

            if ((int)pc > 4)
            {
                await SetStick(SwitchStick.RIGHT, 0, -30000, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }
            if ((int)pc <= 4)
            {
                for (int i = (int)pc; i > 0; i--)
                {
                    await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
            }
            else
            {
                for (int i = (int)pc - 5; i > 0; i--)
                {
                    await SetStick(SwitchStick.RIGHT, 30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
            }
            await Click(A, 200, token).ConfigureAwait(false);
            await Task.Delay(500, token).ConfigureAwait(false);
            if ((int)pc <= 4)
            {
                for (int i = (int)pc; i > 0; i--)
                {
                    await SetStick(SwitchStick.RIGHT, -30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
            }
            else
            {
                for (int i = (int)pc - 5; i > 0; i--)
                {
                    await SetStick(SwitchStick.RIGHT, -30000, 0, 0, token).ConfigureAwait(false);
                    await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
            }
            if ((int)pc > 4)
            {
                await SetStick(SwitchStick.RIGHT, 0, 30000, 0, token).ConfigureAwait(false);
                await SetStick(SwitchStick.RIGHT, 0, 0, 0, token).ConfigureAwait(false);
            }
            codePosition++;
        }
        Log("连线密码输入完成");
    }

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

    private async Task ExitTrade(bool unexpected, CancellationToken token)
    {
        if (unexpected)
            Log("检测到异常行为，正在恢复位置。");

        // Track number of attempts to exit to overworld
        int attempts = 0;
        const int MAX_ATTEMPTS = 3; // After this many attempts, restart the game

        while (!await IsOnOverworldStandard(token))
        {
            attempts++;
            Log($"正在尝试退出，第 {attempts} 次");

            // Check if we've exceeded max attempts
            if (attempts > MAX_ATTEMPTS)
            {
                Log("多次尝试仍未返回主世界，正在重新启动游戏...");
                await RestartGameLGPE(Hub.Config, token).ConfigureAwait(false);
                return;
            }

            // Basic exit sequence
            // Press B to bring up the exit screen
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Task.Delay(6_000, token);
            Log("正在退出交易...");
            await Click(B, 1_000, token).ConfigureAwait(false);

            // Press A to confirm the exit
            Log("正在按下 A 键确认退出...");
            await Click(A, 1_000, token).ConfigureAwait(false);

            // Wait for the exit animation/transition
            await Task.Delay(10_000, token);

            Log("正在按下 B 键返回至主界面。");
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await IsOnOverworldStandard(token))
            {
                Log("已成功返回主世界。");
                return;
            }
            await Task.Delay(2_000, token);
        }

        Log("已成功返回主世界。");
    }

    private Task<PB7?> ApplyAutoOT(PB7 toSend, ulong trainerID)
    {
        var tradeCodeStorage = new TradeCodeStorage();
        var tradeDetails = tradeCodeStorage.GetTradeDetails(trainerID);
        if (tradeDetails != null)
        {
            var cln = toSend.Clone();
            if (!string.IsNullOrEmpty(tradeDetails.OT))
                cln.OriginalTrainerName = tradeDetails.OT;
            cln.SetDisplayTID((uint)tradeDetails.TID);
            cln.SetDisplaySID((uint)tradeDetails.SID);

            // Set gender if available
            if (tradeDetails.Gender.HasValue)
            {
                cln.OriginalTrainerGender = tradeDetails.Gender.Value;
            }

            // Set language if available
            if (tradeDetails.Language.HasValue)
            {
                cln.Language = tradeDetails.Language.Value;
                Log($"正在将宝可梦语言设置为：{GetLanguageName(tradeDetails.Language.Value)}");
            }
            else
            {
                cln.Language = (int)LanguageID.English; // Default fallback
            }

            ClearOTTrash(cln, tradeDetails);

            if (!toSend.IsNicknamed)
                cln.ClearNickname();

            if (toSend.IsShiny)
                cln.PID = (uint)((cln.TID16 ^ cln.SID16 ^ (cln.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (cln.PID & 0xFFFF);

            if (!toSend.ChecksumValid)
                cln.RefreshChecksum();

            var tradelgpe = new LegalityAnalysis(cln);
            if (tradelgpe.Valid)
            {
                Log("宝可梦合法，正在应用自动 OT。");
                return Task.FromResult<PB7?>(cln);
            }
            else
            {
                Log("宝可梦不合法，无法应用自动 OT。");
                Log(tradelgpe.Report());
                return Task.FromResult<PB7?>(null);
            }
        }
        else
        {
            Log("未找到对应训练家的交易信息，无法应用自动 OT。");
            return Task.FromResult<PB7?>(null);
        }
    }

    private static void ClearOTTrash(PB7 pokemon, TradeCodeStorage.TradeCodeDetails? tradeDetails)
    {
        if (tradeDetails?.OT == null)
        {
            LogUtil.LogInfo("AutoOT", "交易信息或 OT 为空，跳过清理垃圾字节。");
            return;
        }
        Span<byte> trash = pokemon.OriginalTrainerTrash;
        trash.Clear();
        string name = tradeDetails.OT;
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
}
