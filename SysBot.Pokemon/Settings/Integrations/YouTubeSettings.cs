using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class YouTubeSettings
{
    private const string Messages = nameof(Messages);

    private const string Operation = nameof(Operation);

    private const string Startup = nameof(Startup);

    [Category(Startup), DisplayName("频道ID"), Description("发送消息的频道ID")]
    public string ChannelID { get; set; } = string.Empty;

    [Category(Startup), DisplayName("客户端ID"), Description("机器人客户端ID")]
    public string ClientID { get; set; } = string.Empty;

    // Startup
    [Category(Startup), DisplayName("客户端密钥"), Description("机器人客户端密钥")]
    public string ClientSecret { get; set; } = string.Empty;

    [Category(Startup), DisplayName("命令前缀"), Description("机器人命令前缀")]
    public char CommandPrefix { get; set; } = '$';

    [Category(Operation), DisplayName("开始消息"), Description("当屏障释放时发送的消息")]
    public string MessageStart { get; set; } = string.Empty;

    [Category(Operation), DisplayName("管理员列表"), Description("管理员用户名列表")]
    public string SudoList { get; set; } = string.Empty;

    // Operation
    [Category(Operation), DisplayName("用户黑名单"), Description("拥有这些用户名的用户无法使用机器人")]
    public string UserBlacklist { get; set; } = string.Empty;

    public bool IsSudo(string username)
    {
        var sudos = SudoList.Split([",", ", ", " "], StringSplitOptions.RemoveEmptyEntries);
        return sudos.Contains(username);
    }

    public override string ToString() => "YouTube 集成设置";
}

public enum YouTubeMessageDestination
{
    Disabled,

    Channel,
}
