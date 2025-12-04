using Discord.Commands;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class EchoModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("toss")]
    [Summary("让所有等待继续的机器人同步恢复运行。")]
    [RequireSudo]
    public async Task TossAsync(string name = "")
    {
        foreach (var b in SysCord<T>.Runner.Bots.Select(z => z.Bot))
        {
            if (b is not IEncounterBot x)
                continue;
            if (!b.Connection.Name.Contains(name) && !b.Connection.Label.Contains(name))
                continue;
            x.Acknowledge();
        }

        await ReplyAsync("操作完成。").ConfigureAwait(false);
    }
}
