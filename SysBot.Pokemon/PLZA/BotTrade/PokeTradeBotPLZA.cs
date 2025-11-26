using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using SysBot.Base.Util;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsPLZA;
using static SysBot.Pokemon.TradeHub.SpecialRequests;

namespace SysBot.Pokemon;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class PokeTradeBotPLZA(PokeTradeHub<PA9> Hub, PokeBotState Config) : PokeRoutineExecutor9PLZA(Config), ICountBot, ITradeBot
{
    public readonly TradeAbuseSettings AbuseSettings = Hub.Config.TradeAbuse;

    /// <summary>
    /// Folder to dump received trade data to.
    /// </summary>
    /// <remarks>If null, will skip dumping.</remarks>
    private readonly FolderSettings DumpSetting = Hub.Config.Folder;

    private readonly TradeSettings TradeSettings = Hub.Config.Trade;

    private uint DisplaySID;
    private uint DisplayTID;

    private string OT = string.Empty;
    private bool StartFromOverworld = true;
    private ulong? _cachedBoxOffset;
    private ulong TradePartnerStatusOffset;
    private bool _wasConnectedToPartner = false;
    private int _consecutiveConnectionFailures = 0; // Track consecutive online connection failures for soft ban detection

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

    #region Lifecycle & Main Loop

    public override Task HardStop()
    {
        UpdateBarrier(false);
        return CleanExit(CancellationToken.None);
    }

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            // Ensure cache is clean on startup
            _cachedBoxOffset = null;
            _wasConnectedToPartner = false;
            _consecutiveConnectionFailures = 0;

            Hub.Queues.Info.CleanStuckTrades();
            await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

            Log("正在连接到主机...");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            OT = sav.OT;
            DisplaySID = sav.DisplaySID;
            DisplayTID = sav.DisplayTID;
            RecentTrainerCache.SetRecentTrainer(sav);
            OnConnectionSuccess();

            StartFromOverworld = true;

            Log("正在初始化机器人...");
            if (!await CheckIfOnOverworld(token).ConfigureAwait(false))
            {
                if (!await RecoverToOverworld(token).ConfigureAwait(false))
                {
                    Log("正在重启游戏...");
                    await RestartGamePLZA(token).ConfigureAwait(false);
                    await Task.Delay(5_000, token).ConfigureAwait(false);

                    if (!await CheckIfOnOverworld(token).ConfigureAwait(false))
                    {
                        Log("启动失败。请重启机器人。");
                        throw new Exception("无法到达大地图。机器人无法开始交易。");
                    }
                }
            }

            Log("机器人就绪。等待交易...");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            OnConnectionError(e);
            throw;
        }

        Log($"结束 {nameof(PokeTradeBotPLZA)} 循环。");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        Hub.Queues.Info.CleanStuckTrades();
        await Task.Delay(2_000, t).ConfigureAwait(false);
        await ReOpenGame(Hub.Config, t).ConfigureAwait(false);
        _cachedBoxOffset = null; // Invalidate box offset cache after reboot
        await HardStop().ConfigureAwait(false);
        await Task.Delay(2_000, t).ConfigureAwait(false);
        if (!t.IsCancellationRequested)
        {
            Log("正在重启主循环。");
            await MainLoop(t).ConfigureAwait(false);
        }
    }

    #endregion

    #region Enums

    protected enum TradePartnerWaitResult
    {
        Success,
        Timeout,
        KickedToMenu
    }

    protected enum LinkCodeEntryResult
    {
        Success,
        VerificationFailedMismatch
    }

    #endregion

    #region Trade Queue Management

    protected virtual (PokeTradeDetail<PA9>? detail, uint priority) GetTradeData(PokeRoutineType type)
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

    #endregion

    #region Trade Partner Detection

    // Upon connecting, their Nintendo ID will instantly update.
    protected virtual async Task<TradePartnerWaitResult> WaitForTradePartner(CancellationToken token)
    {
        Log("正在等待训练家...");

        // Initial delay to let the game populate NID pointer in memory
        await Task.Delay(3_000, token).ConfigureAwait(false);

        int maxWaitMs = Hub.Config.Trade.TradeConfiguration.TradeWaitTime * 1_000;
        int elapsed = 3_000; // Already waited 3 seconds above

        while (elapsed < maxWaitMs)
        {
            // Safety check: verify we're still in a valid state (not kicked to menu/overworld)
            var gameState = await GetGameState(token).ConfigureAwait(false);
            if (gameState != 0x01 && gameState != 0x02)
            {
                Log("连接中断。正在重启...");
                return TradePartnerWaitResult.KickedToMenu;
            }

            // Additional safety check every 10 seconds: verify link code is still valid
            if (elapsed % 10_000 < 500)
            {
                var currentCode = await GetCurrentLinkCode(token).ConfigureAwait(false);
                if (currentCode == 0)
                {
                    Log("连接错误。正在重启...");
                    return TradePartnerWaitResult.KickedToMenu;
                }
            }

            // Check if we've entered the trade box - this confirms a partner is connected
            if (await IsOnMenu(MenuState.InBox, token).ConfigureAwait(false))
            {
                Log("检测到交易伙伴！");
                _wasConnectedToPartner = true; // Mark that we've connected to a partner

                // Set the offset for trade partner status monitoring (used in clone mode)
                var (valid, statusOffset) = await ValidatePointerAll(Offsets.TradePartnerStatusPointer, token).ConfigureAwait(false);
                if (valid)
                    TradePartnerStatusOffset = statusOffset;

                return TradePartnerWaitResult.Success;
            }

            await Task.Delay(500, token).ConfigureAwait(false);
            elapsed += 500;
        }

        Log("等待交易伙伴超时。");
        return TradePartnerWaitResult.Timeout;
    }

    #endregion

    #region AutoOT Features

    private static void ApplyTrainerInfo(PA9 pokemon, TradePartnerStatusPLZA partner)
    {
        pokemon.OriginalTrainerGender = (byte)partner.Gender;
        pokemon.TrainerTID7 = (uint)Math.Abs(partner.DisplayTID);
        pokemon.TrainerSID7 = (uint)Math.Abs(partner.DisplaySID);
        pokemon.OriginalTrainerName = partner.OT;
    }

    private async Task<PA9> ApplyAutoOT(PA9 toSend, TradePartnerStatusPLZA tradePartner, SAV9ZA sav, CancellationToken token)
    {
        // Sanity check: if trade partner OT is empty, skip AutoOT
        if (string.IsNullOrWhiteSpace(tradePartner.OT))
        {
            return toSend;
        }

        if (toSend.Version == GameVersion.GO)
        {
            var goClone = toSend.Clone();
            goClone.OriginalTrainerName = tradePartner.OT;

            ClearOTTrash(goClone, tradePartner);

            if (!toSend.ChecksumValid)
                goClone.RefreshChecksum();

            var boxOffset = await GetBoxStartOffset(token).ConfigureAwait(false);
            await SetBoxPokemonAbsolute(boxOffset, goClone, token, sav).ConfigureAwait(false);
            return goClone;
        }

        if (toSend is IHomeTrack pk && pk.HasTracker)
        {
            return toSend;
        }

        if (toSend.Generation != toSend.Format)
        {
            return toSend;
        }

        bool isMysteryGift = toSend.FatefulEncounter;
        var cln = toSend.Clone();

        // Apply trainer info (OT, TID, SID, Gender)
        ApplyTrainerInfo(cln, tradePartner);

        if (!isMysteryGift)
        {
            // Validate language ID - if invalid, default to English (2)
            int language = tradePartner.Language;
            if (language < 1 || language > 12) // Valid language IDs are 1-12
                language = 2; // English
            cln.Language = language;
        }

        ClearOTTrash(cln, tradePartner);

        // Hard-code version to ZA since PLZA only has one game version
        cln.Version = GameVersion.ZA;

        // Set nickname to species name in the Pokemon's language using PKHeX's method
        // This properly handles generation-specific formatting and language-specific names
        if (!toSend.IsNicknamed)
            cln.ClearNickname();

        // Clear handler info - make it look like trade partner is OT and never traded it
        cln.CurrentHandler = 0; // 0 = OT is current handler

        if (toSend.IsShiny)
            cln.PID = (uint)((cln.TID16 ^ cln.SID16 ^ (cln.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (cln.PID & 0xFFFF);

        cln.RefreshChecksum();

        var tradeSV = new LegalityAnalysis(cln);

        if (tradeSV.Valid)
        {
            // Don't pass sav - we've already set handler info and don't want UpdateHandler to overwrite it
            var boxOffset = await GetBoxStartOffset(token).ConfigureAwait(false);
            await SetBoxPokemonAbsolute(boxOffset, cln, token, null).ConfigureAwait(false);
            return cln;
        }
        else
        {
            if (toSend.Species != 0)
            {
                var boxOffset = await GetBoxStartOffset(token).ConfigureAwait(false);
                await SetBoxPokemonAbsolute(boxOffset, toSend, token, sav).ConfigureAwait(false);
            }
            return toSend;
        }
    }

    private static void ClearOTTrash(PA9 pokemon, TradePartnerStatusPLZA tradePartner)
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

    #endregion

    #region Trade Confirmation

    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PA9> detail, uint checksumBeforeTrade, CancellationToken token)
    {
        await Click(A, 3_000, token).ConfigureAwait(false);

        var boxOffset = await GetBoxStartOffset(token).ConfigureAwait(false);
        bool b1s1Changed = false;
        bool warningSent = false;
        int maxTime = Hub.Config.Trade.TradeConfiguration.MaxTradeConfirmTime;

        for (int i = 0; i < maxTime; i++)
        {
            await Click(A, 1_000, token).ConfigureAwait(false);

            // Send warning 10 seconds before timeout
            if (!warningSent && i == maxTime - 10 && maxTime >= 10)
            {
                detail.SendNotification(this, "嘿！选择一只宝可梦交易，否则我要离开了！");
                warningSent = true;
            }

            // Check if we're still in trade box (partner disconnected if not in InBox menu state)
            if (!await IsOnMenu(MenuState.InBox, token).ConfigureAwait(false))
            {
                Log("不再处于交易箱 - 伙伴在报价阶段拒绝并退出。");
                detail.SendNotification(this, "交易伙伴拒绝或已断开连接。");
                return PokeTradeResult.NoTrainerFound;
            }

            if (!b1s1Changed)
            {
                var currentPokemon = await ReadPokemon(boxOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
                var currentChecksum = currentPokemon.Checksum;

                if (currentChecksum != checksumBeforeTrade)
                    b1s1Changed = true;
            }

            if (b1s1Changed)
            {
                var currentGameState = await GetGameState(token).ConfigureAwait(false);
                if (currentGameState == 0x02)
                {
                    Log("交易开始！等待完成...");
                    return PokeTradeResult.Success;
                }
            }
        }

        if (!b1s1Changed)
        {
            var finalPokemon = await ReadPokemon(boxOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            var finalChecksum = finalPokemon.Checksum;

            if (finalChecksum != checksumBeforeTrade)
                b1s1Changed = true;
        }

        if (b1s1Changed)
        {
            // B1S1 changed means BOTH players confirmed the trade
            // Give additional time for the game state to transition to trade animation (0x02)
            // This prevents disconnecting during an active trade that's about to start
            Log("双方确认交易。等待交易动画开始...");

            int additionalWaitSeconds = 15; // Give 15 extra seconds for animation to start
            for (int i = 0; i < additionalWaitSeconds; i++)
            {
                var currentGameState = await GetGameState(token).ConfigureAwait(false);
                if (currentGameState == 0x02)
                {
                    Log("交易开始！等待完成...");
                    return PokeTradeResult.Success;
                }

                // Check if we're still in trade box (partner disconnected if not in InBox menu state)
                if (!await IsOnMenu(MenuState.InBox, token).ConfigureAwait(false))
                {
                    Log("不再处于交易箱 - 伙伴在确认后但动画前断开连接。");
                    detail.SendNotification(this, "交易伙伴已断开连接。");
                    return PokeTradeResult.NoTrainerFound;
                }

                await Task.Delay(1_000, token).ConfigureAwait(false);
            }

            // If we still haven't entered trade animation after 15 seconds, something is wrong
            Log("交易已确认但动画未开始。可能是连接问题。");
        }

        return PokeTradeResult.TrainerTooSlow;
    }

    #endregion

    #region Online Connection & Portal

    private async Task<bool> ConnectAndEnterPortal(CancellationToken token)
    {
        if (!await CheckIfOnOverworld(token).ConfigureAwait(false))
            await RecoverToOverworld(token).ConfigureAwait(false);

        await Click(X, 3_000, token).ConfigureAwait(false); // Load Menu

        await Click(DUP, 1_000, token).ConfigureAwait(false);
        await Click(A, 2_000, token).ConfigureAwait(false);
        await Click(DRIGHT, 1_000, token).ConfigureAwait(false);
        await Click(DRIGHT, 1_000, token).ConfigureAwait(false);
        await Click(A, 1_000, token).ConfigureAwait(false);
        await Click(DRIGHT, 1_000, token).ConfigureAwait(false);

        bool wasAlreadyConnected = await CheckIfConnectedOnline(token).ConfigureAwait(false);

        if (wasAlreadyConnected)
        {
            await Click(A, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            await Task.Delay(1_000, token).ConfigureAwait(false);
            _consecutiveConnectionFailures = 0;
        }
        else
        {
            await Click(A, 1_000, token).ConfigureAwait(false);

            int attempts = 0;
            while (!await CheckIfConnectedOnline(token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                if (++attempts > 30)
                {
                    _consecutiveConnectionFailures++;
                    Log($"连接网络失败。连续失败次数: {_consecutiveConnectionFailures}");

                    if (_consecutiveConnectionFailures >= 3)
                    {
                        Log("检测到软封禁（连续 3 次连接失败）。等待 30 分钟...");
                        await Task.Delay(30 * 60 * 1000, token).ConfigureAwait(false);
                        Log("30 minute wait complete. Resuming operations.");
                        _consecutiveConnectionFailures = 0;
                    }

                    return false;
                }
            }
            await Task.Delay(8_000 + Hub.Config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);
            Log("已连接到网络。");
            _consecutiveConnectionFailures = 0;

            await Click(A, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            await Task.Delay(3_000, token).ConfigureAwait(false);
        }

        return true;
    }

    #endregion

    #region Trade Queue Processing

    private async Task DoNothing(CancellationToken token)
    {
        Log("等待交易请求...");
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
            await Task.Delay(1_000, token).ConfigureAwait(false);
    }

    private async Task DoTrades(SAV9ZA sav, CancellationToken token)
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

            detail.IsProcessing = true;
            Log($"正在处理交易请求...");
            Hub.Config.Stream.StartTrade(this, detail, Hub);
            Hub.Queues.StartTrade(this, detail);

            await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
        }
    }

    #endregion

    #region Navigation and Recovery

    private async Task DisconnectFromTrade(CancellationToken token)
    {
        Log("正在断开交易...");

        // Check if we're still in the trade box (connected) or kicked to menu
        var menuState = await GetMenuState(token).ConfigureAwait(false);

        if (menuState == MenuState.InBox)
        {
            // Still in trade box - press B+A to disconnect
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
        }
        else
        {
            // Already kicked to menu - only press B to navigate back
            await Click(B, 0_500, token).ConfigureAwait(false);
        }
    }

    private async Task ExitTradeToOverworld(bool unexpected, CancellationToken token)
    {
        if (unexpected)
            Log("异常行为，正在恢复到大地图。");

        Log("正在退出交易到大地图...");

        // CRITICAL: Wait for GameState to return to 0x01 before attempting to exit
        // This ensures the trade animation is completely finished
        int gameStateWaitTime = 10; // Wait up to 10 seconds for animation to complete
        int gameStateElapsed = 0;
        bool animationComplete = false;

        while (gameStateElapsed < gameStateWaitTime)
        {
            var currentState = await GetGameState(token).ConfigureAwait(false);
            if (currentState == 0x01)
            {
                animationComplete = true;
                break;
            }
            await Task.Delay(1_000, token).ConfigureAwait(false);
            gameStateElapsed++;
        }

        if (!animationComplete)
        {
            Log("交易动画未完成。仍尝试退出...");
        }

        // Wait 3 seconds after animation completes before attempting to disconnect
        await Task.Delay(3_000, token).ConfigureAwait(false);

        // Check if we're already at overworld
        if (await CheckIfOnOverworld(token).ConfigureAwait(false))
        {
            StartFromOverworld = true;
            _wasConnectedToPartner = false; // Reset flag when successfully back to overworld
            return;
        }

        // Use MenuState to determine whether to disconnect or navigate back
        int timeoutSeconds = 30;
        int elapsedExit = 0;

        while (elapsedExit < timeoutSeconds)
        {
            var menuState = await GetMenuState(token).ConfigureAwait(false);

            // Check if we've reached overworld
            if (menuState == MenuState.Overworld)
            {
                Log("已返回大地图。");
                StartFromOverworld = true;
                _wasConnectedToPartner = false; // Reset flag when successfully back to overworld
                return;
            }

            if (menuState == MenuState.InBox)
            {
                // Still in trade box with partner connected - press B+A to disconnect
                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
            }
            else
            {
                // Partner disconnected (LinkTrade, LinkPlay, XMenu) - just press B
                await Click(B, 1_000, token).ConfigureAwait(false);
            }

            elapsedExit++;
        }

        // Failed to exit properly - restart the game
        Log("30 秒后仍无法退出交易。正在重启游戏...");
        await RestartGamePLZA(token).ConfigureAwait(false);
        StartFromOverworld = true;
    }

    #endregion

    #region Game State & Data Access

    private async Task<TradePartnerStatusPLZA> GetTradePartnerFullInfo(CancellationToken token)
    {
        var baseAddr = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerDataPointer, token).ConfigureAwait(false);
        var nidAddr = baseAddr + TradePartnerNIDShift;
        var tidAddr = baseAddr + TradePartnerTIDShift;

        // Read chunk starting from NID location - includes NID, TID at +0x44, and OT at +0x4C
        var chunk = await SwitchConnection.ReadBytesAbsoluteAsync(nidAddr, 0x69, token).ConfigureAwait(false);
        var nid = BitConverter.ToUInt64(chunk.AsSpan(0, 8));
        var dataIsLoaded = chunk[0x68] != 0;

        var trader_info = new TradePartnerStatusPLZA();

        if (dataIsLoaded)
        {
            var tid = chunk.AsSpan(0x44, 4).ToArray();
            var ot = chunk.AsSpan(0x4C, TradePartnerPLZA.MaxByteLengthStringObject).ToArray();
            tid.CopyTo(trader_info.Data, 0x00);
            ot.CopyTo(trader_info.Data, 0x08);

            // Read gender and language from TID location offset
            var genderLang = await SwitchConnection.ReadBytesAbsoluteAsync(tidAddr, 0x08, token).ConfigureAwait(false);
            trader_info.Data[0x04] = genderLang[0x04]; // Gender at TID base + 0x04
            trader_info.Data[0x05] = genderLang[0x05]; // Language at TID base + 0x05
        }
        else
        {
            // Data not at primary location, use fallback
            var fallbackTidAddr = tidAddr + FallBackTradePartnerDataShift;
            var fallbackChunk = await SwitchConnection.ReadBytesAbsoluteAsync(fallbackTidAddr, 34, token).ConfigureAwait(false);

            var tid = fallbackChunk.AsSpan(0, 4).ToArray();
            var ot = fallbackChunk.AsSpan(0x08, TradePartnerPLZA.MaxByteLengthStringObject).ToArray();
            tid.CopyTo(trader_info.Data, 0x00);
            ot.CopyTo(trader_info.Data, 0x08);

            // Read gender and language from fallback TID location
            var genderLang = await SwitchConnection.ReadBytesAbsoluteAsync(fallbackTidAddr, 0x08, token).ConfigureAwait(false);
            trader_info.Data[0x04] = genderLang[0x04]; // Gender at fallback TID + 0x04
            trader_info.Data[0x05] = genderLang[0x05]; // Language at fallback TID + 0x05
        }

        return trader_info;
    }

    private async Task<ulong> GetBoxStartOffset(CancellationToken token)
    {
        if (_cachedBoxOffset.HasValue)
            return _cachedBoxOffset.Value;

        // Get Box 1 Slot 1 address
        var finalOffset = await ResolvePointer(Offsets.BoxStartPokemonPointer, token).ConfigureAwait(false);
        _cachedBoxOffset = finalOffset;
        return finalOffset;
    }

    private async Task<bool> CheckIfOnOverworld(CancellationToken token)
    {
        return await IsOnMenu(MenuState.Overworld, token).ConfigureAwait(false);
    }

    private async Task<bool> CheckIfConnectedOnline(CancellationToken token)
    {
        var offset = await SwitchConnection.PointerAll(Offsets.IsConnectedPointer, token).ConfigureAwait(false);
        return await IsConnectedOnline(offset, token).ConfigureAwait(false);
    }

    private async Task<byte> GetGameState(CancellationToken token)
    {
        var offset = await SwitchConnection.PointerAll(Offsets.GameStatePointer, token).ConfigureAwait(false);
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0];
    }

    private async Task<int> GetCurrentLinkCode(CancellationToken token)
    {
        var offset = await SwitchConnection.PointerAll(Offsets.LinkCodeTradePointer, token).ConfigureAwait(false);
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 4, token).ConfigureAwait(false);
        return BitConverter.ToInt32(data, 0);
    }

    #endregion

    #region Trade Result Handling

    private void HandleAbortedTrade(PokeTradeDetail<PA9> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
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

    private async Task InnerLoop(SAV9ZA sav, CancellationToken token)
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

                // Invalidate cached pointers after reconnection - game state may have changed
                _cachedBoxOffset = null;
                Log("已重新连接 - 缓存指针已失效。");
            }
        }
    }

    #endregion

    #region Events

    private void OnConnectionError(Exception ex)
    {
        ConnectionError?.Invoke(this, ex);
    }

    private void OnConnectionSuccess()
    {
        ConnectionSuccess?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Specialized Trade Types

    private async Task<PokeTradeResult> PerformBatchTrade(SAV9ZA sav, PokeTradeDetail<PA9> poke, CancellationToken token)
    {
        int completedTrades = 0;
        var startingDetail = poke;
        var originalTrainerID = startingDetail.Trainer.ID;

        var tradesToProcess = poke.BatchTrades ?? [poke.TradeData];
        var totalBatchTrades = tradesToProcess.Count;

        // Cache trade partner info after first successful connection
        TradePartnerStatusPLZA? cachedTradePartnerInfo = null;

        void SendCollectedPokemonAndCleanup()
        {
            var allReceived = BatchTracker.GetReceivedPokemon(originalTrainerID);
            if (allReceived.Count > 0)
            {
                poke.SendNotification(this, $"正在发送中断前你交给我的 {allReceived.Count} 只宝可梦。");

                Log($"正在向训练家 {originalTrainerID} 返还 {allReceived.Count} 只宝可梦。");

                // Send each Pokemon directly instead of calling TradeFinished
                for (int j = 0; j < allReceived.Count; j++)
                {
                    var pokemon = allReceived[j];
                    var speciesName = SpeciesName.GetSpeciesName(pokemon.Species, 2);
                    Log($"返还: {speciesName}");

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
            Hub.Queues.Info.Remove(new TradeEntry<PA9>(poke, originalTrainerID, PokeRoutineType.Batch, poke.Trainer.TrainerName, poke.UniqueTradeID));
        }

        for (int i = 0; i < totalBatchTrades; i++)
        {
            var currentTradeIndex = i;
            var toSend = tradesToProcess[currentTradeIndex];
            ulong boxOffset;

            poke.TradeData = toSend;
            poke.Notifier.UpdateBatchProgress(currentTradeIndex + 1, toSend, poke.UniqueTradeID);

            // For subsequent trades (after first), we've already prepared the Pokemon during the previous trade animation
            // No need to prepare here - just send notification
            if (currentTradeIndex > 0)
            {
                poke.SendNotification(this, $"**Ready!** You can now offer your Pokémon for trade {currentTradeIndex + 1}/{totalBatchTrades}.");
                await Task.Delay(2_000, token).ConfigureAwait(false);
            }

            // For first trade only - search for partner
            if (currentTradeIndex == 0)
            {
                await Click(A, 0_500, token).ConfigureAwait(false);
                await Click(A, 0_500, token).ConfigureAwait(false);

                WaitAtBarrierIfApplicable(token);
                await Click(A, 1_000, token).ConfigureAwait(false);

                poke.TradeSearching(this);
                var partnerWaitResult = await WaitForTradePartner(token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                {
                    StartFromOverworld = true;
                    await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                    poke.SendNotification(this, "正在取消批量交易。程序已被中断。");
                    SendCollectedPokemonAndCleanup();
                    return PokeTradeResult.RoutineCancel;
                }

                if (partnerWaitResult == TradePartnerWaitResult.Timeout)
                {
                    // Partner never showed up - their fault, don't requeue
                    poke.IsProcessing = false;
                    poke.SendNotification(this, "未找到交易伙伴。正在取消批量交易。");
                    poke.TradeCanceled(this, PokeTradeResult.NoTrainerFound);
                    SendCollectedPokemonAndCleanup();

                    await RecoverToOverworld(token).ConfigureAwait(false);
                    return PokeTradeResult.NoTrainerFound;
                }

                if (partnerWaitResult == TradePartnerWaitResult.KickedToMenu)
                {
                    // Bot got kicked to menu - our fault, trigger requeue
                    Log("连接错误。正在重试...");
                    SendCollectedPokemonAndCleanup();
                    await RecoverToOverworld(token).ConfigureAwait(false);
                    return PokeTradeResult.RecoverStart;
                }

                Hub.Config.Stream.EndEnterCode(this);

                // Wait until we're in the trade box
                Log("正在搜索交易伙伴...");
                int boxCheckAttempts = 0;
                while (!await IsOnMenu(MenuState.InBox, token).ConfigureAwait(false))
                {
                    await Task.Delay(500, token).ConfigureAwait(false);
                    if (++boxCheckAttempts > 30) // 15 seconds max
                    {
                        Log("未找到交易伙伴。");
                        return PokeTradeResult.NoTrainerFound;
                    }
                }

                // Wait for trade UI and partner data to load
                await Task.Delay(2_000, token).ConfigureAwait(false);

                // Now that data has loaded, read partner info
                var tradePartnerFullInfo = await GetTradePartnerFullInfo(token).ConfigureAwait(false);
                cachedTradePartnerInfo = tradePartnerFullInfo; // Cache for subsequent trades
                var tradePartner = new TradePartnerPLZA(tradePartnerFullInfo);

                var trainerNID = await GetTradePartnerNID(token).ConfigureAwait(false);

                Log($"[TradePartner] OT: {tradePartner.TrainerName}, TID: {tradePartner.TID7}, SID: {tradePartner.SID7}, Gender: {tradePartnerFullInfo.Gender}, Language: {tradePartnerFullInfo.Language}, NID: {trainerNID}");

                RecordUtil<PokeTradeBotPLZA>.Record($"Initiating\t{trainerNID:X16}\t{tradePartner.TrainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");

                poke.SendNotification(this, $"找到交易伙伴: {tradePartner.TrainerName}。**TID**: {tradePartner.TID7} **SID**: {tradePartner.SID7}");

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
                    poke.SendNotification(this, "交易伙伴被阻止。正在取消交易。");
                    SendCollectedPokemonAndCleanup();
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                    return partnerCheck;
                }

                poke.SendNotification(this, $"找到交易伙伴: {tradePartner.TrainerName}。**TID**: {tradePartner.TID7} **SID**: {tradePartner.SID7}");

                // Apply AutoOT for first trade if needed
                if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
                {
                    toSend = await ApplyAutoOT(toSend, tradePartnerFullInfo, sav, token).ConfigureAwait(false);
                    poke.TradeData = toSend;
                    // Give game time to refresh trade offer display with AutoOT Pokemon
                    await Task.Delay(3_000, token).ConfigureAwait(false);
                }
            }

            if (currentTradeIndex == 0)
            {
                poke.SendNotification(this, $"请提供你的宝可梦进行交易 1/{totalBatchTrades}。");
            }

            var offsetBeforeBatch = await GetBoxStartOffset(token).ConfigureAwait(false);
            var pokemonBeforeBatchTrade = await ReadPokemon(offsetBeforeBatch, BoxFormatSlotSize, token).ConfigureAwait(false);
            var checksumBeforeBatchTrade = pokemonBeforeBatchTrade.Checksum;

            // Read the partner's offered Pokemon BEFORE we start pressing A to confirm
            var offeredBatch = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 3_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (offeredBatch == null || offeredBatch.Species == 0 || !offeredBatch.ChecksumValid)
            {
                Log($"交易 {currentTradeIndex + 1} 结束，因为训练家的报价撤回太快。");
                poke.SendNotification(this, $"交易伙伴未提供有效的宝可梦进行交易 {currentTradeIndex + 1}。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await DisconnectFromTrade(token).ConfigureAwait(false);
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerOfferCanceledQuick;
            }

            // Check if the offered Pokemon will evolve upon trade BEFORE confirming
            if (Hub.Config.Trade.TradeConfiguration.DisallowTradeEvolve && TradeEvolutions.WillTradeEvolve(offeredBatch.Species, offeredBatch.Form, offeredBatch.HeldItem, toSend.Species))
            {
                Log($"交易 {currentTradeIndex + 1} 已取消，因为训练家提供的宝可梦会在交易后进化。");
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1} 已取消。您不能交易会进化的宝可梦。为防止这种情况，请给您的宝可梦持有不变之石，或换一只宝可梦交易。");
                SendCollectedPokemonAndCleanup();
                await DisconnectFromTrade(token).ConfigureAwait(false);
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                return PokeTradeResult.TradeEvolveNotAllowed;
            }

            Log($"正在确认交易 {currentTradeIndex + 1}/{totalBatchTrades}。");
            var tradeResult = await ConfirmAndStartTrading(poke, checksumBeforeBatchTrade, token).ConfigureAwait(false);
            if (tradeResult != PokeTradeResult.Success)
            {
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 失败。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                if (tradeResult == PokeTradeResult.TrainerTooSlow)
                {
                    await DisconnectFromTrade(token).ConfigureAwait(false);
                }
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                return tradeResult;
            }

            // Wait for trade to complete
            Log($"正在确认交易 {currentTradeIndex + 1}/{totalBatchTrades}...");

            int maxBatchWaitSeconds = Hub.Config.Trade.TradeConfiguration.TradeWaitTime;
            int elapsedBatch = 0;
            bool batchTradeAnimationStarted = false;
            bool batchTradeCompleted = false;
            bool batchWarningSent = false;
            PA9? received = null;

            // First, wait for GameState to become 0x02 (trade animation in progress)
            while (elapsedBatch < maxBatchWaitSeconds && !batchTradeAnimationStarted)
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                elapsedBatch++;

                // Send warning 10 seconds before timeout
                if (!batchWarningSent && elapsedBatch == maxBatchWaitSeconds - 10 && maxBatchWaitSeconds >= 10)
                {
                    poke.SendNotification(this, "嘿！选择一只宝可梦交易，否则我要离开了！");
                    batchWarningSent = true;
                }

                var currentState = await GetGameState(token).ConfigureAwait(false);
                if (currentState == 0x02)
                {
                    batchTradeAnimationStarted = true;
                    Log($"交易 {currentTradeIndex + 1} 动画已开始");

                    // Read the received Pokemon from B1S1 (Pokemon swap has occurred)
                    boxOffset = await GetBoxStartOffset(token).ConfigureAwait(false);
                    received = await ReadPokemon(boxOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
                    Log($"交易 {currentTradeIndex + 1} - 收到 {(Species)received.Species}");

                    // Store the received Pokemon for later processing
                    BatchTracker.AddReceivedPokemon(originalTrainerID, received);

                    // Delay 1500ms
                    await Task.Delay(1_500, token).ConfigureAwait(false);

                    // Inject the next Pokemon so partner sees it when animation ends
                    if (currentTradeIndex + 1 < totalBatchTrades)
                    {
                        var nextPokemon = tradesToProcess[currentTradeIndex + 1];

                        // Apply AutoOT if needed
                        if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT && cachedTradePartnerInfo != null)
                        {
                            nextPokemon = await ApplyAutoOT(nextPokemon, cachedTradePartnerInfo, sav, token);
                            tradesToProcess[currentTradeIndex + 1] = nextPokemon;
                        }
                        else
                        {
                            // No AutoOT - inject directly
                            boxOffset = await GetBoxStartOffset(token).ConfigureAwait(false);
                            await SetBoxPokemonAbsolute(boxOffset, nextPokemon, token, sav).ConfigureAwait(false);
                        }

                        Log($"下一只宝可梦（{currentTradeIndex + 2}/{totalBatchTrades}）已在动画期间注入到 B1S1");
                    }
                }
            }

            if (!batchTradeAnimationStarted)
            {
                Log($"交易 {currentTradeIndex + 1}/{totalBatchTrades} 未确认。");
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 未确认。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await DisconnectFromTrade(token).ConfigureAwait(false);
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            // Now wait for GameState to return to 0x01 (trade animation complete)
            while (elapsedBatch < maxBatchWaitSeconds)
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                elapsedBatch++;

                var currentState = await GetGameState(token).ConfigureAwait(false);

                if (currentState == 0x01) // Trade animation finished!
                {
                    batchTradeCompleted = true;
                    break;
                }
            }

            if (!batchTradeCompleted)
            {
                Log($"交易 {currentTradeIndex + 1}/{totalBatchTrades} 超时。");
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 超时。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await DisconnectFromTrade(token).ConfigureAwait(false);
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            if (token.IsCancellationRequested)
            {
                StartFromOverworld = true;
                poke.SendNotification(this, "正在取消批量交易。");
                SendCollectedPokemonAndCleanup();
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                return PokeTradeResult.RoutineCancel;
            }

            // Validate that we received a Pokemon during the animation
            if (received == null || received.Species == 0)
            {
                Log($"交易 {currentTradeIndex + 1}/{totalBatchTrades} 失败 - 未收到宝可梦。");
                poke.SendNotification(this, $"交易 {currentTradeIndex + 1}/{totalBatchTrades} 已取消。正在取消剩余交易。");
                SendCollectedPokemonAndCleanup();
                await DisconnectFromTrade(token).ConfigureAwait(false);
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerTooSlow;
            }

            Log($"交易 {currentTradeIndex + 1}/{totalBatchTrades} 完成！收到 {(Species)received.Species}。");

            UpdateCountsAndExport(poke, received, toSend);

            // Get the trainer NID and name for logging
            var logTrainerNID = currentTradeIndex == 0 ? await GetTradePartnerNID(token).ConfigureAwait(false) : 0;
            var logPartner = cachedTradePartnerInfo != null ? new TradePartnerPLZA(cachedTradePartnerInfo) : null;
            LogSuccessfulTrades(poke, logTrainerNID, logPartner?.TrainerName ?? "未知");

            completedTrades = currentTradeIndex + 1;

            if (completedTrades == totalBatchTrades)
            {
                // Get all collected Pokemon before cleaning anything up
                var allReceived = BatchTracker.GetReceivedPokemon(originalTrainerID);

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

                // Exit the trade state
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                poke.IsProcessing = false;
                break;
            }

            // Next trade is already prepared - give game a moment to refresh the UI
            if (currentTradeIndex + 1 < totalBatchTrades)
            {
                Log($"准备下一笔交易（{currentTradeIndex + 2}/{totalBatchTrades}）...");
                await Task.Delay(2_000, token).ConfigureAwait(false);
            }
        }

        // Ensure we exit properly even if the loop breaks unexpectedly
        await ExitTradeToOverworld(false, token).ConfigureAwait(false);
        poke.IsProcessing = false;
        return PokeTradeResult.Success;
    }

    #endregion

    #region Core Trade Logic

    private async Task PerformTrade(SAV9ZA sav, PokeTradeDetail<PA9> detail, PokeRoutineType type, uint priority, CancellationToken token)
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

    private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV9ZA sav, PokeTradeDetail<PA9> poke, CancellationToken token)
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

        // Handle connection and portal entry FIRST
        if (!await EnsureConnectedAndInPortal(token).ConfigureAwait(false))
        {
            return PokeTradeResult.RecoverStart;
        }

        // Enter Link Trade and code
        var result = await EnterLinkTradeAndCode(poke, poke.Code, token).ConfigureAwait(false);

        if (result == LinkCodeEntryResult.VerificationFailedMismatch)
        {
            // Code didn't match - something went wrong, restart game
            Log("密码验证失败。正在重启游戏...");
            await RestartGamePLZA(token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }

        // Inject Pokemon AFTER code verification succeeds and BEFORE searching
        var toSend = poke.TradeData;
        if (toSend.Species != 0)
        {
            Log("正在准备交易宝可梦...");
            var offset = await GetBoxStartOffset(token).ConfigureAwait(false);
            await SetBoxPokemonAbsolute(offset, toSend, token, sav).ConfigureAwait(false);
        }

        StartFromOverworld = false;

        // Route to appropriate trade handling based on trade type
        if (poke.Type == PokeTradeType.Batch)
            return await PerformBatchTrade(sav, poke, token).ConfigureAwait(false);

        return await PerformNonBatchTrade(sav, poke, token).ConfigureAwait(false);
    }

    private async Task<bool> EnsureConnectedAndInPortal(CancellationToken token)
    {
        if (StartFromOverworld)
        {
            if (!await CheckIfOnOverworld(token).ConfigureAwait(false))
            {
                await RecoverToOverworld(token).ConfigureAwait(false);
            }

            if (!await ConnectAndEnterPortal(token).ConfigureAwait(false))
            {
                Log("连接错误。正在重启...");
                await RecoverToOverworld(token).ConfigureAwait(false);
                return false;
            }
        }
        else if (!await CheckIfConnectedOnline(token).ConfigureAwait(false))
        {
            await RecoverToOverworld(token).ConfigureAwait(false);
            if (!await ConnectAndEnterPortal(token).ConfigureAwait(false))
            {
                Log("连接失败。正在重启...");
                await RecoverToOverworld(token).ConfigureAwait(false);
                return false;
            }
        }

        return true;
    }

    private async Task<LinkCodeEntryResult> EnterLinkTradeAndCode(PokeTradeDetail<PA9> poke, int code, CancellationToken token)
    {
        // Loading code entry
        if (poke.Type != PokeTradeType.Random)
        {
            Hub.Config.Stream.StartEnterCode(this);
        }
        // Read current code to determine if we need to clear
        var currentCode = await GetCurrentLinkCode(token).ConfigureAwait(false);

        // If there's a non-zero code, clear it
        if (currentCode != 0)
        {
            var formattedCode = $"{currentCode:00000000}";
            var digitCount = formattedCode.Length;
            await Task.Delay(1_000, token).ConfigureAwait(false);

            for (int i = 0; i < digitCount; i++)
                await Click(B, 0, token).ConfigureAwait(false);

            await Task.Delay(1_000, token).ConfigureAwait(false);
        }

        // If code is 0, skip entering a code (blank code for random matchmaking)
        if (code != 0)
        {
            // Enter the new code
            await EnterLinkCode(code, Hub.Config, token).ConfigureAwait(false);
        }

        await Click(PLUS, 2_000, token).ConfigureAwait(false);

        // Verify the code was entered correctly (memory updates immediately after PLUS)
        var verifyCode = await GetCurrentLinkCode(token).ConfigureAwait(false);

        if (verifyCode != code)
        {
            return LinkCodeEntryResult.VerificationFailedMismatch;
        }

        return LinkCodeEntryResult.Success;
    }

    private async Task<PokeTradeResult> PerformNonBatchTrade(SAV9ZA sav, PokeTradeDetail<PA9> poke, CancellationToken token)
    {
        var toSend = poke.TradeData;

        await Click(A, 0_500, token).ConfigureAwait(false);
        await Click(A, 0_500, token).ConfigureAwait(false);

        WaitAtBarrierIfApplicable(token);
        await Click(A, 1_000, token).ConfigureAwait(false);

        poke.TradeSearching(this);
        var partnerWaitResult = await WaitForTradePartner(token).ConfigureAwait(false);

        if (token.IsCancellationRequested)
        {
            StartFromOverworld = true;
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return PokeTradeResult.RoutineCancel;
        }

        if (partnerWaitResult == TradePartnerWaitResult.Timeout)
        {
            // Partner never showed up - their fault, don't requeue
            poke.IsProcessing = false;
            poke.SendNotification(this, "未找到交易伙伴。正在取消交易。");
            poke.TradeCanceled(this, PokeTradeResult.NoTrainerFound);

            await RecoverToOverworld(token).ConfigureAwait(false);
            return PokeTradeResult.NoTrainerFound;
        }

        if (partnerWaitResult == TradePartnerWaitResult.KickedToMenu)
        {
            // Bot got kicked to menu - our fault, trigger requeue
            Log("连接错误。正在重试...");
            await RecoverToOverworld(token).ConfigureAwait(false);
            return PokeTradeResult.RecoverStart;
        }

        Hub.Config.Stream.EndEnterCode(this);

        // Wait until we're in the trade box
        Log("正在搜索交易伙伴...");
        int boxCheckAttempts = 0;
        while (!await IsOnMenu(MenuState.InBox, token).ConfigureAwait(false))
        {
            await Task.Delay(500, token).ConfigureAwait(false);
            if (++boxCheckAttempts > 30) // 15 seconds max
            {
                Log("未找到交易伙伴。");
                return PokeTradeResult.NoTrainerFound;
            }
        }

        // Wait for trade UI and partner data to load
        await Task.Delay(5_000, token).ConfigureAwait(false);

        // Now that data has loaded, read partner info
        var tradePartnerFullInfo = await GetTradePartnerFullInfo(token).ConfigureAwait(false);
        var tradePartner = new TradePartnerPLZA(tradePartnerFullInfo);

        var trainerNID = await GetTradePartnerNID(token).ConfigureAwait(false);

        Log($"[TradePartner] OT: {tradePartner.TrainerName}, TID: {tradePartner.TID7}, SID: {tradePartner.SID7}, Gender: {tradePartnerFullInfo.Gender}, Language: {tradePartnerFullInfo.Language}, NID: {trainerNID}");

        RecordUtil<PokeTradeBotPLZA>.Record($"Initiating\t{trainerNID:X16}\t{tradePartner.TrainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");
        poke.SendNotification(this, $"找到交易伙伴: {tradePartner.TrainerName}。**TID**: {tradePartner.TID7} **SID**: {tradePartner.SID7} 等待宝可梦...");

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
            await Click(A, 1_000, token).ConfigureAwait(false);
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return partnerCheck;
        }

        // Read the offered Pokemon for Clone/Dump trades
        PA9? offered = null;
        if (poke.Type == PokeTradeType.Clone || poke.Type == PokeTradeType.Dump)
        {
            offered = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 3_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (offered == null || offered.Species == 0)
            {
                poke.SendNotification(this, "读取提供的宝可梦失败。退出交易。");
                await ExitTradeToOverworld(true, token).ConfigureAwait(false);
                return PokeTradeResult.TrainerRequestBad;
            }
        }

        if (poke.Type == PokeTradeType.Clone)
        {
            var (result, clone) = await ProcessCloneTradeAsync(poke, sav, offered!, token).ConfigureAwait(false);
            if (result != PokeTradeResult.Success)
            {
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
                return result;
            }

            // Trade them back their cloned Pokemon
            toSend = clone!;
        }

        if (poke.Type == PokeTradeType.Dump)
        {
            var result = await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return result;
        }

        if (Hub.Config.Legality.UseTradePartnerInfo && !poke.IgnoreAutoOT)
        {
            toSend = await ApplyAutoOT(toSend, tradePartnerFullInfo, sav, token);
            // Give game time to refresh trade offer display with AutoOT Pokemon
            await Task.Delay(3_000, token).ConfigureAwait(false);
        }

        SpecialTradeType itemReq = SpecialTradeType.None;
        if (poke.Type == PokeTradeType.Seed)
        {
            poke.SendNotification(this, "种子交易暂时不可用。请改为请求特定的宝可梦。");
            await ExitTradeToOverworld(true, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerRequestBad;
        }

        if (itemReq == SpecialTradeType.WonderCard)
            poke.SendNotification(this, "分发成功！");
        else if (itemReq != SpecialTradeType.None && itemReq != SpecialTradeType.Shinify)
            poke.SendNotification(this, "特殊请求成功！");
        else if (itemReq == SpecialTradeType.Shinify)
            poke.SendNotification(this, "闪光化成功！感谢你成为社区的一员！");

        var offsetBefore = await GetBoxStartOffset(token).ConfigureAwait(false);
        var pokemonBeforeTrade = await ReadPokemon(offsetBefore, BoxFormatSlotSize, token).ConfigureAwait(false);
        var checksumBeforeTrade = pokemonBeforeTrade.Checksum;

        // Read the partner's offered Pokemon BEFORE we start pressing A to confirm
        // This way we can cancel with B+A if they're offering something that will evolve
        if (offered == null) // Only read if we haven't already (Clone/Dump read it earlier)
        {
            offered = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 3_000, 0_500, BoxFormatSlotSize, token).ConfigureAwait(false);
        }

        if (offered == null || offered.Species == 0 || !offered.ChecksumValid)
        {
            Log("交易结束，因为训练家的报价撤回太快。");
            poke.SendNotification(this, "交易伙伴未提供有效的宝可梦。");
            await DisconnectFromTrade(token).ConfigureAwait(false);
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerOfferCanceledQuick;
        }

        // Check if the offered Pokemon will evolve upon trade BEFORE confirming
        if (Hub.Config.Trade.TradeConfiguration.DisallowTradeEvolve && TradeEvolutions.WillTradeEvolve(offered.Species, offered.Form, offered.HeldItem, toSend.Species))
        {
            Log("交易已取消，因为训练家提供的宝可梦会在交易后进化。");
            poke.SendNotification(this, "交易已取消。您不能交易会进化的宝可梦。为防止这种情况，请给您的宝可梦持有不变之石，或换一只宝可梦交易。");
            await DisconnectFromTrade(token).ConfigureAwait(false);
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return PokeTradeResult.TradeEvolveNotAllowed;
        }

        Log("正在确认交易。");
        var tradeResult = await ConfirmAndStartTrading(poke, checksumBeforeTrade, token).ConfigureAwait(false);
        if (tradeResult != PokeTradeResult.Success)
        {
            if (tradeResult == PokeTradeResult.TrainerTooSlow)
            {
                await DisconnectFromTrade(token).ConfigureAwait(false);
            }
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return tradeResult;
        }

        if (token.IsCancellationRequested)
        {
            StartFromOverworld = true;
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return PokeTradeResult.RoutineCancel;
        }

        Log("正在确认交易...");

        int maxWaitSeconds = Hub.Config.Trade.TradeConfiguration.TradeWaitTime;
        int elapsed = 0;
        bool tradeAnimationStarted = false;
        bool tradeCompleted = false;
        bool warningSent = false;

        // First, wait for GameState to become 0x02 (trade animation in progress)
        while (elapsed < maxWaitSeconds && !tradeAnimationStarted)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            elapsed++;

            // Send warning 10 seconds before timeout
            if (!warningSent && elapsed == maxWaitSeconds - 10 && maxWaitSeconds >= 10)
            {
                poke.SendNotification(this, "嘿！选择一只宝可梦交易，否则我要离开了！");
                warningSent = true;
            }

            var currentState = await GetGameState(token).ConfigureAwait(false);
            if (currentState == 0x02)
            {
                tradeAnimationStarted = true;
            }
        }

        if (!tradeAnimationStarted)
        {
            Log("交易未确认。");
            await DisconnectFromTrade(token).ConfigureAwait(false);
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        // Now wait for GameState to return to 0x01 (trade animation complete)
        while (elapsed < maxWaitSeconds)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            elapsed++;

            var currentState = await GetGameState(token).ConfigureAwait(false);
            if (currentState == 0x01)
            {
                tradeCompleted = true;
                break;
            }
        }

        if (!tradeCompleted)
        {
            Log("交易超时。");
            await DisconnectFromTrade(token).ConfigureAwait(false);
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        // CRITICAL: Verify that Box 1 Slot 1 actually changed (trade occurred)
        var offset2 = await GetBoxStartOffset(token).ConfigureAwait(false);
        var received = await ReadPokemon(offset2, BoxFormatSlotSize, token).ConfigureAwait(false);
        var checksumAfterTrade = received.Checksum;

        if (checksumBeforeTrade == checksumAfterTrade)
        {
            Log("交易已取消。");
            poke.SendNotification(this, "交易已取消。请重试。");
            await DisconnectFromTrade(token).ConfigureAwait(false);
            await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            return PokeTradeResult.TrainerTooSlow;
        }

        Log($"交易完成！收到 {(Species)received.Species}。");

        poke.TradeFinished(this, received);
        UpdateCountsAndExport(poke, received, toSend);
        LogSuccessfulTrades(poke, trainerNID, tradePartner.TrainerName);

        await ExitTradeToOverworld(false, token).ConfigureAwait(false);
        return PokeTradeResult.Success;
    }

    private async Task HandleAbortedBatchTrade(PokeTradeDetail<PA9> detail, PokeRoutineType type, uint priority, PokeTradeResult result, CancellationToken token)
    {
        detail.IsProcessing = false;

        // Always remove from UsersInQueue on abort
        Hub.Queues.Info.Remove(new TradeEntry<PA9>(detail, detail.Trainer.ID, type, detail.Trainer.TrainerName, detail.UniqueTradeID));

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
                await ExitTradeToOverworld(false, token).ConfigureAwait(false);
            }
        }
        else
        {
            HandleAbortedTrade(detail, type, priority, result);
        }
    }

    private async Task<bool> RecoverToOverworld(CancellationToken token)
    {
        if (await CheckIfOnOverworld(token).ConfigureAwait(false))
            return true;

        Log("正在恢复...");

        await Click(B, 1_500, token).ConfigureAwait(false);
        if (await CheckIfOnOverworld(token).ConfigureAwait(false))
            return true;

        await Click(A, 1_500, token).ConfigureAwait(false);
        if (await CheckIfOnOverworld(token).ConfigureAwait(false))
            return true;

        var attempts = 0;
        while (!await CheckIfOnOverworld(token).ConfigureAwait(false))
        {
            attempts++;
            if (attempts >= 30)
                break;

            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await CheckIfOnOverworld(token).ConfigureAwait(false))
                break;

            await Click(B, 1_000, token).ConfigureAwait(false);
            if (await CheckIfOnOverworld(token).ConfigureAwait(false))
                break;
        }

        if (!await CheckIfOnOverworld(token).ConfigureAwait(false))
        {
            Log("正在重启游戏...");
            await RestartGamePLZA(token).ConfigureAwait(false);
        }
        await Task.Delay(1_000, token).ConfigureAwait(false);

        StartFromOverworld = true;
        return true;
    }

    private async Task RestartGamePLZA(CancellationToken token)
    {
        await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
        _cachedBoxOffset = null; // Invalidate box offset cache after restart

        // If we were connected to a partner before restart, prevent soft ban
        if (_wasConnectedToPartner)
        {
            Log("防止交易软封禁 - 连接随机伙伴以清除交易状态...");
            await PreventTradeSoftBan(token).ConfigureAwait(false);
            _wasConnectedToPartner = false; // Reset the flag after recovery
        }
    }

    /// <summary>
    /// Prevents trade soft ban after restarting during an active trade connection.
    ///
    /// When the bot restarts AFTER successfully connecting to a trade partner (verified via MenuState.InBox),
    /// the game may impose a soft ban if we attempt to trade again without clearing the previous connection state.
    ///
    /// This method connects to a random partner (no code) and immediately disconnects using B+A to signal
    /// to the game servers that the previous trade session has ended, preventing the soft ban.
    /// </summary>
    private async Task PreventTradeSoftBan(CancellationToken token)
    {
        await Task.Delay(5_000, token).ConfigureAwait(false);

        if (!await CheckIfOnOverworld(token).ConfigureAwait(false))
        {
            Log("重启后不在大地图，正在尝试恢复...");
            await RecoverToOverworld(token).ConfigureAwait(false);
        }

        Log("正在连接网络以防止交易软封禁...");
        await Click(X, 3_000, token).ConfigureAwait(false);
        await Click(DUP, 1_000, token).ConfigureAwait(false);
        await Click(A, 2_000, token).ConfigureAwait(false);
        await Click(DRIGHT, 1_000, token).ConfigureAwait(false);
        await Click(DRIGHT, 1_000, token).ConfigureAwait(false);
        await Click(A, 1_000, token).ConfigureAwait(false);
        await Click(DRIGHT, 1_000, token).ConfigureAwait(false);
        await Click(A, 1_000, token).ConfigureAwait(false);

        int attempts = 0;
        while (!await CheckIfConnectedOnline(token).ConfigureAwait(false))
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            if (++attempts > 30)
            {
                Log("软封禁防止期间连接网络失败。");
                await RecoverToOverworld(token).ConfigureAwait(false);
                return;
            }
        }
        await Task.Delay(8_000 + Hub.Config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);
        Log("已连接网络以防止软封禁。");

        await Click(A, 1_000, token).ConfigureAwait(false);
        await Click(A, 1_000, token).ConfigureAwait(false);
        await Task.Delay(3_000, token).ConfigureAwait(false);

        Log("正在连接随机伙伴以清除之前的交易会话...");
        await Click(PLUS, 2_000, token).ConfigureAwait(false);

        Log("正在等待随机伙伴连接...");
        await Task.Delay(3_000, token).ConfigureAwait(false);

        int waitAttempts = 0;
        bool connected = false;
        while (waitAttempts < 30 && !connected)
        {
            var nid = await GetTradePartnerNID(token).ConfigureAwait(false);
            if (nid != 0)
            {
                Log("随机伙伴通过 NID 连接。断开连接以完成软封禁防止...");
                connected = true;
                break;
            }

            if (await IsOnMenu(MenuState.InBox, token).ConfigureAwait(false))
            {
                Log("随机伙伴通过交易箱连接。断开连接以完成软封禁防止...");
                connected = true;
                break;
            }

            await Task.Delay(1_000, token).ConfigureAwait(false);
            waitAttempts++;
        }

        if (!connected)
        {
            Log("30 秒内未找到随机伙伴。软封禁可能未完全防止。继续...");
            await RecoverToOverworld(token).ConfigureAwait(false);
            return;
        }

        Log("正在断开与随机伙伴的连接（B 取消，A 确认）...");
        await Click(B, 1_000, token).ConfigureAwait(false);
        await Click(A, 1_000, token).ConfigureAwait(false);

        Log("等待伙伴断开连接确认...");
        int disconnectAttempts = 0;
        bool partnerDisconnected = false;
        while (disconnectAttempts < 10 && !partnerDisconnected)
        {
            await Task.Delay(500, token).ConfigureAwait(false);
            var currentNid = await GetTradePartnerNID(token).ConfigureAwait(false);
            if (currentNid == 0)
            {
                Log("伙伴已断开连接（NID = 0）。正在退出到大地图...");
                partnerDisconnected = true;
                break;
            }
            disconnectAttempts++;
        }

        if (!partnerDisconnected)
        {
            Log("伙伴未在超时时间内断开连接。强制退出...");
        }

        Log("连续按 B 返回大地图...");
        for (int i = 0; i < 15; i++)
        {
            await Click(B, 1_000, token).ConfigureAwait(false);

            if (await CheckIfOnOverworld(token).ConfigureAwait(false))
            {
                Log("软封禁防止完成。已成功返回大地图。");
                StartFromOverworld = true;
                return;
            }
        }

        Log("连续按 B 后仍无法返回大地图。正在执行完全恢复...");
        await RecoverToOverworld(token).ConfigureAwait(false);
        StartFromOverworld = true;
    }

    #endregion

    #region Multi-Bot Synchronization

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

    private void UpdateCountsAndExport(PokeTradeDetail<PA9> poke, PA9 received, PA9 toSend)
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

    #region Clone & Dump Features

    private async Task<bool> CheckCloneChangedOffer(CancellationToken token)
    {
        // Watch their status to indicate they canceled, then offered a new Pokémon.
        var hovering = await ReadUntilChanged(TradePartnerStatusOffset, [0x2], 25_000, 1_000, true, true, token).ConfigureAwait(false);
        if (!hovering)
        {
            Log("交易伙伴未更换最初的报价。");
            return false;
        }
        var offering = await ReadUntilChanged(TradePartnerStatusOffset, [0x3], 25_000, 1_000, true, true, token).ConfigureAwait(false);
        if (!offering)
        {
            return false;
        }
        return true;
    }

    private async Task<(PokeTradeResult Result, PA9? ClonedPokemon)> ProcessCloneTradeAsync(PokeTradeDetail<PA9> poke, SAV9ZA sav, PA9 offered, CancellationToken token)
    {
        if (Hub.Config.Discord.ReturnPKMs)
            poke.SendNotification(this, offered, "这是你展示给我的！");

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

            return (PokeTradeResult.IllegalTrade, null);
        }

        var clone = offered.Clone();
        if (Hub.Config.Legality.ResetHOMETracker)
            clone.Tracker = 0;

        poke.SendNotification(this, $"**Cloned your {GameInfo.GetStrings("en").Species[clone.Species]}!**\nNow press B to cancel your offer and trade me a Pokémon you don't want.");
        Log($"已克隆 {(Species)clone.Species}。等待用户更换宝可梦...");

        if (!await CheckCloneChangedOffer(token).ConfigureAwait(false))
        {
            // They get one more chance.
            poke.SendNotification(this, "**嘿，立即更换，否则我要离开了！！！**");
            if (!await CheckCloneChangedOffer(token).ConfigureAwait(false))
            {
                Log("交易伙伴未更换宝可梦。");
                return (PokeTradeResult.TrainerTooSlow, null);
            }
        }

        // If we got to here, we can read their offered Pokémon.
        var pk2 = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 5_000, 1_000, BoxFormatSlotSize, token).ConfigureAwait(false);
        if (pk2 is null || SearchUtil.HashByDetails(pk2) == SearchUtil.HashByDetails(offered))
        {
            Log("交易伙伴未更换宝可梦。");
            return (PokeTradeResult.TrainerTooSlow, null);
        }

        var boxOffset = await GetBoxStartOffset(token).ConfigureAwait(false);
        await SetBoxPokemonAbsolute(boxOffset, clone, token, sav).ConfigureAwait(false);

        return (PokeTradeResult.Success, clone);
    }

    private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PA9> detail, CancellationToken token)
    {
        int ctr = 0;
        var maxDumps = Hub.Config.Trade.TradeConfiguration.MaxDumpsPerTrade;
        var time = TimeSpan.FromSeconds(Hub.Config.Trade.TradeConfiguration.MaxDumpTradeTime);
        var start = DateTime.Now;

        // Tell the user what to do
        detail.SendNotification(this, $"现在展示你的宝可梦！你最多可以展示 {maxDumps} 只宝可梦。继续更换宝可梦来转储更多！");

        var pkprev = new PA9();
        var warnedAboutTime = false;
        var bctr = 0;

        while (ctr < maxDumps && DateTime.Now - start < time)
        {
            // Check if we're still in the trade box (user disconnected if not)
            if (!await IsOnMenu(MenuState.InBox, token).ConfigureAwait(false))
            {
                Log("交易伙伴已断开连接（不在交易箱中）。");
                break;
            }

            // Periodic B button press to keep connection alive
            if (bctr++ % 3 == 0)
                await Click(B, 0_100, token).ConfigureAwait(false);

            // Warn user when they're running low on time
            var elapsed = DateTime.Now - start;
            if (!warnedAboutTime && elapsed.TotalSeconds > time.TotalSeconds - 15)
            {
                detail.SendNotification(this, "只剩 15 秒！展示你最后一只宝可梦或按 B 退出。");
                warnedAboutTime = true;
            }

            // Wait for the user to show us a Pokemon - needs to be different from the previous one
            var pk = await ReadUntilPresentPointer(Offsets.LinkTradePartnerPokemonPointer, 3_000, 0_050, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (pk == null || pk.Species == 0 || !pk.ChecksumValid)
            {
                await Task.Delay(0_050, token).ConfigureAwait(false);
                continue;
            }

            // Check if this is the same Pokemon as before
            if (SearchUtil.HashByDetails(pk) == SearchUtil.HashByDetails(pkprev))
            {
                Log($"用户展示的是之前相同的宝可梦。等待不同的宝可梦...");
                await Task.Delay(0_500, token).ConfigureAwait(false);
                continue;
            }

            // Heal and refresh checksum to ensure valid data
            pk.Heal();
            pk.RefreshChecksum();

            // Save the new Pokemon for comparison next round
            pkprev = pk;

            // Dump the Pokemon to file if dumping is enabled
            if (DumpSetting.Dump)
            {
                var subfolder = detail.Type.ToString().ToLower();
                DumpPokemon(DumpSetting.DumpFolder, subfolder, pk);
            }

            var la = new LegalityAnalysis(pk);
            var verbose = $"```{la.Report(true)}```";
            Log($"展示的宝可梦是: {(la.Valid ? "合法" : "非法")}。");

            ctr++;
            var msg = Hub.Config.Trade.TradeConfiguration.DumpTradeLegalityCheck ? verbose : $"文件 {ctr}";

            // Include trainer data for people requesting with their own trainer data
            var ot = pk.OriginalTrainerName;
            var ot_gender = pk.OriginalTrainerGender == 0 ? "Male" : "Female";
            var tid = pk.GetDisplayTID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringTID());
            var sid = pk.GetDisplaySID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringSID());
            msg += $"\n**Trainer Data**\n```OT: {ot}\nOTGender: {ot_gender}\nTID: {tid}\nSID: {sid}```";

            // Extra information for shiny eggs
            var eggstring = pk.IsEgg ? "Egg " : string.Empty;
            msg += pk.IsShiny ? $"\n**This Pokémon {eggstring}is shiny!**" : string.Empty;

            // Send the Pokemon file back to the user via Discord
            detail.SendNotification(this, pk, msg);

            // Tell user their progress
            var remaining = maxDumps - ctr;
            if (remaining > 0)
                detail.SendNotification(this, $"已接收！你还可以展示 {remaining} 只。展示不同的宝可梦继续，或按 B 退出。");
            else
                detail.SendNotification(this, "已达到上限！按 B 退出交易。");
        }

        var timeElapsed = DateTime.Now - start;
        Log($"处理 {ctr} 只宝可梦后结束转储循环，耗时 {timeElapsed.TotalSeconds:F1} 秒。");

        if (ctr == 0)
            return PokeTradeResult.TrainerTooSlow;

        TradeSettings.CountStatsSettings.AddCompletedDumps();
        detail.Notifier.SendNotification(this, detail, $"已转储 {ctr} 只宝可梦。");
        detail.Notifier.TradeFinished(this, detail, pkprev); // Send last dumped Pokemon
        return PokeTradeResult.Success;
    }

    #endregion

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
            Log("等待交易请求...");
        }

        return Task.Delay(1_000, token);
    }

    #endregion
}
