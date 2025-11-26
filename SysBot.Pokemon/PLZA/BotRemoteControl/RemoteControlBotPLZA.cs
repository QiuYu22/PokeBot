using SysBot.Base;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

public class RemoteControlBotPLZA(PokeBotState Config) : PokeRoutineExecutor9PLZA(Config)
{
    public override async Task HardStop()
    {
        await SetStick(SwitchStick.LEFT, 0, 0, 0_500, CancellationToken.None).ConfigureAwait(false);
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
    }

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            Log("正在识别主机的训练家数据。");
            await IdentifyTrainer(token).ConfigureAwait(false);

            Log("开始主循环，等待命令。");
            Config.IterateNextRoutine();
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                ReportStatus();
            }
        }
        catch (Exception e)
        {
            Log(e.Message);
        }

        Log($"结束 {nameof(RemoteControlBotPLZA)} 循环。");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        await ReOpenGame(new PokeTradeHubConfig(), t).ConfigureAwait(false);
        await HardStop().ConfigureAwait(false);

        await Task.Delay(2_000, t).ConfigureAwait(false);
        if (!t.IsCancellationRequested)
        {
            Log("正在重启主循环。");
            await MainLoop(t).ConfigureAwait(false);
        }
    }

    private class DummyReset : IBotStateSettings
    {
        public bool ScreenOff => true;
    }
}
