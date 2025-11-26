using Discord;
using Discord.Commands;
using SysBot.Pokemon.Helpers;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

// src: https://github.com/foxbot/patek/blob/master/src/Patek/Modules/InfoModule.cs
// ISC License (ISC)
// Copyright 2017, Christopher F. <foxbot@protonmail.com>
public class InfoModule : ModuleBase<SocketCommandContext>
{
    private const string detail = "我是一个由 hexbyt3 开发的开源宝可梦交易 Discord 机器人。";

    private const string repo = "https://github.com/hexbyt3/PokeBot";

    [Command("info")]
    [Alias("about", "whoami", "owner")]
    public async Task InfoAsync()
    {
        var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);

        var builder = new EmbedBuilder
        {
            Color = new Color(114, 137, 218),
            Description = detail,
        };

        builder.AddField("信息",
            $"- [源代码]({repo})\n" +
            $"- {Format.Bold("所有者")}: {app.Owner} ({app.Owner.Id})\n" +
            $"- {Format.Bold("库")}: Discord.Net ({DiscordConfig.Version})\n" +
            $"- {Format.Bold("运行时间")}: {GetUptime()}\n" +
            $"- {Format.Bold("运行环境")}: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture} " +
            $"({RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture})\n" +
            $"- {Format.Bold("构建时间")}: {GetVersionInfo("SysBot.Base", false)}\n" +
            $"- {Format.Bold("SysBot+ 版本")}: {PokeBot.Version}\n" +
            $"- {Format.Bold("Core 版本")}: {GetVersionInfo("PKHeX.Core")}\n" +
            $"- {Format.Bold("AutoLegality 版本")}: {GetVersionInfo("PKHeX.Core.AutoMod")}\n"
        );

        builder.AddField("统计",
            $"- {Format.Bold("堆大小")}: {GetHeapSize()}MiB\n" +
            $"- {Format.Bold("服务器数")}: {Context.Client.Guilds.Count}\n" +
            $"- {Format.Bold("频道数")}: {Context.Client.Guilds.Sum(g => g.Channels.Count)}\n" +
            $"- {Format.Bold("用户数")}: {Context.Client.Guilds.Sum(g => g.MemberCount)}\n"
        );

        await ReplyAsync("这是关于我的一些信息！", embed: builder.Build()).ConfigureAwait(false);
    }

    private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString(CultureInfo.CurrentCulture);

    private static string GetUptime() => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");

    private static string GetVersionInfo(string assemblyName, bool inclVersion = true)
    {
        const string _default = "Unknown";
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var assembly = Array.Find(assemblies, x => x.GetName().Name == assemblyName);

        var attribute = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute is null)
            return _default;

        var info = attribute.InformationalVersion;
        var split = info.Split('+');
        if (split.Length >= 2)
        {
            var version = split[0];
            var revision = split[1];
            if (DateTime.TryParseExact(revision, "yyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var buildTime))
                return (inclVersion ? $"{version} " : "") + $@"{buildTime:yy-MM-dd\.hh\:mm}";
            return inclVersion ? version : _default;
        }
        return _default;
    }
}
