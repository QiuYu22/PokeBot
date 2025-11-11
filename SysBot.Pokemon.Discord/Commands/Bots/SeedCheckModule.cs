using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("加入新的种子检测交易队列")]
public class SeedCheckModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("findFrame")]
    [Alias("ff", "getFrameData")]
    [Summary("输出提供种子的下一帧闪光信息。")]
    public async Task FindFrameAsync([Remainder] string seedString)
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;

        seedString = seedString.ToLower();
        if (seedString.StartsWith("0x"))
            seedString = seedString[2..];

        var seed = Util.GetHexValue64(seedString);

        var r = new SeedSearchResult(Z3SearchResult.Success, seed, -1, hub.Config.SeedCheckSWSH.ResultDisplayMode);
        var msg = r.ToString();

        var embed = new EmbedBuilder { Color = Color.LighterGrey };

        embed.AddField(x =>
        {
            x.Name = $"Seed: {seed:X16}";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync($"以下是 `{r.Seed:X16}` 的详细信息：", embed: embed.Build()).ConfigureAwait(false);
    }

    [Command("seedList")]
    [Alias("sl", "scq", "seedCheckQueue", "seedQueue", "seedList")]
    [Summary("列出种子检测队列中的用户。")]
    [RequireSudo]
    public async Task GetSeedListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.SeedCheck);
        var embed = new EmbedBuilder();
        embed.AddField(x =>
        {
            x.Name = "等待中的交易";
            x.Value = msg;
            x.IsInline = false;
        });
        await ReplyAsync("以下是当前正在等待的用户：", embed: embed.Build()).ConfigureAwait(false);
    }

    [Command("seedCheck")]
    [Alias("checkMySeed", "checkSeed", "seed", "s", "sc", "specialrequest", "sr")]
    [Summary("检测宝可梦的种子。")]
    [RequireQueueRole(nameof(DiscordManager.RolesSeed))]
    public async Task SeedCheckAsync(int code)
    {
        // Check if the user is already in the queue
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            await ReplyAsync("你在队列中已有一个待处理的交易，请耐心等待处理完成。").ConfigureAwait(false);
            return;
        }
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.SeedCheck, PokeTradeType.Seed).ConfigureAwait(false);
    }

    [Command("seedCheck")]
    [Alias("checkMySeed", "checkSeed", "seed", "s", "sc", "specialrequest", "sr")]
    [Summary("检测宝可梦的种子。")]
    [RequireQueueRole(nameof(DiscordManager.RolesSeed))]
    public async Task SeedCheckAsync([Summary("交换密码")][Remainder] string code)
    {
        // Check if the user is already in the queue
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            await ReplyAsync("你在队列中已有一个待处理的交易，请耐心等待处理完成。").ConfigureAwait(false);
            return;
        }
        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        await QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode(userID) : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.SeedCheck, PokeTradeType.Seed).ConfigureAwait(false);
    }

    [Command("seedCheck")]
    [Alias("checkMySeed", "checkSeed", "seed", "s", "sc", "specialrequest", "sr")]
    [Summary("检测宝可梦的种子。")]
    [RequireQueueRole(nameof(DiscordManager.RolesSeed))]
    public async Task SeedCheckAsync()
    {
        // Check if the user is already in the queue
        var userID = Context.User.Id;
        if (Info.IsUserInQueue(userID))
        {
            await ReplyAsync("你在队列中已有一个待处理的交易，请耐心等待处理完成。").ConfigureAwait(false);
            return;
        }
        var code = Info.GetRandomTradeCode(userID);
        await SeedCheckAsync(code).ConfigureAwait(false);
        if (Context.Message is IUserMessage userMessage)
            await userMessage.DeleteAsync().ConfigureAwait(false);
    }
}
