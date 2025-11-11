using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Settings for the Web Control Panel server
/// </summary>
public sealed class WebServerSettings
{
    private const string WebServer = "Web 服务";
    
    [DisplayName("控制面板端口")]
    [Category(WebServer)]
    [Description("Bot 控制面板 Web 接口使用的端口号，默认 8080。")]
    public int ControlPanelPort { get; set; } = 8080;
    
    [DisplayName("启用 Web 控制面板")]
    [Category(WebServer)]
    [Description("启用或禁用 Web 控制面板。禁用后界面不可访问。")]
    public bool EnableWebServer { get; set; } = true;
    
    [DisplayName("允许外部访问")]
    [Category(WebServer)]
    [Description("允许外部设备访问 Web 控制面板；关闭时仅允许本机连接。")]
    public bool AllowExternalConnections { get; set; } = false;

    public override string ToString() => "Web 控制面板设置";
}