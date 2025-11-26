using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Settings for the Web Control Panel server
/// </summary>
public sealed class WebServerSettings
{
    private const string WebServer = "Web 服务器";
    
    [Category(WebServer), Description("机器人控制面板 Web 界面的端口号。默认为 8080。"), DisplayName("控制面板端口")]
    public int ControlPanelPort { get; set; } = 8080;
    
    [Category(WebServer), Description("启用或禁用 Web 控制面板。禁用后，Web 界面将无法访问。"), DisplayName("启用 Web 服务器")]
    public bool EnableWebServer { get; set; } = true;
    
    [Category(WebServer), Description("允许外部连接到 Web 控制面板。设置为 false 时，仅允许本地连接。"), DisplayName("允许外部连接")]
    public bool AllowExternalConnections { get; set; } = false;
}