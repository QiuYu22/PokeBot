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
                LogUtil.LogInfo("回显", $"转发消息时发生异常：{ex}；消息内容：{message}；转发器：{fwd}");
                LogUtil.LogSafe(ex, "Echo");
            }
        }
        LogUtil.LogInfo("回显", message);
    }
}
