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
    [Summary("封禁在线用户 ID。")]
    [RequireSudo]
    public async Task BanOnlineIDs([Summary("逗号分隔的在线 ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var objects = IDs.Select(GetReference);

        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        hub.Config.TradeAbuse.BannedIDs.AddIfNew(objects);
        await ReplyAsync("完成。").ConfigureAwait(false);
    }

    [Command("bannedIDComment")]
    [Summary("为已封禁的在线用户 ID 添加备注。")]
    [RequireSudo]
    public async Task BanOnlineIDs(ulong id, [Remainder] string comment)
    {
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        var obj = hub.Config.TradeAbuse.BannedIDs.List.Find(z => z.ID == id);
        if (obj is null)
        {
            await ReplyAsync($"无法找到具有该在线 ID ({id}) 的用户。").ConfigureAwait(false);
            return;
        }

        var oldComment = obj.Comment;
        obj.Comment = comment;
        await ReplyAsync($"完成。已将现有备注 ({oldComment}) 更改为 ({comment})。").ConfigureAwait(false);
    }

    [Command("blacklistId")]
    [Summary("将 Discord 用户 ID 加入黑名单。（适用于用户不在服务器中的情况）")]
    [RequireSudo]
    public async Task BlackListIDs([Summary("逗号分隔的 Discord ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var objects = IDs.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
        await ReplyAsync("完成。").ConfigureAwait(false);
    }

    [Command("blacklist")]
    [Summary("将提及的 Discord 用户加入黑名单。")]
    [RequireSudo]
    public async Task BlackListUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.AddIfNew(objects);
        await ReplyAsync("完成。").ConfigureAwait(false);
    }

    [Command("blacklistComment")]
    [Summary("为已加入黑名单的 Discord 用户 ID 添加备注。")]
    [RequireSudo]
    public async Task BlackListUsers(ulong id, [Remainder] string comment)
    {
        var obj = SysCordSettings.Settings.UserBlacklist.List.Find(z => z.ID == id);
        if (obj is null)
        {
            await ReplyAsync($"无法找到具有该 ID ({id}) 的用户。").ConfigureAwait(false);
            return;
        }

        var oldComment = obj.Comment;
        obj.Comment = comment;
        await ReplyAsync($"完成。已将现有备注 ({oldComment}) 更改为 ({comment})。").ConfigureAwait(false);
    }

    [Command("forgetUser")]
    [Alias("forget")]
    [Summary("忘记之前遇到的用户。")]
    [RequireSudo]
    public async Task ForgetPreviousUser([Summary("逗号分隔的在线 ID")][Remainder] string content)
    {
        foreach (var ID in GetIDs(content))
        {
            PokeRoutineExecutorBase.PreviousUsers.RemoveAllNID(ID);
            PokeRoutineExecutorBase.PreviousUsersDistribution.RemoveAllNID(ID);
        }
        await ReplyAsync("完成。").ConfigureAwait(false);
    }

    [Command("bannedIDSummary")]
    [Alias("printBannedID", "bannedIDPrint")]
    [Summary("打印已封禁的在线 ID 列表。")]
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
    [Summary("打印已加入黑名单的 Discord 用户列表。")]
    [RequireSudo]
    public async Task PrintBlacklist()
    {
        var lines = SysCordSettings.Settings.UserBlacklist.Summarize();
        var msg = string.Join("\n", lines);
        await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
    }

    [Command("previousUserSummary")]
    [Alias("prevUsers")]
    [Summary("打印之前遇到的用户列表。")]
    [RequireSudo]
    public async Task PrintPreviousUsers()
    {
        bool found = false;
        var lines = PokeRoutineExecutorBase.PreviousUsers.Summarize().ToList();
        if (lines.Count != 0)
        {
            found = true;
            var msg = "之前的用户:\n" + string.Join("\n", lines);
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }

        lines = [.. PokeRoutineExecutorBase.PreviousUsersDistribution.Summarize()];
        if (lines.Count != 0)
        {
            found = true;
            var msg = "之前的分发用户:\n" + string.Join("\n", lines);
            await ReplyAsync(Format.Code(msg)).ConfigureAwait(false);
        }
        if (!found)
            await ReplyAsync("未找到之前的用户。").ConfigureAwait(false);
    }

    [Command("unbanID")]
    [Summary("解封在线用户 ID。")]
    [RequireSudo]
    public async Task UnBanOnlineIDs([Summary("逗号分隔的在线 ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        var me = SysCord<T>.Runner;
        var hub = me.Hub;
        hub.Config.TradeAbuse.BannedIDs.RemoveAll(z => IDs.Any(o => o == z.ID));
        await ReplyAsync("完成。").ConfigureAwait(false);
    }

    [Command("unBlacklistId")]
    [Summary("从黑名单中移除 Discord 用户 ID。（适用于用户不在服务器中的情况）")]
    [RequireSudo]
    public async Task UnBlackListIDs([Summary("逗号分隔的 Discord ID")][Remainder] string content)
    {
        var IDs = GetIDs(content);
        SysCordSettings.Settings.UserBlacklist.RemoveAll(z => IDs.Any(o => o == z.ID));
        await ReplyAsync("完成。").ConfigureAwait(false);
    }

    [Command("unblacklist")]
    [Summary("从黑名单中移除提及的 Discord 用户。")]
    [RequireSudo]
    public async Task UnBlackListUsers([Remainder] string _)
    {
        var users = Context.Message.MentionedUsers;
        var objects = users.Select(GetReference);
        SysCordSettings.Settings.UserBlacklist.RemoveAll(z => objects.Any(o => o.ID == z.ID));
        await ReplyAsync("完成。").ConfigureAwait(false);
    }

    [Command("banTrade")]
    [Alias("bant")]
    [Summary("禁止用户交易并附带原因。")]
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
                await dmChannel.SendMessageAsync("未提供原因。请按以下格式使用命令:\n.banTrade {NID} {可选: 名称} {原因}\n示例: .banTrade 123456789 滥发交易");
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
                Comment = $"由 {Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd-hh:mm:ss} 封禁。原因: {banReason}"
            };

            hub.Config.TradeAbuse.BannedIDs.AddIfNew([bannedUser]);
            await dmChannel.SendMessageAsync($"完成。用户 {userName}（NID {userNID}）已被禁止交易。");
        }
        catch (Exception ex)
        {
            await dmChannel.SendMessageAsync($"发生错误: {ex.Message}");
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
        Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
    };
}
