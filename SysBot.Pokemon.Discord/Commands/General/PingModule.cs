using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class PingModule : ModuleBase<SocketCommandContext>
{
    [Command("ping")]
    [Summary("让机器人响应，表明它正在运行。")]
    public async Task PingAsync()
    {
        var embed = new EmbedBuilder()
            .WithTitle("Ping 响应")
            .WithDescription("Pong！机器人运行正常。")
            .WithImageUrl("https://i.gifer.com/QgxJ.gif")
            .WithColor(Color.Green)
            .Build();

        await ReplyAsync(embed: embed).ConfigureAwait(false);
    }
}
