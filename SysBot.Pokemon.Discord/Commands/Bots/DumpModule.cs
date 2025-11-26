using Discord;
using Discord.Net;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("排队新的导出交易")]
public class DumpModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("dump")]
    [Alias("d")]
    [Summary("导出您通过连接交易展示的宝可梦。")]
    [RequireQueueRole(nameof(DiscordManager.RolesDump))]
    public async Task DumpAsync(int code)
    {
        if (await CheckUserInQueueAsync())
            return;

        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();
        await QueueHelper<T>.AddToQueueAsync(
            Context,
            code,
            Context.User.Username,
            sig,
            new T(),
            PokeRoutineType.Dump,
            PokeTradeType.Dump,
            Context.User,
            isBatchTrade: false,
            batchTradeNumber: 1,
            totalBatchTrades: 1,
            isMysteryEgg: false,
            lgcode: lgcode);

        _ = DeleteMessageAsync(Context.Message, 2000);
    }

    [Command("dump")]
    [Alias("d")]
    [Summary("导出您通过连接交易展示的宝可梦。")]
    [RequireQueueRole(nameof(DiscordManager.RolesDump))]
    public async Task DumpAsync([Summary("交易密码")][Remainder] string code)
    {
        if (await CheckUserInQueueAsync())
            return;

        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode(Context.User.Id) : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.Dump, PokeTradeType.Dump);
    }

    [Command("dump")]
    [Alias("d")]
    [Summary("导出您通过连接交易展示的宝可梦。")]
    [RequireQueueRole(nameof(DiscordManager.RolesDump))]
    public async Task DumpAsync()
    {
        if (await CheckUserInQueueAsync())
            return;

        var code = Info.GetRandomTradeCode(Context.User.Id);
        await DumpAsync(code);
    }

    [Command("dumpList")]
    [Alias("dl", "dq")]
    [Summary("打印导出队列中的用户。")]
    [RequireSudo]
    public async Task GetListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.Dump);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "待处理交易";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("以下是当前正在等待的用户:", embed: embed.Build()).ConfigureAwait(false);
    }

    private async Task<bool> CheckUserInQueueAsync()
    {
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            await ReplyAsync("您在队列中已有一个交易。请等待处理完成。").ConfigureAwait(false);
            return true;
        }
        return false;
    }

    private static async Task DeleteMessageAsync(IMessage message, int delay)
    {
        await Task.Delay(delay);
        try
        {
            await message.DeleteAsync();
        }
        catch (HttpException)
        {
            // Ignore exceptions if the message was already deleted or we don't have permission
        }
    }
}
