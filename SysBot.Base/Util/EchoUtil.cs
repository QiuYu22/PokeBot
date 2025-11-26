using System;
using System.Collections.Generic;

namespace SysBot.Base;

public static class EchoUtil
{
    public static readonly List<Action<string>> Forwarders = [];

    public static void Echo(string message)
    {
        foreach (var fwd in Forwarders)
        {
            try
            {
                fwd(message);
            }
            catch (Exception ex)
            {
                LogUtil.LogInfo($"异常: {ex} 在尝试回显消息: {message} 到转发器: {fwd} 时发生", "回显");
                LogUtil.LogSafe(ex, "回显");
            }
        }
        LogUtil.LogInfo(message, "回显");
    }
}
