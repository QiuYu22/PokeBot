using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Z3;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysBot.Pokemon.ConsoleApp;

public static class Program
{
    private const string ConfigPath = "config.json";

    private static void ExitNoConfig()
    {
        var bot = new PokeBotState { Connection = new SwitchConnectionConfig { IP = "192.168.0.1", Port = 6000 }, InitialRoutine = PokeRoutineType.FlexTrade };
        var cfg = new ProgramConfig { Bots = [bot] };
        var created = JsonSerializer.Serialize(cfg, ProgramConfigContext.Default.ProgramConfig);
        File.WriteAllText(ConfigPath, created);
        LogUtil.LogInfo("系统机器人", "由于在程序路径中未找到配置文件，已创建新的配置文件。请配置后重新启动程序。");
        LogUtil.LogInfo("系统机器人", "建议尽可能使用 GUI 项目来配置此配置文件，这将帮助您正确设置各项参数。");
        LogUtil.LogInfo("系统机器人", "按任意键退出。");
        Console.ReadKey();
    }

    private static void Main(string[] args)
    {
        LogUtil.LogInfo("系统机器人", "正在启动...");
        if (args.Length > 1)
            LogUtil.LogInfo("系统机器人", "此程序不支持命令行参数。");

        if (!File.Exists(ConfigPath))
        {
            ExitNoConfig();
            return;
        }

        try
        {
            var lines = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize(lines, ProgramConfigContext.Default.ProgramConfig) ?? new ProgramConfig();
            PokeTradeBotSWSH.SeedChecker = new Z3SeedSearchHandler<PK8>();
            BotContainer.RunBots(cfg);
        }
        catch (Exception)
        {
            LogUtil.LogInfo("系统机器人", "无法使用已保存的配置文件启动机器人。请从 WinForms 项目复制配置文件，或删除后重新配置。");
            Console.ReadKey();
        }
    }
}

[JsonSerializable(typeof(ProgramConfig))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed partial class ProgramConfigContext : JsonSerializerContext;

public static class BotContainer
{
    public static void RunBots(ProgramConfig prog)
    {
        IPokeBotRunner env = GetRunner(prog);
        foreach (var bot in prog.Bots)
        {
            bot.Initialize();
            if (!AddBot(env, bot, prog.Mode))
                LogUtil.LogInfo("系统机器人", $"添加机器人失败: {bot}");
        }

        LogUtil.Forwarders.Add(ConsoleForwarder.Instance);
        env.StartAll();
        LogUtil.LogInfo("系统机器人", $"所有机器人已启动（数量: {prog.Bots.Length}）。");
        LogUtil.LogInfo("系统机器人", "按任意键停止运行并退出。您可以随意最小化此窗口！");
        Console.ReadKey();
        env.StopAll();
    }

    private static bool AddBot(IPokeBotRunner env, PokeBotState cfg, ProgramMode mode)
    {
        if (!cfg.IsValid())
        {
            LogUtil.LogInfo("系统机器人", $"{cfg} 的配置无效。");
            return false;
        }

        PokeRoutineExecutorBase newBot;
        try
        {
            newBot = env.CreateBotFromConfig(cfg);
        }
        catch
        {
            LogUtil.LogInfo("系统机器人", $"当前模式 ({mode}) 不支持此类型的机器人 ({cfg.CurrentRoutineType})。");
            return false;
        }
        try
        {
            env.Add(newBot);
        }
        catch (ArgumentException ex)
        {
            LogUtil.LogInfo("系统机器人", ex.Message);
            return false;
        }

        LogUtil.LogInfo("系统机器人", $"已添加: {cfg}: {cfg.InitialRoutine}");
        return true;
    }

    private static IPokeBotRunner GetRunner(ProgramConfig prog) => prog.Mode switch
    {
        ProgramMode.SWSH => new PokeBotRunnerImpl<PK8>(new PokeTradeHub<PK8>(prog.Hub), new BotFactory8SWSH(), prog),
        ProgramMode.BDSP => new PokeBotRunnerImpl<PB8>(new PokeTradeHub<PB8>(prog.Hub), new BotFactory8BS(), prog),
        ProgramMode.LA => new PokeBotRunnerImpl<PA8>(new PokeTradeHub<PA8>(prog.Hub), new BotFactory8LA(), prog),
        ProgramMode.SV => new PokeBotRunnerImpl<PK9>(new PokeTradeHub<PK9>(prog.Hub), new BotFactory9SV(), prog),
        ProgramMode.LGPE => new PokeBotRunnerImpl<PB7>(new PokeTradeHub<PB7>(prog.Hub), new BotFactory7LGPE(), prog),
        ProgramMode.PLZA => new PokeBotRunnerImpl<PA9>(new PokeTradeHub<PA9>(prog.Hub), new BotFactory9PLZA(), prog),
        _ => throw new IndexOutOfRangeException("不支持的模式。"),
    };
}
