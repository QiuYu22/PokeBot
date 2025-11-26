using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class YouTubeSettings
{
    private const string Messages = "消息";

    private const string Operation = "操作";

    private const string Startup = "启动";

    [Category(Startup), Description("发送消息的频道 ID"), DisplayName("频道 ID")]
    public string ChannelID { get; set; } = string.Empty;

    [Category(Startup), Description("机器人客户端 ID"), DisplayName("客户端 ID")]
    public string ClientID { get; set; } = string.Empty;

    // 启动
    [Category(Startup), Description("机器人客户端密钥"), DisplayName("客户端密钥")]
    public string ClientSecret { get; set; } = string.Empty;

    [Category(Startup), Description("机器人命令前缀"), DisplayName("命令前缀")]
    public char CommandPrefix { get; set; } = '$';

    [Category(Operation), Description("屏障释放时发送的消息。"), DisplayName("开始消息")]
    public string MessageStart { get; set; } = string.Empty;

    [Category(Operation), Description("Sudo 用户名"), DisplayName("Sudo 列表")]
    public string SudoList { get; set; } = string.Empty;

    // 操作
    [Category(Operation), Description("具有这些用户名的用户无法使用机器人。"), DisplayName("用户黑名单")]
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
