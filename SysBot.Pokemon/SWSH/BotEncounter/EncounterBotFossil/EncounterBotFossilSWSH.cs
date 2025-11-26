using PKHeX.Core;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon;

public class EncounterBotFossilSWSH : EncounterBotSWSH
{
    private static readonly PK8 Blank = new();

    private readonly IDumper DumpSetting;

    private readonly FossilSettings Settings;

    public EncounterBotFossilSWSH(PokeBotState Config, PokeTradeHub<PK8> hub) : base(Config, hub)
    {
        Settings = Hub.Config.EncounterSWSH.Fossil;
        DumpSetting = Hub.Config.Folder;
    }

    public override async Task RebootAndStop(CancellationToken t)
    {
        await ReOpenGame(new PokeTradeHubConfig(), t).ConfigureAwait(false);
        await HardStop().ConfigureAwait(false);
    }

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        await SetupBoxState(DumpSetting, token).ConfigureAwait(false);

        Log("正在检查物品数量...");
        var pouchData = await Connection.ReadBytesAsync(ItemTreasureAddress, 80, token).ConfigureAwait(false);
        var counts = FossilCount.GetFossilCounts(pouchData);
        int reviveCount = counts.PossibleRevives(Settings.Species);
        if (reviveCount == 0)
        {
            Log("化石碎片不足。请先至少获取每种所需化石碎片各一个。");
            return;
        }
        Log($"化石碎片足够复原 {reviveCount} 只 {Settings.Species}。");

        while (!token.IsCancellationRequested)
        {
            if (encounterCount != 0 && encounterCount % reviveCount == 0)
            {
                Log($"复原 {Settings.Species} 的化石已耗尽。");
                if (Settings.InjectWhenEmpty)
                {
                    Log("正在恢复原始背包数据。");
                    await Connection.WriteBytesAsync(pouchData, ItemTreasureAddress, token).ConfigureAwait(false);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
                else
                {
                    Log("化石碎片已耗尽。正在重置游戏。");
                    await CloseGame(Hub.Config, token).ConfigureAwait(false);
                    await StartGame(Hub.Config, token).ConfigureAwait(false);
                    await SetupBoxState(DumpSetting, token).ConfigureAwait(false);
                }
            }

            await ReviveFossil(counts, token).ConfigureAwait(false);
            Log("化石已复活。正在检查详情...");

            var pk = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
            if (pk.Species == 0 || !pk.ChecksumValid)
            {
                Log("在盒子1槽位1未找到化石宝可梦。请确保队伍已满。正在重启循环。");
                continue;
            }

            if (await HandleEncounter(pk, token).ConfigureAwait(false))
                return;

            Log("正在清理目标槽位。");
            await SetBoxPokemon(Blank, 0, 0, token).ConfigureAwait(false);
        }
    }

    private async Task ReviveFossil(FossilCount count, CancellationToken token)
    {
        Log("开始化石复活流程...");
        if (GameLang == LanguageID.Spanish)
            await Click(A, 0_900, token).ConfigureAwait(false);

        await Click(A, 1_100, token).ConfigureAwait(false);

        // French is slightly slower.
        if (GameLang == LanguageID.French)
            await Task.Delay(0_200, token).ConfigureAwait(false);

        await Click(A, 1_300, token).ConfigureAwait(false);

        // Selecting first fossil.
        if (count.UseSecondOption1(Settings.Species))
            await Click(DDOWN, 0_300, token).ConfigureAwait(false);
        await Click(A, 1_300, token).ConfigureAwait(false);

        // Selecting second fossil.
        if (count.UseSecondOption2(Settings.Species))
            await Click(DDOWN, 300, token).ConfigureAwait(false);

        // A spam through accepting the fossil and agreeing to revive.
        for (int i = 0; i < 16; i++)
            await Click(A, 0_200, token).ConfigureAwait(false);

        // Safe to mash B from here until we get out of all menus.
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            await Click(B, 0_200, token).ConfigureAwait(false);
    }
}
