using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

/// <summary>
/// Same as <see cref="RequireRoleAccessAttribute"/> with extra consideration for bots accepting Queue requests.
/// </summary>
public sealed class RequireQueueRoleAttribute(string RoleName) : PreconditionAttribute
{
    // Create a field to store the specified name

    // Create a constructor so the name can be specified

    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        var mgr = SysCordSettings.Manager;
        if (mgr.Config.AllowGlobalSudo && mgr.CanUseSudo(context.User.Id))
            return Task.FromResult(PreconditionResult.FromSuccess());

        // Check if this user is a Guild User, which is the only context where roles exist
        if (context.User is not SocketGuildUser gUser)
            return Task.FromResult(PreconditionResult.FromError("该命令需要在服务器频道中执行。"));

        var roles = gUser.Roles;
        if (mgr.CanUseSudo(roles.Select(z => z.Name)))
            return Task.FromResult(PreconditionResult.FromSuccess());

        bool canQueue = SysCordSettings.HubConfig.Queues.CanQueue;
        if (!canQueue)
            return Task.FromResult(PreconditionResult.FromError("抱歉，我目前不接受新的排队请求！"));

        if (!mgr.GetHasRoleAccess(RoleName, roles.Select(z => z.Name)))
            return Task.FromResult(PreconditionResult.FromError("你没有执行此命令所需的角色权限。"));

        return Task.FromResult(PreconditionResult.FromSuccess());
    }
}
