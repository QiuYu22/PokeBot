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
    [Summary("克隆你通过连线交换展示的宝可梦。")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public async Task CloneAsync(int code)
    {
        // PA9 (Legends Z-A) clone trades are currently disabled
        if (typeof(T) == typeof(PA9))
        {
            await ReplyAsync("由于存在问题，传奇Z-A的克隆交易目前已被禁用。请稍后再试。").ConfigureAwait(false);
            return;
        }

        // Check if the user is already in the queue
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            await ReplyAsync("你在队列中已有一个待处理的交易，请耐心等待处理完成。").ConfigureAwait(false);
            return;
        }

        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();

        // Add to queue asynchronously
        _ = QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone, Context.User, false, 1, 1, false, false, lgcode: lgcode);

        // Immediately send a confirmation message without waiting
        var confirmationMessage = await ReplyAsync("正在处理你的克隆请求……").ConfigureAwait(false);

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
    [Summary("克隆你通过连线交换展示的宝可梦。")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public async Task CloneAsync([Summary("交换密码")][Remainder] string code)
    {
        // PA9 (Legends Z-A) clone trades are currently disabled
        if (typeof(T) == typeof(PA9))
        {
            await ReplyAsync("由于存在问题，传奇Z-A的克隆交易目前已被禁用。请稍后再试。").ConfigureAwait(false);
            return;
        }

        // Check if the user is already in the queue
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            await ReplyAsync("你在队列中已有一个待处理的交易，请耐心等待处理完成。").ConfigureAwait(false);
            return;
        }

        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        var lgcode = Info.GetRandomLGTradeCode();

        // Add to queue asynchronously
        _ = QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode(userID) : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone, Context.User, false, 1, 1, false, false, lgcode: lgcode);

        // Immediately send a confirmation message without waiting
        var confirmationMessage = await ReplyAsync("正在处理你的克隆请求……").ConfigureAwait(false);

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
    [Summary("克隆你通过连线交换展示的宝可梦。")]
    [RequireQueueRole(nameof(DiscordManager.RolesClone))]
    public Task CloneAsync()
    {
        var userID = Context.User.Id;
        var code = Info.GetRandomTradeCode(userID);
        return CloneAsync(code);
    }

    [Command("cloneList")]
    [Alias("cl", "cq")]
    [Summary("列出克隆队列中的用户。")]
    [RequireSudo]
    public async Task GetListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.Clone);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "等待中的交易";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("以下是当前正在等待的用户：", embed: embed.Build()).ConfigureAwait(false);
    }
}
