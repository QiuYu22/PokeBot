using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("排队新的克隆交易")]
public class CloneModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("clone")]
    [Alias("c")]
    [Summary("克隆您通过连接交易展示的宝可梦。")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public async Task CloneAsync(int code)
    {
        // Check if the user is already in the queue
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            await ReplyAsync("您在队列中已有一个交易。请等待处理完成。").ConfigureAwait(false);
            return;
        }

        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();

        // Add to queue asynchronously
        _ = QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone, Context.User, false, 1, 1, false, false, lgcode: lgcode);

        // Immediately send a confirmation message without waiting
        var confirmationMessage = await ReplyAsync("正在处理您的克隆请求...").ConfigureAwait(false);

        // Use a fire-and-forget approach for the delay and deletion
        _ = Task.Delay(2000).ContinueWith(async _ =>
        {
            if (Context.Message is IUserMessage userMessage)
                await userMessage.DeleteAsync().ConfigureAwait(false);

            if (confirmationMessage != null)
                await confirmationMessage.DeleteAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Command("clone")]
    [Alias("c")]
    [Summary("克隆您通过连接交易展示的宝可梦。")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public async Task CloneAsync([Summary("交易密码")][Remainder] string code)
    {
        // Check if the user is already in the queue
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            await ReplyAsync("您在队列中已有一个交易。请等待处理完成。").ConfigureAwait(false);
            return;
        }

        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();

        // Add to queue asynchronously
        _ = QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode(userID) : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone, Context.User, false, 1, 1, false, false, lgcode: lgcode);

        // Immediately send a confirmation message without waiting
        var confirmationMessage = await ReplyAsync("正在处理您的克隆请求...").ConfigureAwait(false);

        // Use a fire-and-forget approach for the delay and deletion
        _ = Task.Delay(2000).ContinueWith(async _ =>
        {
            if (Context.Message is IUserMessage userMessage)
                await userMessage.DeleteAsync().ConfigureAwait(false);

            if (confirmationMessage != null)
                await confirmationMessage.DeleteAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Command("clone")]
    [Alias("c")]
    [Summary("克隆您通过连接交易展示的宝可梦。")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public Task CloneAsync()
    {
        var userID = Context.User.Id;
        var code = Info.GetRandomTradeCode(userID);
        return CloneAsync(code);
    }

    [Command("cloneList")]
    [Alias("cl", "cq")]
    [Summary("打印克隆队列中的用户。")]
    [RequireSudo]
    public async Task GetListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.Clone);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "待处理交易";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("以下是当前正在等待的用户:", embed: embed.Build()).ConfigureAwait(false);
    }
}
