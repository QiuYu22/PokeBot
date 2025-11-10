using PKHeX.Core;
using System.Linq;
using System.Text;

namespace SysBot.Pokemon.Helpers;

/// <summary>
/// Simplified legality feedback that focuses on extracting data from LegalityAnalysis.Results
/// </summary>
public static class SimpleLegalityFeedback
{
    public static string GetLegalityReport(PKM pkm, LegalityAnalysis la, string speciesName)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"**合法性分析：{speciesName}**");
        sb.AppendLine($"状态：{(la.Valid ? "✅ 合法" : "❌ 不合法")}");

        if (!la.Valid)
        {
            // Get all invalid checks from the Results list
            var invalidChecks = la.Results.Where(r => !r.Valid).ToList();

            if (invalidChecks.Count > 0)
            {
                sb.AppendLine("\n**发现的问题：**");

                // Group by identifier for better organization
                var groupedIssues = invalidChecks.GroupBy(r => r.Identifier);

                // Create localization context to convert CheckResult to human-readable messages
                var localizationSet = LegalityLocalizationSet.GetLocalization(GameLanguage.DefaultLanguage);
                var context = LegalityLocalizationContext.Create(la, localizationSet);

                foreach (var group in groupedIssues)
                {
                    sb.AppendLine($"\n{GetCategoryIcon(group.Key)} **{GetCategoryName(group.Key)}：**");

                    foreach (var issue in group)
                    {
                        // Clean up the comment for display
                        var cleanComment = context.Humanize(issue)
                            .Replace("Invalid:", "")
                            .Replace("Fishy:", "警告：")
                            .Trim();

                        sb.AppendLine($"  • {cleanComment}");
                    }
                }
            }

            // Add basic move analysis
            var moveIssues = invalidChecks.Where(r => r.Identifier == CheckIdentifier.CurrentMove).ToList();
            if (moveIssues.Count > 0)
            {
                sb.AppendLine("\n**招式提示：**");
                sb.AppendLine("  • 请确认该招式在目标世代中可用");
                sb.AppendLine("  • 检查招式组合是否同时合法");
                sb.AppendLine("  • 部分招式仅限活动或事件获得");
            }
        }
        else
        {
            sb.AppendLine($"\n✨ 你的 {speciesName} 已通过所有合法性检查！");
            if (la.EncounterOriginal != null)
            {
                sb.AppendLine($"遇见来源：{la.EncounterOriginal.LongName}");
            }
        }

        return sb.ToString();
    }

    private static string GetCategoryIcon(CheckIdentifier identifier) => identifier switch
    {
        CheckIdentifier.CurrentMove => "🎯",
        CheckIdentifier.Ability => "⚡",
        CheckIdentifier.Ball => "🏀",
        CheckIdentifier.Level => "📊",
        CheckIdentifier.Shiny => "✨",
        CheckIdentifier.Form => "🔄",
        CheckIdentifier.GameOrigin => "🎮",
        CheckIdentifier.Encounter => "📍",
        _ => "🔸"
    };

    public static string GetCategoryName(CheckIdentifier identifier) => identifier switch
    {
        CheckIdentifier.CurrentMove => "当前招式",
        CheckIdentifier.RelearnMove => "重学招式",
        CheckIdentifier.Ability => "特性",
        CheckIdentifier.Ball => "精灵球",
        CheckIdentifier.Level => "等级",
        CheckIdentifier.Shiny => "闪光状态",
        CheckIdentifier.Form => "形态",
        CheckIdentifier.GameOrigin => "来源版本",
        CheckIdentifier.Encounter => "遇见方式",
        CheckIdentifier.IVs => "个体值",
        CheckIdentifier.EVs => "努力值",
        CheckIdentifier.Nature => "性格",
        CheckIdentifier.Gender => "性别",
        _ => identifier.ToString()
    };
}
