using Discord.Commands;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class EchoModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("toss")]
    [Summary("让所有等待确认的机器人继续执行任务。")]
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

        await ReplyAsync("已完成。").ConfigureAwait(false);
    }
}
