using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class LegalizerModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        [Command("convert"), Alias("showdown")]
        [Summary("尝试将 Showdown 配置转换为 RegenTemplate（生成派送模板）。")]
        [Priority(1)]
        public async Task ConvertShowdown([Summary("世代/格式")] byte gen, [Remainder][Summary("Showdown 配置")] string content)
        {
            var deleteMessageTask = LegalizerModule<T>.DeleteCommandMessageAsync(Context.Message, 2000);
            var convertTask = Context.Channel.ReplyWithLegalizedSetAsync(content, gen);
            await Task.WhenAll(deleteMessageTask, convertTask).ConfigureAwait(false);
        }

        [Command("convert"), Alias("showdown")]
        [Summary("尝试将 Showdown 配置转换为 RegenTemplate（生成派送模板）。")]
        [Priority(0)]
        public async Task ConvertShowdown([Remainder][Summary("Showdown 配置")] string content)
        {
            var deleteMessageTask = LegalizerModule<T>.DeleteCommandMessageAsync(Context.Message, 2000);
            var convertTask = Context.Channel.ReplyWithLegalizedSetAsync<T>(content);
            await Task.WhenAll(deleteMessageTask, convertTask).ConfigureAwait(false);
        }

        [Command("legalize"), Alias("alm")]
        [Summary("尝试将附件中的 pkm 数据合法化，并输出为 RegenTemplate（批量生成派送模板）。")]
        public async Task LegalizeAsync()
        {
            var deleteMessageTask = LegalizerModule<T>.DeleteCommandMessageAsync(Context.Message, 2000);
            var legalizationTasks = Context.Message.Attachments.Select(att =>
                Context.Channel.ReplyWithLegalizedSetAsync(att)
            ).ToArray();

            await Task.WhenAll(deleteMessageTask, Task.WhenAll(legalizationTasks)).ConfigureAwait(false);
        }

        private static async Task DeleteCommandMessageAsync(IUserMessage message, int delayMilliseconds)
        {
            await Task.Delay(delayMilliseconds).ConfigureAwait(false);
            await message.DeleteAsync().ConfigureAwait(false);
        }
    }
}
