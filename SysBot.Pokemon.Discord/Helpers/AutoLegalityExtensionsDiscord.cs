using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public static class AutoLegalityExtensionsDiscord
{
    public static async Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, ITrainerInfo sav, ShowdownSet set)
    {
        if (set.Species <= 0)
        {
            await channel.SendMessageAsync("抱歉！无法解析你的消息。如果你在转换配置，请确认粘贴的内容无误。").ConfigureAwait(false);
            return;
        }

        try
        {
            var template = AutoLegalityWrapper.GetTemplate(set);

            // Check if this is an egg request based on nickname
            bool isEggRequest = set.Nickname.Equals("egg", StringComparison.CurrentCultureIgnoreCase) && Breeding.CanHatchAsEgg(set.Species);

            PKM pkm;
            string result;
            if (isEggRequest)
            {
                // Generate as egg using ALM's GenerateEgg method
                pkm = sav.GenerateEgg(template, out var eggResult);
                result = eggResult.ToString();
            }
            else
            {
                // Generate normally
                pkm = sav.GetLegal(template, out result);
            }

            var la = new LegalityAnalysis(pkm);
            var spec = GameInfo.Strings.Species[template.Species];
            if (!la.Valid)
            {
                var reason = result == "超时" ? $"该 {spec} 配置生成耗时过长。" : result == "版本不匹配" ? "请求被拒：PKHeX 与 Auto-Legality Mod 版本不匹配。" : $"无法根据该配置生成 {spec}。";
                var imsg = $"抱歉！{reason}";
                if (result == "失败")
                    imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                await channel.SendMessageAsync(imsg).ConfigureAwait(false);
                return;
            }
            var msg = $"这是你的（{result}）合法化 {spec}（{la.EncounterOriginal.Name}）！";
            await channel.SendPKMAsync(pkm, msg + $"\n{ReusableActions.GetFormattedShowdownText(pkm)}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(AutoLegalityExtensionsDiscord));
            var msg = $"抱歉！处理该 Showdown 配置时发生意外问题：\n```{string.Join("\n", set.GetSetLines())}```";
            await channel.SendMessageAsync(msg).ConfigureAwait(false);
        }
    }

    public static Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, string content, byte gen)
    {
        content = ReusableActions.StripCodeBlock(content);
        var set = new ShowdownSet(content);
        var sav = AutoLegalityWrapper.GetTrainerInfo(gen);
        return channel.ReplyWithLegalizedSetAsync(sav, set);
    }

    public static Task ReplyWithLegalizedSetAsync<T>(this ISocketMessageChannel channel, string content) where T : PKM, new()
    {
        content = ReusableActions.StripCodeBlock(content);
        var set = new ShowdownSet(content);
        var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
        return channel.ReplyWithLegalizedSetAsync(sav, set);
    }

    public static async Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, IAttachment att)
    {
        var download = await NetUtil.DownloadPKMAsync(att).ConfigureAwait(false);
        if (!download.Success)
        {
            await channel.SendMessageAsync(download.ErrorMessage).ConfigureAwait(false);
            return;
        }

        var pkm = download.Data!;
        if (new LegalityAnalysis(pkm).Valid)
        {
            await channel.SendMessageAsync($"{download.SanitizedFileName}：该文件已合法化。").ConfigureAwait(false);
            return;
        }

        var legal = pkm.LegalizePokemon();
        if (!new LegalityAnalysis(legal).Valid)
        {
            await channel.SendMessageAsync($"{download.SanitizedFileName}：无法完成合法化。").ConfigureAwait(false);
            return;
        }

        legal.RefreshChecksum();

        var msg = $"这是你的合法化 PKM：{download.SanitizedFileName}！\n{ReusableActions.GetFormattedShowdownText(legal)}";
        await channel.SendPKMAsync(legal, msg).ConfigureAwait(false);
    }
}
