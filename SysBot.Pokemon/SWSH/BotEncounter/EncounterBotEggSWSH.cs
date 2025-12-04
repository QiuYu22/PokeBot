using PKHeX.Core;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon;

public class EncounterBotEggSWSH : EncounterBotSWSH
{
    private static readonly PK8 Blank = new();

    private readonly IDumper DumpSetting;

    public EncounterBotEggSWSH(PokeBotState Config, PokeTradeHub<PK8> hub) : base(Config, hub)
    {
        DumpSetting = Hub.Config.Folder;
    }

    public async Task<bool> IsEggReady(CancellationToken token)
    {
        // Read a single byte of the Daycare metadata to check the IsEggReady flag.
        var data = await Connection.ReadBytesAsync(DayCare_Route5_Egg_Is_Ready, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        await ReOpenGame(new PokeTradeHubConfig(), t).ConfigureAwait(false);
        await HardStop().ConfigureAwait(false);
    }

    public Task SetEggStepCounter(CancellationToken token)
    {
        // 将培育屋元数据中的步数计数设为 180，这是触发“是否生成蛋”子程序的阈值。
        // 游戏执行该子程序时会生成新的种子并置位 IsEggReady。
        // 仅设置 IsEggReady 无法刷新种子，因此需要通过步数触发。
        var data = new byte[] { 0xB4, 0, 0, 0 }; // 180
        return Connection.WriteBytesAsync(data, DayCare_Route5_Step_Counter, token);
    }

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        await SetupBoxState(DumpSetting, token).ConfigureAwait(false);

        while (!token.IsCancellationRequested)
        {
            // Walk a step left, then right => check if egg was generated on this attempt.
            // Repeat until an egg is generated.
            var attempts = await StepUntilEgg(token).ConfigureAwait(false);
            if (attempts < 0) // aborted
                return;

            Log($"第 {attempts} 次尝试后获得了蛋，正在清空目标槽位。");
            await SetBoxPokemon(Blank, 0, 0, token).ConfigureAwait(false);

            for (int i = 0; i < 10; i++)
                await Click(A, 0_200, token).ConfigureAwait(false);

            // Safe to mash B from here until we get out of all menus.
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(B, 0_200, token).ConfigureAwait(false);

            Log("已领取蛋，正在检查详情。");
            var pk = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
            if (pk.Species == 0)
            {
                Log("盒 1 槽 1 中未找到蛋，请确认队伍已满，重新开始循环。");
                continue;
            }

            if (await HandleEncounter(pk, token).ConfigureAwait(false))
                return;
        }
    }

    private async Task<int> StepUntilEgg(CancellationToken token)
    {
        Log("正在周围走动以等待蛋生成…");
        int attempts = 0;
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.EggFetch)
        {
            await SetEggStepCounter(token).ConfigureAwait(false);

            // Walk diagonally left.
            await SetStick(LEFT, -19000, 19000, 0_500, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

            // Walk diagonally right, slightly longer to ensure we stay at the Daycare lady.
            await SetStick(LEFT, 19000, 19000, 0_550, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

            bool eggReady = await IsEggReady(token).ConfigureAwait(false);
            if (eggReady)
                return attempts;

            attempts++;
            if (attempts % 10 == 0)
                Log($"已尝试 {attempts} 次，仍未生成蛋。");

            if (attempts > 10)
                await Click(B, 500, token).ConfigureAwait(false);
        }

        return -1; // aborted
    }
}
