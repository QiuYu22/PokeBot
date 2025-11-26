using PKHeX.Core;
using SysBot.Base;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public class StreamSettings
{
    private const string Operation = "操作";

    private static readonly byte[] BlackPixel = // 1x1 black pixel
    [
        0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00,
    ];

    public static Action<PKM, string>? CreateSpriteFile { get; set; }

    [Category(Operation), Description("显示已完成交易数的格式。{0} = 计数"), DisplayName("已完成交易格式")]
    public string CompletedTradesFormat { get; set; } = "已完成交易: {0}";

    [Category(Operation), Description("如果存在 TradeBlockFile 则复制它，否则复制占位符图像。"), DisplayName("复制图像文件")]
    public bool CopyImageFile { get; set; } = true;

    [Category(Operation), Description("生成直播素材；关闭将阻止生成素材。"), DisplayName("创建素材")]
    public bool CreateAssets { get; set; }

    [Category(Operation), Description("在新交易开始时创建一个指示已完成交易数的文件。"), DisplayName("创建已完成交易")]
    public bool CreateCompletedTrades { get; set; } = true;

    [Category(Operation), Description("创建一个文件，列出用户加入队列后需要等待的预计时间。"), DisplayName("创建预计时间")]
    public bool CreateEstimatedTime { get; set; } = true;

    [Category(Operation), Description("生成当前待命人员列表。"), DisplayName("创建待命列表")]
    public bool CreateOnDeck { get; set; } = true;

    [Category(Operation), Description("生成当前待命人员列表 #2。"), DisplayName("创建待命列表 2")]
    public bool CreateOnDeck2 { get; set; } = true;

    [Category(Operation), Description("生成交易开始详情，指示机器人正在与谁交易。"), DisplayName("创建交易开始")]
    public bool CreateTradeStart { get; set; } = true;

    [Category(Operation), Description("生成交易开始详情，指示机器人正在交易什么。"), DisplayName("创建交易开始精灵")]
    public bool CreateTradeStartSprite { get; set; } = true;

    [Category(Operation), Description("生成当前正在交易的人员列表。"), DisplayName("创建用户列表")]
    public bool CreateUserList { get; set; } = true;

    [Category(Operation), Description("创建一个指示队列中用户数量的文件。"), DisplayName("创建队列用户数")]
    public bool CreateUsersInQueue { get; set; } = true;

    [Category(Operation), Description("创建一个文件，列出最近出队用户等待的时间。"), DisplayName("创建等待时间")]
    public bool CreateWaitedTime { get; set; } = true;

    [Category(Operation), Description("显示预计等待时间戳的格式。"), DisplayName("预计完成格式")]
    public string EstimatedFulfillmentFormat { get; set; } = @"hh\:mm\:ss";

    // 预计时间
    [Category(Operation), Description("显示预计等待时间的格式。"), DisplayName("预计时间格式")]
    public string EstimatedTimeFormat { get; set; } = "预计时间: {0:F1} 分钟";

    [Category(Operation), Description("显示待命列表用户的格式。{0} = ID, {3} = 用户"), DisplayName("待命格式")]
    public string OnDeckFormat { get; set; } = "(ID {0}) - {3}";

    [Category(Operation), Description("显示待命列表 #2 用户的格式。{0} = ID, {3} = 用户"), DisplayName("待命格式 2")]
    public string OnDeckFormat2 { get; set; } = "(ID {0}) - {3}";

    [Category(Operation), Description("分隔待命列表用户的分隔符。"), DisplayName("待命分隔符")]
    public string OnDeckSeparator { get; set; } = "\n";

    [Category(Operation), Description("分隔待命列表 #2 用户的分隔符。"), DisplayName("待命分隔符 2")]
    public string OnDeckSeparator2 { get; set; } = "\n";

    [Category(Operation), Description("要跳过的顶部待命用户数。如果要隐藏正在处理的人，请将其设置为你的主机数量。"), DisplayName("待命跳过数")]
    public int OnDeckSkip { get; set; }

    [Category(Operation), Description("要跳过的顶部待命 #2 用户数。如果要隐藏正在处理的人，请将其设置为你的主机数量。"), DisplayName("待命跳过数 2")]
    public int OnDeckSkip2 { get; set; }

    // 待命
    [Category(Operation), Description("待命列表中显示的用户数。"), DisplayName("待命显示数")]
    public int OnDeckTake { get; set; } = 5;

    // 待命 2
    [Category(Operation), Description("待命列表 #2 中显示的用户数。"), DisplayName("待命显示数 2")]
    public int OnDeckTake2 { get; set; } = 5;

    // 交易密码遮挡
    [Category(Operation), Description("输入交易密码时要复制的图像源文件名。如果留空，将创建占位符图像。"), DisplayName("交易遮挡文件")]
    public string TradeBlockFile { get; set; } = string.Empty;

    [Category(Operation), Description("连接密码遮挡图像的目标文件名。{0} 将被替换为本地 IP 地址。"), DisplayName("交易遮挡格式")]
    public string TradeBlockFormat { get; set; } = "block_{0}.png";

    [Category(Operation), Description("显示正在交易详情的格式。{0} = ID, {1} = 用户"), DisplayName("训练家交易开始")]
    public string TrainerTradeStart { get; set; } = "(ID {0}) {1}";

    [Category(Operation), Description("显示列表用户的格式。{0} = ID, {3} = 用户"), DisplayName("用户列表格式")]
    public string UserListFormat { get; set; } = "(ID {0}) - {3}";

    [Category(Operation), Description("分隔列表用户的分隔符。"), DisplayName("用户列表分隔符")]
    public string UserListSeparator { get; set; } = ", ";

    [Category(Operation), Description("要跳过的顶部用户数。如果要隐藏正在处理的人，请将其设置为你的主机数量。"), DisplayName("用户列表跳过数")]
    public int UserListSkip { get; set; }

    // 用户列表
    [Category(Operation), Description("列表中显示的用户数。"), DisplayName("用户列表显示数")]
    public int UserListTake { get; set; } = -1;

    // 队列中的用户
    [Category(Operation), Description("显示队列中用户数的格式。{0} = 计数"), DisplayName("队列用户数格式")]
    public string UsersInQueueFormat { get; set; } = "队列中的用户: {0}";

    // 等待时间
    [Category(Operation), Description("显示最近出队用户等待时间的格式。"), DisplayName("等待时间格式")]
    public string WaitedTimeFormat { get; set; } = @"hh\:mm\:ss";

    public void EndEnterCode(PokeRoutineExecutorBase b)
    {
        try
        {
            var file = GetBlockFileName(b);
            if (File.Exists(file))
                File.Delete(file);
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    public void IdleAssets(PokeRoutineExecutorBase b)
    {
        if (!CreateAssets)
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly))
            {
                if (file.Contains(b.Connection.Name))
                    File.Delete(file);
            }

            if (CreateWaitedTime)
                File.WriteAllText("waited.txt", "00:00:00");
            if (CreateEstimatedTime)
            {
                File.WriteAllText("estimatedTime.txt", "预计时间: 0 分钟");
                File.WriteAllText("estimatedTimestamp.txt", "");
            }
            if (CreateOnDeck)
                File.WriteAllText("ondeck.txt", "等待中...");
            if (CreateOnDeck2)
                File.WriteAllText("ondeck2.txt", "队列为空！");
            if (CreateUserList)
                File.WriteAllText("users.txt", "无");
            if (CreateUsersInQueue)
                File.WriteAllText("queuecount.txt", "队列中的用户: 0");
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    public void StartEnterCode(PokeRoutineExecutorBase b)
    {
        if (!CreateAssets)
            return;

        try
        {
            var file = GetBlockFileName(b);
            if (CopyImageFile && File.Exists(TradeBlockFile))
                File.Copy(TradeBlockFile, file);
            else
                File.WriteAllBytes(file, BlackPixel);
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    // Completed Trades
    public void StartTrade<T>(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail, PokeTradeHub<T> hub) where T : PKM, new()
    {
        if (!CreateAssets)
            return;

        try
        {
            if (CreateTradeStart)
                GenerateBotConnection(b, detail);
            if (CreateWaitedTime)
                GenerateWaitedTime(detail.Time);
            if (CreateEstimatedTime)
                GenerateEstimatedTime(hub);
            if (CreateUsersInQueue)
                GenerateUsersInQueue(hub.Queues.Info.Count);
            if (CreateOnDeck)
                GenerateOnDeck(hub);
            if (CreateOnDeck2)
                GenerateOnDeck2(hub);
            if (CreateUserList)
                GenerateUserList(hub);
            if (CreateCompletedTrades)
                GenerateCompletedTrades(hub);
            if (CreateTradeStartSprite)
                GenerateBotSprite(b, detail);
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    public override string ToString() => "直播设置";

    private static void GenerateBotSprite<T>(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail) where T : PKM, new()
    {
        var func = CreateSpriteFile;
        if (func == null)
            return;
        var file = b.Connection.Name;
        var pk = detail.TradeData;
        func.Invoke(pk, $"sprite_{file}.png");
    }

    private void GenerateBotConnection<T>(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail) where T : PKM, new()
    {
        var file = b.Connection.Name;
        var name = string.Format(TrainerTradeStart, detail.ID, detail.Trainer.TrainerName, (Species)detail.TradeData.Species);
        File.WriteAllText($"{file}.txt", name);
    }

    private void GenerateCompletedTrades<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var msg = string.Format(CompletedTradesFormat, hub.Config.Trade.CountStatsSettings.CompletedTrades);
        File.WriteAllText("completed.txt", msg);
    }

    private void GenerateEstimatedTime<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var count = hub.Queues.Info.Count;
        var estimate = hub.Config.Queues.EstimateDelay(count, hub.Bots.Count);

        // Minutes
        var wait = string.Format(EstimatedTimeFormat, estimate);
        File.WriteAllText("estimatedTime.txt", wait);

        // Expected to be fulfilled at this time
        var now = DateTime.Now;
        var difference = now.AddMinutes(estimate);
        var date = difference.ToString(EstimatedFulfillmentFormat);
        File.WriteAllText("estimatedTimestamp.txt", date);
    }

    private void GenerateOnDeck<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var ondeck = hub.Queues.Info.GetUserList(OnDeckFormat);
        ondeck = ondeck.Skip(OnDeckSkip).Take(OnDeckTake); // filter down
        File.WriteAllText("ondeck.txt", string.Join(OnDeckSeparator, ondeck));
    }

    private void GenerateOnDeck2<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var ondeck = hub.Queues.Info.GetUserList(OnDeckFormat2);
        ondeck = ondeck.Skip(OnDeckSkip2).Take(OnDeckTake2); // filter down
        File.WriteAllText("ondeck2.txt", string.Join(OnDeckSeparator2, ondeck));
    }

    private void GenerateUserList<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var users = hub.Queues.Info.GetUserList(UserListFormat);
        users = users.Skip(UserListSkip);
        if (UserListTake > 0)
            users = users.Take(UserListTake); // filter down
        File.WriteAllText("users.txt", string.Join(UserListSeparator, users));
    }

    private void GenerateUsersInQueue(int count)
    {
        var value = string.Format(UsersInQueueFormat, count);
        File.WriteAllText("queuecount.txt", value);
    }

    private void GenerateWaitedTime(DateTime time)
    {
        var now = DateTime.Now;
        var difference = now - time;
        var value = difference.ToString(WaitedTimeFormat);
        File.WriteAllText("waited.txt", value);
    }

    private string GetBlockFileName(PokeRoutineExecutorBase b) => string.Format(TradeBlockFormat, b.Connection.Name);
}
