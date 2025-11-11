using Discord;
using Discord.WebSocket;
using System.Linq;

namespace SysBot.Pokemon.Discord;

public static class MedalHelpers
{
    public static int GetCurrentMilestone(int totalTrades)
    {
        int[] milestones = [700, 650, 600, 550, 500, 450, 400, 350, 300, 250, 200, 150, 100, 50, 1];
        return milestones.FirstOrDefault(m => totalTrades >= m, 0);
    }

    public static Embed CreateMedalsEmbed(SocketUser user, int milestone, int totalTrades)
    {
        string status = milestone switch
        {
            1 => "新手训练家",
            50 => "初级训练家",
            100 => "宝可梦研究员",
            150 => "宝可梦专家",
            200 => "宝可梦冠军",
            250 => "宝可梦英雄",
            300 => "宝可梦精英",
            350 => "宝可梦交易员",
            400 => "宝可梦贤者",
            450 => "宝可梦传说",
            500 => "地区宗师",
            550 => "交易大师",
            600 => "享誉世界",
            650 => "宝可梦大师",
            700 => "宝可梦之神",
            _ => "新晋训练家"
        };

        string description = $"累计交易：**{totalTrades}**\n**当前称号：** {status}";

        if (milestone > 0)
        {
            string imageUrl = $"https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/{milestone:D3}.png";
            return new EmbedBuilder()
                .WithTitle($"{user.Username}'s Trading Status")
                .WithTitle($"{user.Username} 的交易状态")
                .WithColor(new Color(255, 215, 0))
                .WithDescription(description)
                .WithThumbnailUrl(imageUrl)
                .Build();
        }
        else
        {
            return new EmbedBuilder()
                .WithTitle($"{user.Username} 的交易状态")
                .WithColor(new Color(255, 215, 0))
                .WithDescription(description)
                .Build();
        }
    }
}
