using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Settings for the Web Control Panel server
/// </summary>
public sealed class WebServerSettings
{
    private const string WebServer = "Web 服务器";
    
    [Category(WebServer)]
    [Description("机器人控制面板 Web 界面使用的端口号，默认 8080。")]
    [DisplayName("控制面板端口")]
    public int ControlPanelPort { get; set; } = 8080;
    
    [Category(WebServer)]
    [Description("启用或禁用 Web 控制面板。禁用后无法通过网页访问。")]
    [DisplayName("启用 Web 控制面板")]
    public bool EnableWebServer { get; set; } = true;
    
    [Category(WebServer)]
    [Description("允许来自外部的 Web 控制面板连接；关闭时仅允许本机访问。")]
    [DisplayName("允许外部连接")]
    public bool AllowExternalConnections { get; set; } = false;
}