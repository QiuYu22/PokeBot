using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("派发池管理模块")]
public class PoolModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("pool")]
    [Summary("显示随机派发池中的宝可梦详情。")]
    public async Task DisplayPoolCountAsync()
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        var pool = hub.Ledy.Pool;
        var count = pool.Count;
        if (count is > 0 and < 20)
        {
            var lines = pool.Files.Select((z, i) => $"{i + 1:00}: {z.Key} = {(Species)z.Value.RequestInfo.Species}");
            var msg = string.Join("\n", lines);

            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = $"数量：{count}";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("派发池详情", embed: embed.Build()).ConfigureAwait(false);
        }
        else
        {
            await ReplyAsync($"派发池数量：{count}").ConfigureAwait(false);
        }
    }

    [Command("poolReload")]
    [Summary("从配置的文件夹重新加载派发池。")]
    [RequireSudo]
    public async Task ReloadPoolAsync()
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;

        var pool = hub.Ledy.Pool.Reload(hub.Config.Folder.DistributeFolder);
        if (!pool)
            await ReplyAsync("从文件夹重新加载失败。").ConfigureAwait(false);
        else
            await ReplyAsync($"已从文件夹重新加载，当前派发池数量：{hub.Ledy.Pool.Count}").ConfigureAwait(false);
    }
}
