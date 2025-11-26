using Discord;
using Discord.Commands;
using Discord.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class HelpModule(CommandService commandService) : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commandService = commandService;

        [Command("help")]
        [Summary("显示可用的命令。")]
        public async Task HelpAsync(int page = 1)
        {
            var mgr = SysCordSettings.Manager;
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            var owner = app.Owner.Id;
            var uid = Context.User.Id;

            var modules = _commandService.Modules.ToList();
            var moduleList = new Dictionary<string, Dictionary<string, string>>();

            foreach (var module in modules)
            {
                var moduleName = module.Name;
                var commandDict = new Dictionary<string, string>();

                foreach (var command in module.Commands)
                {
                    if (command.CheckPreconditionsAsync(Context).GetAwaiter().GetResult().IsSuccess)
                    {
                        if (command.Attributes.Any(a => a is RequireOwnerAttribute) && owner != uid)
                            continue;
                        if (command.Attributes.Any(a => a is RequireSudoAttribute) && !mgr.CanUseSudo(uid))
                            continue;

                        var commandName = command.Name;
                        var commandSummary = command.Summary ?? "暂无描述。";

                        if (!commandDict.ContainsKey(commandName))
                            commandDict.Add(commandName, commandSummary);
                    }
                }

                if (commandDict.Count > 0)
                {
                    var moduleSanitizedName = moduleName.Split('`')[0];

                    var uniqueModuleName = moduleSanitizedName;
                    var count = 1;
                    while (moduleList.ContainsKey(uniqueModuleName))
                    {
                        uniqueModuleName = $"{moduleSanitizedName}_{count}";
                        count++;
                    }

                    moduleList.Add(uniqueModuleName, commandDict);
                }
            }

            var sortedModules = moduleList.OrderByDescending(x => x.Key.StartsWith("TradeModule")).ThenBy(x => x.Key).ToList();

            var pages = new List<string>();
            var currentPage = new StringBuilder();
            var lineCount = 0;

            foreach (var module in sortedModules)
            {
                currentPage.AppendLine($"**{module.Key}**");
                lineCount++;

                foreach (var command in module.Value)
                {
                    currentPage.AppendLine($"`{command.Key}` - {command.Value}");
                    lineCount++;

                    if (lineCount >= 45)
                    {
                        pages.Add(currentPage.ToString());
                        currentPage.Clear();
                        lineCount = 0;
                    }
                }

                if (lineCount > 0)
                {
                    currentPage.AppendLine();
                    lineCount++;
                }
            }

            if (currentPage.Length > 0)
                pages.Add(currentPage.ToString());

            var pageCount = pages.Count;
            if (page < 1 || page > pageCount)
            {
                await ReplyAsync($"无效的页码。请指定 1 到 {pageCount} 之间的数字。");
                return;
            }

            var footerText = $"第 {page}/{pageCount} 页";
            if (page < pageCount)
                footerText += $" | 输入 `help {page + 1}` 查看下一页。";

            var embedBuilder = new EmbedBuilder()
                .WithTitle("可用命令")
                .WithColor(Color.Blue)
                .WithDescription(pages[page - 1])
                .WithFooter(footerText);

            try
            {
                var dmChannel = await Context.User.CreateDMChannelAsync();
                await dmChannel.SendMessageAsync(embed: embedBuilder.Build());
                await ReplyAsync($"{Context.User.Mention}，我已通过私信向您发送了帮助信息！");
            }
            catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
            {
                await ReplyAsync($"{Context.User.Mention}，我无法向您发送私信，因为您已禁用私信。请启用私信后重试。");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"发送私信时发生错误: {ex.Message}");
            }

            if (Context.Message is IUserMessage userMessage)
                await userMessage.DeleteAsync().ConfigureAwait(false);
        }

        [Command("help")]
        [Summary("显示特定命令的信息。")]
        public async Task HelpAsync([Summary("要获取信息的命令。")] string command)
        {
            var searchResult = _commandService.Search(Context, command);

            if (!searchResult.IsSuccess)
            {
                await ReplyAsync($"抱歉，我找不到命令 **{command}**。");
                return;
            }

            var embedBuilder = new EmbedBuilder()
                .WithTitle($"{command} 的帮助")
                .WithColor(Color.Blue);

            foreach (var match in searchResult.Commands)
            {
                var cmd = match.Command;

                var parameters = cmd.Parameters.Select(p => $"`{p.Name}` - {p.Summary}");
                var parameterSummary = string.Join("\n", parameters);

                embedBuilder.AddField(cmd.Name, $"{cmd.Summary}\n\n**参数:**\n{parameterSummary}", false);
            }

            try
            {
                var dmChannel = await Context.User.CreateDMChannelAsync();
                await dmChannel.SendMessageAsync(embed: embedBuilder.Build());
                await ReplyAsync($"{Context.User.Mention}，我已通过私信向您发送了命令 **{command}** 的帮助信息！");
            }
            catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
            {
                await ReplyAsync($"{Context.User.Mention}，我无法向您发送私信，因为您已禁用私信。请启用私信后重试。");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"发送私信时发生错误: {ex.Message}");
            }

            if (Context.Message is IUserMessage userMessage)
                await userMessage.DeleteAsync().ConfigureAwait(false);
        }
    }
}
