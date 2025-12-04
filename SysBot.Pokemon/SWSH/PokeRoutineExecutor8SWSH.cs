using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon;

/// <summary>
/// 剑盾例程执行器。
/// </summary>
public abstract class PokeRoutineExecutor8SWSH(PokeBotState Config) : PokeRoutineExecutor<PK8>(Config)
{
    protected PokeDataOffsetsSWSH Offsets { get; } = new();

    public async Task<bool> CheckIfSoftBanned(CancellationToken token)
    {
        // 检查 Unix 时间戳是否非零，若非零则表示被软封禁。
        var data = await Connection.ReadBytesAsync(SoftBanUnixTimespanOffset, 1, token).ConfigureAwait(false);
        return data[0] > 1;
    }

    public async Task CleanExit(CancellationToken token)
    {
        await SetScreen(ScreenState.On, token).ConfigureAwait(false);
        Log("例程结束时正在断开控制器。");
        await DetachController(token).ConfigureAwait(false);
    }

    public async Task CloseGame(PokeTradeHubConfig config, CancellationToken token)
    {
        var timing = config.Timings;

        // 退出游戏
        await Click(B, 0_500, token).ConfigureAwait(false);
        await Click(HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
        await Click(X, 1_000, token).ConfigureAwait(false);
        await Click(A, 5_000 + timing.ExtraTimeCloseGame, token).ConfigureAwait(false);
        Log("已退出游戏！");
    }

    public async Task EnsureConnectedToYComm(ulong overworldOffset, PokeTradeHubConfig config, CancellationToken token)
    {
        if (!await IsGameConnectedToYComm(token).ConfigureAwait(false))
        {
            Log("正在重新连接 Y 通讯…");
            await ReconnectToYComm(overworldOffset, config, token).ConfigureAwait(false);
        }
    }

    public async Task<byte> GetCurrentBox(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(CurrentBoxOffset, 1, token).ConfigureAwait(false);
        return data[0];
    }

    public async Task<uint> GetCurrentScreen(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
        return BitConverter.ToUInt32(data, 0);
    }

    /// <summary>
    /// 标识训练家信息并加载当前运行时语言。
    /// </summary>
    public async Task<SAV8SWSH> GetFakeTrainerSAV(CancellationToken token)
    {
        var sav = new SAV8SWSH();
        var info = sav.MyStatus;
        var read = await Connection.ReadBytesAsync(TrainerDataOffset, TrainerDataLength, token).ConfigureAwait(false);

        read.CopyTo(info.Data);
        return sav;
    }

    public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
        return (TextSpeedOption)(data[0] & 3);
    }

    public async Task<SAV8SWSH> IdentifyTrainer(CancellationToken token)
    {
        // 检查 Botbase 是否为正确版本或更高版本。
        await VerifyBotbaseVersion(token).ConfigureAwait(false);

        // 检查标题 ID，以便在模式选择错误时提醒。
        string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
        if (title is not (SwordID or ShieldID))
            throw new Exception($"{title} 不是有效的剑/盾标题，请确认已选择正确模式。");

        // 核对游戏版本。
        var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
        if (!game_version.SequenceEqual(SWSHGameVersion))
            throw new Exception($"当前游戏版本不受支持，应为 {SWSHGameVersion}，检测到 {game_version}。");

        Log("正在读取主机的训练家数据…");
        var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
        InitSaveData(sav);

        if (!IsValidTrainerData())
        {
            await CheckForRAMShiftingApps(token).ConfigureAwait(false);
            throw new Exception("训练家数据无效，请参阅 SysBot.NET Wiki（https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting）了解详情。");
        }

        if (await GetTextSpeed(token).ConfigureAwait(false) < TextSpeedOption.Fast)
            throw new Exception("文本速度需设置为“快速”，请调整后再继续。");

        return sav;
    }

    public async Task InitializeHardware(IBotStateSettings settings, CancellationToken token)
    {
        Log("启动时正在断开控制器。");
        await DetachController(token).ConfigureAwait(false);
        if (settings.ScreenOff)
        {
            Log("正在关闭屏幕。");
            await SetScreen(ScreenState.Off, token).ConfigureAwait(false);
        }
        await SetController(ControllerType.ProController, token);
    }

    public async Task<bool> IsCorrectScreen(uint expectedScreen, CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
        return BitConverter.ToUInt32(data, 0) == expectedScreen;
    }

    public async Task<bool> IsGameConnectedToYComm(CancellationToken token)
    {
        // 读取 Y 通讯标志以检查游戏是否联网
        var data = await Connection.ReadBytesAsync(IsConnectedOffset, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    public async Task<bool> IsInBattle(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(Version == GameVersion.SH ? InBattleRaidOffsetSH : InBattleRaidOffsetSW, 1, token).ConfigureAwait(false);
        return data[0] == (Version == GameVersion.SH ? 0x40 : 0x41);
    }

    public async Task<bool> IsInBox(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
        var dataint = BitConverter.ToUInt32(data, 0);
        return dataint is CurrentScreen_Box1 or CurrentScreen_Box2;
    }

    public async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    public override Task<PK8> ReadBoxPokemon(int box, int slot, CancellationToken token)
    {
        var ofs = GetBoxSlotOffset(box, slot);
        return ReadPokemon(ofs, BoxFormatSlotSize, token);
    }

    public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
    {
        var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
        return !result.SequenceEqual(original);
    }

    public override Task<PK8> ReadPokemon(ulong offset, CancellationToken token) => ReadPokemon(offset, BoxFormatSlotSize, token);

    public override async Task<PK8> ReadPokemon(ulong offset, int size, CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync((uint)offset, size, token).ConfigureAwait(false);
        return new PK8(data);
    }

    public override async Task<PK8> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return new PK8();
        return await ReadPokemon(offset, token).ConfigureAwait(false);
    }

    public async Task<PK8> ReadSurpriseTradePokemon(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(SurpriseTradePartnerPokemonOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
        return new PK8(data);
    }

    public async Task ReconnectToYComm(ulong overworldOffset, PokeTradeHubConfig config, CancellationToken token)
    {
        // 先按 B，防止存在错误弹窗
        await Click(B, 2000, token).ConfigureAwait(false);

        // 返回场景
        if (!await IsOnOverworld(overworldOffset, token).ConfigureAwait(false))
        {
            for (int i = 0; i < 5; i++)
            {
                await Click(B, 500, token).ConfigureAwait(false);
            }
        }

        await Click(Y, 1000, token).ConfigureAwait(false);

        // 为确保成功按两次，防止第一次未生效。
        await Click(PLUS, 2_000, token).ConfigureAwait(false);
        await Click(PLUS, 5_000 + config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);

        for (int i = 0; i < 5; i++)
        {
            await Click(B, 500, token).ConfigureAwait(false);
        }
    }

    public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
    {
        // 遭遇软封禁时重新启动游戏
        Log("检测到可能的软封禁，正在重新启动游戏以防万一！");
        await CloseGame(config, token).ConfigureAwait(false);
        await StartGame(config, token).ConfigureAwait(false);

        // 若确实被封禁则重置时间戳
        await UnSoftBan(token).ConfigureAwait(false);
    }

    public Task SetBoxPokemon(PK8 pkm, int box, int slot, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            pkm.UpdateHandler(sav);
            pkm.RefreshChecksum();
        }
        var ofs = GetBoxSlotOffset(box, slot);
        pkm.ResetPartyStats();
        return Connection.WriteBytesAsync(pkm.EncryptedPartyData, ofs, token);
    }

    public Task SetCurrentBox(byte box, CancellationToken token)
    {
        return Connection.WriteBytesAsync([box], CurrentBoxOffset, token);
    }

    public async Task SetTextSpeed(TextSpeedOption speed, CancellationToken token)
    {
        var textSpeedByte = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
        var data = new[] { (byte)((textSpeedByte[0] & 0xFC) | (int)speed) };
        await Connection.WriteBytesAsync(data, TextSpeedOffset, token).ConfigureAwait(false);
    }

        // 切换到盒 1 并清空槽位 1，为化石/孵蛋机器人做好准备。
    public async Task SetupBoxState(IDumper DumpSetting, CancellationToken token)
    {
        await SetCurrentBox(0, token).ConfigureAwait(false);

        var existing = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
        if (existing.Species != 0 && existing.ChecksumValid)
        {
            Log("目标槽位已有宝可梦，正在转储该宝可梦…");
            DumpPokemon(DumpSetting.DumpFolder, "saved", existing);
        }

        Log("正在清空目标槽位，准备启动机器人。");
        PK8 blank = new();
        await SetBoxPokemon(blank, 0, 0, token).ConfigureAwait(false);
    }

    public async Task StartGame(PokeTradeHubConfig config, CancellationToken token)
    {

        // Open game.
        var timing = config.Timings;
        var loadPro = timing.ProfileSelectionRequired ? timing.ExtraTimeLoadProfile : 0;

        await Click(A, 1_000 + loadPro, token).ConfigureAwait(false); // 初始 “A” 用于开始游戏，如需等待档案则额外延时

        // 菜单可能按以下顺序出现：系统更新提示 -> 档案选择 -> DLC 检查 -> 无法使用 DLC。
        // 若用户知道系统更新会导致异常，可提前开启相关设置避开更新。
        if (timing.AvoidSystemUpdate)
        {
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);
        }

        // 仅在需要时才额外按键
        if (timing.ProfileSelectionRequired)
        {
            await Click(A, 1_000, token).ConfigureAwait(false); // 进入档案界面
            await Click(A, 1_000, token).ConfigureAwait(false); // 选择档案
        }

        // 数字版游戏加载更久
        if (timing.CheckGameDelay)
        {
            await Task.Delay(2_000 + timing.ExtraTimeCheckGame, token).ConfigureAwait(false);
        }

        await Click(A, 0_600, token).ConfigureAwait(false);

        Log("正在重启游戏！");

        // Switch Logo 延迟、跳过过场、加载游戏
        await Task.Delay(10_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

        for (int i = 0; i < 4; i++)
            await Click(A, 1_000, token).ConfigureAwait(false);

        var timer = 60_000;
        while (!await IsOnOverworldTitle(token).ConfigureAwait(false) && !await IsInBattle(token).ConfigureAwait(false))
        {
            await Task.Delay(0_200, token).ConfigureAwait(false);
            timer -= 0_250;

            // 若 1 分钟仍未回到场景，则每 6 秒按一次 A 尝试重启；若配置要求避免更新，则不冒险。
            if (timer <= 0 && !timing.AvoidSystemUpdate)
            {
                Log("仍未进入游戏，正在启动救援流程！");
                while (!await IsOnOverworldTitle(token).ConfigureAwait(false) && !await IsInBattle(token).ConfigureAwait(false))
                    await Click(A, 6_000, token).ConfigureAwait(false);
                break;
            }
        }

        Log("已返回场景！");
    }

    public Task UnSoftBan(CancellationToken token)
    {
        // 与旧世代相同，游戏使用 Unix 时间戳记录软封禁时长，解禁后会将其重置为 0（1970/01/01 00:00 UTC）。
        Log("检测到软封禁，正在解除。");
        var data = BitConverter.GetBytes(0);
        return Connection.WriteBytesAsync(data, SoftBanUnixTimespanOffset, token);
    }

    protected virtual async Task EnterLinkCode(int code, PokeTradeHubConfig config, CancellationToken token)
    {
        // Default implementation to just press directional arrows. Can do via Hid keys, but users are slower than bots at even the default code entry.
        foreach (var key in TradeUtil.GetPresses(code))
        {
            int delay = config.Timings.KeypressTime;
            await Click(key, delay, token).ConfigureAwait(false);
        }

        // Confirm Code outside of this method (allow synchronization)
    }

    private static uint GetBoxSlotOffset(int box, int slot) => BoxStartOffset + (uint)(BoxFormatSlotSize * ((30 * box) + slot));

    // Only used to check if we made it off the title screen.
    private async Task<bool> IsOnOverworldTitle(CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
        if (!valid)
            return false;
        return await IsOnOverworld(offset, token).ConfigureAwait(false);
    }
}
