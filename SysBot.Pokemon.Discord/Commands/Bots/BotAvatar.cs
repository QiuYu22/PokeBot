using Discord;
using Discord.Commands;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class BotAvatar : ModuleBase<SocketCommandContext>
    {
        [Command("setavatar")]
        [Alias("botavatar", "changeavatar", "sa", "ba")]
        [Summary("将机器人的头像设置为指定 GIF。")]
        [RequireOwner]
        public async Task SetAvatarAsync()
        {
            var userMessage = Context.Message;

            if (userMessage.Attachments.Count == 0)
            {
                var reply = await ReplyAsync("请附上要设置为头像的 GIF 图片。"); // 标准静态图可通过仪表板设置
                await Task.Delay(60000);
                await userMessage.DeleteAsync();
                await reply.DeleteAsync();
                return;
            }
            var attachment = userMessage.Attachments.First();
            if (!attachment.Filename.EndsWith(".gif"))
            {
                var reply = await ReplyAsync("请提供 GIF 图片。");
                await Task.Delay(60000);
                await userMessage.DeleteAsync();
                await reply.DeleteAsync();
                return;
            }

            using var httpClient = new HttpClient();
            var imageBytes = await httpClient.GetByteArrayAsync(attachment.Url);

            await using var ms = new MemoryStream(imageBytes);
            var image = new Image(ms);
            await Context.Client.CurrentUser.ModifyAsync(user => user.Avatar = image);

            var successReply = await ReplyAsync("头像更新成功！");
            await Task.Delay(60000);
            await userMessage.DeleteAsync();
            await successReply.DeleteAsync();
        }
    }
}
