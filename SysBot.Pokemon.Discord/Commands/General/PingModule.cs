using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class PingModule : ModuleBase<SocketCommandContext>
{
    [Command("ping")]
    [Summary("让机器人回应，确认其正在运行。")]
    public async Task PingAsync()
    {
        var embed = new EmbedBuilder()
            .WithTitle("Ping 响应")
            .WithDescription("Pong！机器人运行一切正常。")
            .WithImageUrl("https://i.gifer.com/QgxJ.gif")
            .WithColor(Color.Green)
            .Build();

        await ReplyAsync(embed: embed).ConfigureAwait(false);
    }
}
