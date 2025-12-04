using Discord;
using Discord.Commands;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

[Summary("清理并切换队列功能。")]
public class QueueModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    [Command("queueMode")]
    [Alias("qm")]
    [Summary("修改队列控制方式（手动/阈值/间隔）。")]
    [RequireSudo]
    public async Task ChangeQueueModeAsync([Summary("队列模式")] QueueOpening mode)
    {
        SysCord<T>.Runner.Hub.Config.Queues.QueueToggleMode = mode;
        await ReplyAsync($"已将队列模式切换为 {mode}。").ConfigureAwait(false);
    }

    [Command("queueClearAll")]
    [Alias("qca", "tca")]
    [Summary("清空交易队列中的所有用户。")]
    [RequireSudo]
    public async Task ClearAllTradesAsync()
    {
        Info.ClearAllQueues();
        await ReplyAsync("已清空队列。").ConfigureAwait(false);
    }

    [Command("queueClear")]
    [Alias("qc", "tc")]
    [Summary("将用户从交易队列中移除（正在处理的用户不会被移除）。")]
    public async Task ClearTradeAsync()
    {
        string msg = ClearTrade(Context.User.Id);
        await ReplyAndDeleteAsync(msg, 5, Context.Message).ConfigureAwait(false);
    }

    [Command("queueClearUser")]
    [Alias("qcu", "tcu")]
    [Summary("将用户从交易队列中移除（正在处理的用户不会被移除）。")]
    [RequireSudo]
    public async Task ClearTradeUserAsync([Summary("Discord 用户 ID")] ulong id)
    {
        string msg = ClearTrade(id);
        await ReplyAsync(msg).ConfigureAwait(false);
    }

    [Command("queueClearUser")]
    [Alias("qcu", "tcu")]
    [Summary("将用户从交易队列中移除（正在处理的用户不会被移除）。")]
    [RequireSudo]
    public async Task ClearTradeUserAsync([Summary("需移除的用户名")] string _)
    {
        foreach (var user in Context.Message.MentionedUsers)
        {
            string msg = ClearTrade(user.Id);
            await ReplyAsync(msg).ConfigureAwait(false);
        }
    }

    [Command("queueClearUser")]
    [Alias("qcu", "tcu")]
    [Summary("将用户从交易队列中移除（正在处理的用户不会被移除）。")]
    [RequireSudo]
    public async Task ClearTradeUserAsync()
    {
        var users = Context.Message.MentionedUsers;
        if (users.Count == 0)
        {
            await ReplyAsync("未提及任何用户。").ConfigureAwait(false);
            return;
        }
        foreach (var u in users)
            await ClearTradeUserAsync(u.Id).ConfigureAwait(false);
    }

    [Command("deleteTradeCode")]
    [Alias("dtc")]
    [Summary("删除用户存储的交易代码。")]
    public async Task DeleteTradeCodeAsync()
    {
        var userID = Context.User.Id;
        string msg = QueueModule<T>.DeleteTradeCode(userID);
        await ReplyAsync(msg).ConfigureAwait(false);
    }

    [Command("queueStatus")]
    [Alias("qs", "ts")]
    [Summary("查看用户在队列中的位置。")]
    public async Task GetTradePositionAsync()
    {
        var userID = Context.User.Id;
        var tradeEntry = Info.GetDetail(userID);

        string msg;
        if (tradeEntry != null)
        {
            var uniqueTradeID = tradeEntry.UniqueTradeID;
            msg = Context.User.Mention + " - " + Info.GetPositionString(userID, uniqueTradeID, tradeEntry.Type);
        }
        else
        {
            msg = Context.User.Mention + " - 你当前不在队列中。";
        }

        await ReplyAndDeleteAsync(msg, 5, Context.Message).ConfigureAwait(false);
    }

    [Command("queueList")]
    [Alias("ql")]
    [Summary("以嵌入形式显示当前队列（包含物种、交易类型和用户名）。")]
    [RequireSudo]
    public async Task ListUserQueue()
    {
        var queue = SysCord<T>.Runner.Hub.Queues.Info.GetUserList("{4}|{2}|{3}"); // Species|Type|Username

        if (!queue.Any())
        {
            await ReplyAsync("当前队列为空。").ConfigureAwait(false);
            return;
        }

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"📋 当前交易队列（{queue.Count()} 名用户）")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

        var queueList = queue.Select((entry, index) =>
        {
            var parts = entry.Split('|');
            var species = parts[0];
            var tradeType = parts[1];
            var username = parts[2];

            return $"`{index + 1}.` **{species}** - {tradeType} - *{username}*";
        });

        var description = string.Join("\n", queueList);

        // Discord embeds have a 4096 character limit for description
            if (description.Length > 4000)
            {
                description = description.Substring(0, 4000) + "\n…（列表已截断）";
            }

        embedBuilder.WithDescription(description);

        await ReplyAsync(embed: embedBuilder.Build()).ConfigureAwait(false);
    }

    [Command("queueToggle")]
    [Alias("qt", "tt")]
    [Summary("开启/关闭加入交易队列的能力。")]
    [RequireSudo]
    public Task ToggleQueueTradeAsync()
    {
        var state = Info.ToggleQueue();
        var msg = state
            ? "用户现在可以加入交易队列。"
            : "队列设置已更新：**用户无法加入队列，直到重新开启。**";

        return Context.Channel.EchoAndReply(msg);
    }

    private static string ClearTrade(ulong userID)
    {
        var result = Info.ClearTrade(userID);
        return GetClearTradeMessage(result);
    }

    private static string DeleteTradeCode(ulong userID)
    {
        var tradeCodeStorage = new TradeCodeStorage();
        bool success = tradeCodeStorage.DeleteTradeCode(userID);

        if (success)
            return "已成功删除你存储的交易代码。";
        else
            return "未找到与你的用户 ID 对应的交易代码。";
    }

    private static string GetClearTradeMessage(QueueResultRemove result)
    {
        return result switch
        {
            QueueResultRemove.Removed => "已将你待处理的交易移出队列。",
            QueueResultRemove.CurrentlyProcessing => "你有交易正在处理，未从队列中移除这些条目。",
            QueueResultRemove.CurrentlyProcessingRemoved => "你有交易正在处理，已移除其他待处理的交易。",
            QueueResultRemove.NotInQueue => "你当前不在队列中。",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
        };
    }

    private async Task DeleteMessagesAfterDelayAsync(IMessage sentMessage, IMessage? messageToDelete, int delaySeconds)
    {
        try
        {
            // Don't attempt to delete messages in DM channels - Discord doesn't allow it
            if (sentMessage.Channel is IDMChannel)
                return;

            await Task.Delay(delaySeconds * 1000);
            await sentMessage.DeleteAsync();
            if (messageToDelete != null)
                await messageToDelete.DeleteAsync();
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(QueueModule<T>));
        }
    }

    private async Task ReplyAndDeleteAsync(string message, int delaySeconds, IMessage? messageToDelete = null)
    {
        try
        {
            var sentMessage = await ReplyAsync(message).ConfigureAwait(false);
            _ = DeleteMessagesAfterDelayAsync(sentMessage, messageToDelete, delaySeconds);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(QueueModule<T>));
        }
    }

    [Command("changeTradeCode")]
    [Alias("ctc")]
    [Summary("当启用交易代码存储时，修改用户的交易代码。")]
    public async Task ChangeTradeCodeAsync([Summary("新的 8 位交易代码")] string newCode)
    {
        // Delete user's message immediately to protect the trade code
        await Context.Message.DeleteAsync().ConfigureAwait(false);

        var userID = Context.User.Id;
        var tradeCodeStorage = new TradeCodeStorage();

        if (!ValidateTradeCode(newCode, out string errorMessage))
        {
            await SendTemporaryMessageAsync(errorMessage).ConfigureAwait(false);
            return;
        }

        try
        {
            int code = int.Parse(newCode);
            if (tradeCodeStorage.UpdateTradeCode(userID, code))
            {
                await SendTemporaryMessageAsync("交易代码已更新。").ConfigureAwait(false);
            }
            else
            {
                await SendTemporaryMessageAsync("你尚未设置交易代码，请先使用 trade 指令生成。").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError($"修改用户 {userID} 的交易代码时出错：{ex.Message}", nameof(QueueModule<T>));
            await SendTemporaryMessageAsync("修改交易代码时出现错误，请稍后再试。").ConfigureAwait(false);
        }
    }

    private async Task SendTemporaryMessageAsync(string message)
    {
        var sentMessage = await ReplyAsync(message).ConfigureAwait(false);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            await sentMessage.DeleteAsync().ConfigureAwait(false);
        });
    }

    private static bool ValidateTradeCode(string code, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (code.Length != 8)
        {
            errorMessage = "交易代码必须是 8 位数字。";
            return false;
        }

        if (!Regex.IsMatch(code, @"^\d{8}$"))
        {
            errorMessage = "交易代码只能包含数字。";
            return false;
        }

        if (QueueModule<T>.IsEasilyGuessableCode(code))
        {
            errorMessage = "交易代码过于容易被猜中，请选择更复杂的组合。";
            return false;
        }

        return true;
    }

    private static bool IsEasilyGuessableCode(string code)
    {
        string[] easyPatterns = [
                @"^(\d)\1{7}$",           // All same digits (e.g., 11111111)
                @"^12345678$",            // Ascending sequence
                @"^87654321$",            // Descending sequence
                @"^(?:01234567|12345678|23456789)$" // Other common sequences
            ];

        foreach (var pattern in easyPatterns)
        {
            if (Regex.IsMatch(code, pattern))
            {
                return true;
            }
        }

        return false;
    }
}
