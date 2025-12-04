using PKHeX.Core;
using SysBot.Base;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

public abstract class PokeRoutineExecutorBase(IConsoleBotManaged<IConsoleConnection, IConsoleConnectionAsync> Config)
    : SwitchRoutineExecutor<PokeBotState>(Config)
{
    public const decimal BotbaseVersion = 2.4m;

    public static readonly TrackedUserLog PreviousUsers = new();

    public static readonly TrackedUserLog PreviousUsersDistribution = new();

    public LanguageID GameLang { get; private set; }

    public string InGameName { get; private set; } = "Shinypkm.com";

    public GameVersion Version { get; private set; }

    /// <summary>
    /// Display-friendly label showing in-game trainer info
    /// Used for UI/display purposes only - NOT for logging
    /// </summary>
    public string TrainerLabel { get; private set; } = string.Empty;

    public Task Click(SwitchButton b, int delayMin, int delayMax, CancellationToken token) =>
        Click(b, Util.Rand.Next(delayMin, delayMax), token);

    public override string GetSummary()
    {
        var current = Config.CurrentRoutineType;
        var initial = Config.InitialRoutine;
        // 如果可用则优先使用 TrainerLabel（显示游戏内名称），否则使用 Connection.Name（IP/USB）
        var displayLabel = !string.IsNullOrEmpty(TrainerLabel) ? TrainerLabel : Connection.Name;
        if (current == initial)
            return $"{displayLabel} - {initial}";
        return $"{displayLabel} - {initial} ({current})";
    }

    public Task SetStick(SwitchStick stick, short x, short y, int delayMin, int delayMax, CancellationToken token) =>
        SetStick(stick, x, y, Util.Rand.Next(delayMin, delayMax), token);

    public override void SoftStop() => Config.Pause();

    protected void InitSaveData(SaveFile sav)
    {
        GameLang = (LanguageID)sav.Language;
        Version = sav.Version;
        InGameName = sav.OT;
        TrainerLabel = $"{InGameName}-{sav.DisplayTID:000000}";

        // 将初始标识（IP/USB）的缓存日志刷新到训练家目录
        var earlyIdentifier = Connection.Label;

        // 更新 Connection.Label 为训练家标识，用于日志分类
        // 这样可生成类似 logs/HeXbyt3-483256/ 的目录，而不是 logs/192.168.0.106/
        Connection.Label = TrainerLabel;

        // 将所有缓存日志写入训练家目录
        LogUtil.FlushBufferedLogs(earlyIdentifier, TrainerLabel);

        Log($"{Connection.Name} 已识别为 {TrainerLabel}，使用语言 {GameLang}。");
    }

    protected bool IsValidTrainerData() => GameLang is > 0 and <= LanguageID.SpanishL && InGameName.Length > 0 && Version > 0;
}
