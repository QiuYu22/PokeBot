using Discord;
using Discord.Commands;
using Discord.Net;
using PKHeX.Core;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public static class ListHelpers<T> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

    public static async Task HandleListCommandAsync(SocketCommandContext context, string folderPath, string itemType,
        string commandPrefix, string args)
    {
        const int itemsPerPage = 20;
        var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;

        if (string.IsNullOrEmpty(folderPath))
        {
            await Helpers<T>.ReplyAndDeleteAsync(context, "该功能尚未在此机器人上配置。", 2);
            return;
        }

        var (filter, page) = Helpers<T>.ParseListArguments(args);

        var allFiles = Directory.GetFiles(folderPath)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(file => file != null)
            .OrderBy(file => file)
            .ToList()!;

        var filteredFiles = allFiles
            .Where(file => file != null && (string.IsNullOrWhiteSpace(filter) ||
                   file.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (filteredFiles.Count == 0)
        {
            var replyMessage = await context.Channel.SendMessageAsync($"没有找到与筛选条件“{filter}”匹配的 {itemType}。");
            _ = Helpers<T>.DeleteMessagesAfterDelayAsync(replyMessage, context.Message, 10);
            return;
        }

        var pageCount = (int)Math.Ceiling(filteredFiles.Count / (double)itemsPerPage);
        page = Math.Clamp(page, 1, pageCount);

        var pageItems = filteredFiles.Skip((page - 1) * itemsPerPage).Take(itemsPerPage);

        var embed = new EmbedBuilder()
            .WithTitle($"可用的 {char.ToUpper(itemType[0]) + itemType[1..]} - 筛选：“{filter}”")
            .WithDescription($"第 {page}/{pageCount} 页")
            .WithColor(Color.Blue);

        foreach (var item in pageItems)
        {
            var index = allFiles.IndexOf(item) + 1;
            embed.AddField($"{index}. {item}", $"使用 `{botPrefix}{commandPrefix} {index}` 请求该 {itemType.TrimEnd('s')}。");
        }

        await SendDMOrReplyAsync(context, embed.Build());
    }

    public static async Task SendDMOrReplyAsync(SocketCommandContext context, Embed embed)
    {
        IUserMessage replyMessage;

        if (context.User is IUser user)
        {
            try
            {
                var dmChannel = await user.CreateDMChannelAsync();
                await dmChannel.SendMessageAsync(embed: embed);
                replyMessage = await context.Channel.SendMessageAsync($"{context.User.Mention}，已通过私信发送列表。");
            }
            catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
            {
                replyMessage = await context.Channel.SendMessageAsync($"{context.User.Mention}，无法发送私信，请检查你的**服务器隐私设置**。");
            }
        }
        else
        {
            replyMessage = await context.Channel.SendMessageAsync("**错误**：无法发送私信，请检查你的**服务器隐私设置**。");
        }

        _ = Helpers<T>.DeleteMessagesAfterDelayAsync(replyMessage, context.Message, 10);
    }

    public static async Task HandleRequestCommandAsync(SocketCommandContext context, string folderPath, int index,
        string itemType, string listCommand)
    {
        var userID = context.User.Id;
        if (!await Helpers<T>.EnsureUserNotInQueueAsync(userID))
        {
            await Helpers<T>.ReplyAndDeleteAsync(context,
                "你在队列中已有一个无法清除的交易，请等待处理完成。", 2);
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                await Helpers<T>.ReplyAndDeleteAsync(context, "该功能尚未在此机器人上配置。", 2);
                return;
            }

            var files = Directory.GetFiles(folderPath)
                .Select(Path.GetFileName)
                .Where(x => x != null)
                .OrderBy(x => x)
                .ToList()!;

            if (index < 1 || index > files.Count)
            {
                await Helpers<T>.ReplyAndDeleteAsync(context,
                    $"无效的 {itemType} 索引。请使用 `.{listCommand}` 命令提供的有效编号。", 2);
                return;
            }

            var selectedFile = files[index - 1];
            var fileData = await File.ReadAllBytesAsync(Path.Combine(folderPath, selectedFile!));
            var download = new Download<PKM>
            {
                Data = EntityFormat.GetFromBytes(fileData),
                Success = true
            };

            var pk = Helpers<T>.GetRequest(download);
            if (pk == null)
            {
                await Helpers<T>.ReplyAndDeleteAsync(context,
                    $"无法将 {itemType} 文件转换为所需的 PKM 类型。", 2);
                return;
            }

            var code = Info.GetRandomTradeCode(userID);
            var lgcode = Info.GetRandomLGTradeCode();
            var sig = context.User.GetFavor();

            await context.Channel.SendMessageAsync($"{char.ToUpper(itemType[0]) + itemType[1..]} 请求已加入队列。").ConfigureAwait(false);
            await Helpers<T>.AddTradeToQueueAsync(context, code, context.User.Username, pk, sig,
                context.User, lgcode: lgcode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Helpers<T>.ReplyAndDeleteAsync(context, $"发生错误：{ex.Message}", 2);
        }
        finally
        {
            if (context.Message is IUserMessage userMessage)
                _ = Helpers<T>.DeleteMessagesAfterDelayAsync(userMessage, null, 2);
        }
    }
}
