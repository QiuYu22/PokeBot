using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class LegalityCheckModule : ModuleBase<SocketCommandContext>
{
    [Command("lc"), Alias("check", "validate", "verify")]
    [Summary("验证附件的合法性。")]
    public async Task LegalityCheck()
    {
        foreach (var att in (System.Collections.Generic.IReadOnlyCollection<Attachment>)Context.Message.Attachments)
            await LegalityCheck(att, false).ConfigureAwait(false);
    }

    [Command("lcv"), Alias("verbose")]
    [Summary("验证附件的合法性并输出详细信息。")]
    public async Task LegalityCheckVerbose()
    {
        foreach (var att in (System.Collections.Generic.IReadOnlyCollection<Attachment>)Context.Message.Attachments)
            await LegalityCheck(att, true).ConfigureAwait(false);
    }

    private async Task LegalityCheck(IAttachment att, bool verbose)
    {
        var download = await NetUtil.DownloadPKMAsync(att).ConfigureAwait(false);
        if (!download.Success)
        {
            await ReplyAsync(download.ErrorMessage).ConfigureAwait(false);
            return;
        }

        var pkm = download.Data!;
        var la = new LegalityAnalysis(pkm);
        var builder = new EmbedBuilder
        {
            Color = la.Valid ? Color.Green : Color.Red,
            Description = $"{download.SanitizedFileName} 的合法性报告:",
        };

        builder.AddField(x =>
        {
            x.Name = la.Valid ? "合法" : "不合法";
            x.Value = la.Report(verbose);
            x.IsInline = false;
        });

        await ReplyAsync("这是合法性报告！", false, builder.Build()).ConfigureAwait(false);
    }
}
