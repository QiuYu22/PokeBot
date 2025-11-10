using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public static class BatchHelpers<T> where T : PKM, new()
{
    public static List<string> ParseBatchTradeContent(string content)
    {
        var delimiters = new[] { "---", "—-" };
        return [.. content.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Select(trade => trade.Trim())];
    }

    public static async Task<(T? Pokemon, string? Error, ShowdownSet? Set, string? LegalizationHint)> ProcessSingleTradeForBatch(string tradeContent)
    {
        var result = await Helpers<T>.ProcessShowdownSetAsync(tradeContent);

        if (result.Pokemon != null)
        {
            return (result.Pokemon, null, result.ShowdownSet, null);
        }

        return (null, result.Error, result.ShowdownSet, result.LegalizationHint);
    }

    public static async Task SendBatchErrorEmbedAsync(SocketCommandContext context, List<BatchTradeError> errors, int totalTrades)
    {
        var embed = new EmbedBuilder()
            .WithTitle("❌ 批量交易校验失败")
            .WithColor(Color.Red)
            .WithDescription($"共有 {errors.Count} / {totalTrades} 只宝可梦处理失败。")
            .WithFooter("请修正无效的配置后再试。");

        foreach (var error in errors)
        {
            var fieldValue = $"**错误：** {error.ErrorMessage}";
            if (!string.IsNullOrEmpty(error.LegalizationHint))
            {
                fieldValue += $"\n💡 **提示：** {error.LegalizationHint}";
            }

            if (!string.IsNullOrEmpty(error.ShowdownSet))
            {
                var lines = error.ShowdownSet.Split('\n').Take(2);
                fieldValue += $"\n**配置：** {string.Join(" | ", lines)}...";
            }

            if (fieldValue.Length > 1024)
            {
                fieldValue = fieldValue[..1021] + "...";
            }

            embed.AddField($"交易 #{error.TradeNumber} - {error.SpeciesName}", fieldValue);
        }

        var replyMessage = await context.Channel.SendMessageAsync(embed: embed.Build());
        _ = Helpers<T>.DeleteMessagesAfterDelayAsync(replyMessage, context.Message, 20);
    }

    public static async Task ProcessBatchContainer(SocketCommandContext context, List<T> batchPokemonList,
        int batchTradeCode, int totalTrades)
    {
        var sig = context.User.GetFavor();
        var firstPokemon = batchPokemonList[0];

        await QueueHelper<T>.AddBatchContainerToQueueAsync(context, batchTradeCode, context.User.Username,
            firstPokemon, batchPokemonList, sig, context.User, totalTrades).ConfigureAwait(false);
    }

    public static string BuildDetailedBatchErrorMessage(List<BatchTradeError> errors, int totalTrades)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**批量交易校验失败**");
        sb.AppendLine($"❌ 共有 {errors.Count} / {totalTrades} 只宝可梦处理失败。\n");

        foreach (var error in errors)
        {
            sb.AppendLine($"**交易 #{error.TradeNumber} - {error.SpeciesName}**");
            sb.AppendLine($"错误：{error.ErrorMessage}");

            if (!string.IsNullOrEmpty(error.LegalizationHint))
            {
                sb.AppendLine($"💡 提示：{error.LegalizationHint}");
            }

            if (!string.IsNullOrEmpty(error.ShowdownSet))
            {
                var lines = error.ShowdownSet.Split('\n').Take(3);
                sb.AppendLine($"配置预览：{string.Join(" | ", lines)}...");
            }

            sb.AppendLine();
        }

        sb.AppendLine("**请修正无效的配置后重新尝试。**");
        return sb.ToString();
    }
}
