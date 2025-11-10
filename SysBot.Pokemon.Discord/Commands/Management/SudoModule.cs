using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class SudoModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("banID")]
    [Summary("封禁指定的线上用户 ID。")]
    [RequireSudo]
    public async Task BanOnlineIDs([Summary("逗号分隔的线上 ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var objects = IDs.Select(GetReference);

        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        hub.Config.TradeAbuse.BannedIDs.AddIfNew(objects);
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("bannedIDComment")]
    [Summary("为已封禁的线上用户 ID 添加备注。")]
    [RequireSudo]
    public async Task BanOnlineIDs(ulong id, [Remainder] string comment)
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        var obj = hub.Config.TradeAbuse.BannedIDs.List.Find(z => z.ID == id);
        if (obj is null)
        {
            await ReplyAsync($"未找到该线上 ID 对应的用户（{id}）。").ConfigureAwait(false);
            return;
        }

        var oldComment = obj.Comment;
        obj.Comment = comment;
        await ReplyAsync($"已完成。备注已由（{oldComment}）修改为（{comment}）。").ConfigureAwait(false);
    }

    [Command("blacklistId")]
    [Summary("封禁指定的 Discord 用户 ID（适用于不在服务器的用户）。")]
    [RequireSudo]
    public async Task BlackListIDs([Summary("逗号分隔的 Discord ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var objects = IDs.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("blacklist")]
    [Summary("封禁被提及的 Discord 用户。")]
    [RequireSudo]
    public async Task BlackListUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("blacklistComment")]
    [Summary("为已封禁的 Discord 用户 ID 添加备注。")]
    [RequireSudo]
    public async Task BlackListUsers(ulong id, [Remainder] string comment)
    {
        var obj = SysCordSettings.Settings.UserBlacklist.List.Find(z => z.ID == id);
        if (obj is null)
        {
            await ReplyAsync($"未找到该 ID 对应的用户（{id}）。").ConfigureAwait(false);
            return;
        }

        var oldComment = obj.Comment;
        obj.Comment = comment;
        await ReplyAsync($"已完成。备注已由（{oldComment}）修改为（{comment}）。").ConfigureAwait(false);
    }

    [Command("forgetUser")]
    [Alias("forget")]
    [Summary("忘记与指定线上 ID 的历史交互记录。")]
    [RequireSudo]
    public async Task ForgetPreviousUser([Summary("逗号分隔的线上 ID")][Remainder] string content)
    {
        foreach (var ID in GetIDs(content))
        {
            PokeRoutineExecutorBase.PreviousUsers.RemoveAllNID(ID);
            PokeRoutineExecutorBase.PreviousUsersDistribution.RemoveAllNID(ID);
        }
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("bannedIDSummary")]
    [Alias("printBannedID", "bannedIDPrint")]
    [Summary("输出已封禁线上 ID 的列表。")]
    [RequireSudo]
    public async Task PrintBannedOnlineIDs()
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        var lines = hub.Config.TradeAbuse.BannedIDs.Summarize();
        var msg = string.Join("\n", lines);
        await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
    }

    [Command("blacklistSummary")]
    [Alias("printBlacklist", "blacklistPrint")]
    [Summary("输出已封禁 Discord 用户列表。")]
    [RequireSudo]
    public async Task PrintBlacklist()
    {
        var lines = SysCordSettings.Settings.UserBlacklist.Summarize();
        var msg = string.Join("\n", lines);
        await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
    }

    [Command("previousUserSummary")]
    [Alias("prevUsers")]
    [Summary("输出曾与机器人交互过的用户列表。")]
    [RequireSudo]
    public async Task PrintPreviousUsers()
    {
        bool found = false;
        var lines = PokeRoutineExecutorBase.PreviousUsers.Summarize().ToList();
        if (lines.Count != 0)
        {
            found = true;
            var msg = "历史用户：\n" + string.Join("\n", lines);
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }

        lines = [.. PokeRoutineExecutorBase.PreviousUsersDistribution.Summarize()];
        if (lines.Count != 0)
        {
            found = true;
            var msg = "历史配布用户：\n" + string.Join("\n", lines);
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }
        if (!found)
            await ReplyAsync("未找到历史用户记录。").ConfigureAwait(false);
    }

    [Command("unbanID")]
    [Summary("解除线上用户 ID 的封禁。")]
    [RequireSudo]
    public async Task UnBanOnlineIDs([Summary("逗号分隔的线上 ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        hub.Config.TradeAbuse.BannedIDs.RemoveAll(z => IDs.Any(o => o == z.ID));
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("unBlacklistId")]
    [Summary("从黑名单中移除指定的 Discord 用户 ID（适用于不在服务器的用户）。")]
    [RequireSudo]
    public async Task UnBlackListIDs([Summary("逗号分隔的 Discord ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        SysCordSettings.Settings.UserBlacklist.RemoveAll(z => IDs.Any(o => o == z.ID));
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("unblacklist")]
    [Summary("将被提及的 Discord 用户移出黑名单。")]
    [RequireSudo]
    public async Task UnBlackListUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.RemoveAll(z => objects.Any(o => o.ID == z.ID));
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("banTrade")]
    [Alias("bant")]
    [Summary("封禁指定用户的交易权限，并记录原因。")]
    [RequireSudo]
    public async Task BanTradeUser(ulong userNID, string? userName = null, [Remainder] string? banReason = null)
    {
        await Context.Message.DeleteAsync();
        var dmChannel = await Context.User.CreateDMChannelAsync();
        try
        {
            // Check if the ban reason is provided
            if (string.IsNullOrWhiteSpace(banReason))
            {
                await dmChannel.SendMessageAsync("未提供封禁原因。请按以下格式使用命令：\n.banTrade {NID} {可选：昵称} {原因}\n示例： .banTrade 123456789 违规交易");
                return;
            }

            // Use a default name if none is provided
            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = "未知";
            }

            var me = SysCord<T>.Runner;
            var hub = me.Hub;
            var bannedUser = new RemoteControlAccess
            {
                ID = userNID,
                Name = userName,
                Comment = $"由 {Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd-hh:mm:ss} 封禁。原因：{banReason}"
            };

            hub.Config.TradeAbuse.BannedIDs.AddIfNew([bannedUser]);
            await dmChannel.SendMessageAsync($"已完成。用户 {userName}（NID {userNID}）已被禁止交易。");
        }
        catch (Exception ex)
        {
            await dmChannel.SendMessageAsync($"发生错误：{ex.Message}");
        }
    }

    protected static IEnumerable<ulong> GetIDs(string content)
    {
        return content.Split([",", ", ", " "], StringSplitOptions.RemoveEmptyEntries)
            .Select(z => ulong.TryParse(z, out var x) ? x : 0).Where(z => z != 0);
    }

    private RemoteControlAccess GetReference(IUser channel) => new()
    {
        ID = channel.Id,
        Name = channel.Username,
        Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
    };

    private RemoteControlAccess GetReference(ulong id) => new()
    {
        ID = id,
        Name = "Manual",
        Comment = $"由 {Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd-hh:mm:ss} 添加",
    };
}
