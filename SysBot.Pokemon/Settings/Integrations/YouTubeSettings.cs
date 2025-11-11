using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class YouTubeSettings
{
    private const string Messages = "消息";

    private const string Operation = "运行";

    private const string Startup = "启动";

    [DisplayName("频道 ID")]
    [Category(Startup), Description("发送消息的频道 ID。")]
    public string ChannelID { get; set; } = string.Empty;

    [DisplayName("客户端 ID")]
    [Category(Startup), Description("机器人 Client ID。")]
    public string ClientID { get; set; } = string.Empty;

    // Startup
    [DisplayName("客户端密钥")]
    [Category(Startup), Description("机器人 Client Secret。")]
    public string ClientSecret { get; set; } = string.Empty;

    [DisplayName("命令前缀")]
    [Category(Startup), Description("机器人命令前缀。")]
    public char CommandPrefix { get; set; } = '$';

    [DisplayName("开始交易消息")]
    [Category(Operation), Description("解除屏障（开始交易）时发送的消息。")]
    public string MessageStart { get; set; } = string.Empty;

    [DisplayName("Sudo 用户列表")]
    [Category(Operation), Description("拥有 Sudo 权限的用户名。")]
    public string SudoList { get; set; } = string.Empty;

    // Operation
    [DisplayName("黑名单用户")]
    [Category(Operation), Description("禁止使用机器人的用户名列表。")]
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
