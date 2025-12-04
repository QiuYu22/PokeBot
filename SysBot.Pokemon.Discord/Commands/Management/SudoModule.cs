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
    [Summary("封禁指定的在线用户 ID。")]
    [RequireSudo]
    public async Task BanOnlineIDs([Summary("逗号分隔的在线 ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var objects = IDs.Select(GetReference);

        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        hub.Config.TradeAbuse.BannedIDs.AddIfNew(objects);
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("bannedIDComment")]
    [Summary("为已封禁的在线 ID 添加备注。")]
    [RequireSudo]
    public async Task BanOnlineIDs(ulong id, [Remainder] string comment)
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        var obj = hub.Config.TradeAbuse.BannedIDs.List.Find(z => z.ID == id);
        if (obj is null)
        {
            await ReplyAsync($"找不到该在线 ID ({id}) 的记录。").ConfigureAwait(false);
            return;
        }

        var oldComment = obj.Comment;
        obj.Comment = comment;
        await ReplyAsync($"已完成。原备注 ({oldComment}) 已更新为 ({comment})。").ConfigureAwait(false);
    }

    [Command("blacklistId")]
    [Summary("根据 Discord 用户 ID 拉黑（适用于不在服务器的用户）。")]
    [RequireSudo]
    public async Task BlackListIDs([Summary("逗号分隔的 Discord ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var objects = IDs.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("blacklist")]
    [Summary("拉黑被提及的 Discord 用户。")]
    [RequireSudo]
    public async Task BlackListUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("blacklistComment")]
    [Summary("为已拉黑的 Discord 用户添加备注。")]
    [RequireSudo]
    public async Task BlackListUsers(ulong id, [Remainder] string comment)
    {
        var obj = SysCordSettings.Settings.UserBlacklist.List.Find(z => z.ID == id);
        if (obj is null)
        {
            await ReplyAsync($"找不到该用户 ID ({id})。").ConfigureAwait(false);
            return;
        }

        var oldComment = obj.Comment;
        obj.Comment = comment;
        await ReplyAsync($"已完成。原备注 ({oldComment}) 已更新为 ({comment})。").ConfigureAwait(false);
    }

    [Command("forgetUser")]
    [Alias("forget")]
    [Summary("清除曾遇到过的用户记录。")]
    [RequireSudo]
    public async Task ForgetPreviousUser([Summary("逗号分隔的在线 ID")][Remainder] string content)
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
    [Summary("输出所有已封禁的在线 ID 列表。")]
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
    [Summary("输出所有已拉黑的 Discord 用户列表。")]
    [RequireSudo]
    public async Task PrintBlacklist()
    {
        var lines = SysCordSettings.Settings.UserBlacklist.Summarize();
        var msg = string.Join("\n", lines);
        await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
    }

    [Command("previousUserSummary")]
    [Alias("prevUsers")]
    [Summary("输出曾经遇到过的用户列表。")]
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
            var msg = "历史派发用户：\n" + string.Join("\n", lines);
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }
        if (!found)
            await ReplyAsync("未找到历史用户。").ConfigureAwait(false);
    }

    [Command("unbanID")]
    [Summary("解封指定的在线用户 ID。")]
    [RequireSudo]
    public async Task UnBanOnlineIDs([Summary("逗号分隔的在线 ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        hub.Config.TradeAbuse.BannedIDs.RemoveAll(z => IDs.Any(o => o == z.ID));
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("unBlacklistId")]
    [Summary("解除 Discord 用户 ID 的拉黑（适用于不在服务器的用户）。")]
    [RequireSudo]
    public async Task UnBlackListIDs([Summary("逗号分隔的 Discord ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        SysCordSettings.Settings.UserBlacklist.RemoveAll(z => IDs.Any(o => o == z.ID));
        await ReplyAsync("已完成。").ConfigureAwait(false);
    }

    [Command("unblacklist")]
    [Summary("解除被提及 Discord 用户的拉黑。")]
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
    [Summary("封禁指定用户的交易资格，并附加原因。")]
    [RequireSudo]
    public async Task BanTradeUser(ulong userNID, string? userName = null, [Remainder] string? banReason = null)
    {
        await Context.Message.DeleteAsync();
        var dmChannel = await Context.User.CreateDMChannelAsync();
        try
        {
            // 检查是否提供了封禁原因
            if (string.IsNullOrWhiteSpace(banReason))
            {
                await dmChannel.SendMessageAsync("未提供封禁原因。正确示例：\n.banTrade {NID} {可选：名称} {原因}\n例如：.banTrade 123456789 频繁刷交易");
                return;
            }

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
                Comment = $"{Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd.hh\\:mm\\:ss} 封禁。原因：{banReason}"
            };

            hub.Config.TradeAbuse.BannedIDs.AddIfNew([bannedUser]);
            await dmChannel.SendMessageAsync($"已完成。用户 {userName}（NID {userNID}）已被禁止交易。");
        }
        catch (Exception ex)
        {
            await dmChannel.SendMessageAsync($"操作失败：{ex.Message}");
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
        Comment = $"{Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd.hh\\:mm\\:ss} 添加",
    };

    private RemoteControlAccess GetReference(ulong id) => new()
    {
        ID = id,
        Name = "手动添加",
        Comment = $"{Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd.hh\\:mm\\:ss} 添加",
    };
}
