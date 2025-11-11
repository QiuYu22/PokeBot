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
                LogUtil.LogInfo($"尝试回显消息到转发器 {fwd} 时发生异常：{ex}。原始内容：{message}", "Echo");
                LogUtil.LogSafe(ex, "Echo");
            }
        }
        LogUtil.LogInfo(message, "回声");
    }
}
