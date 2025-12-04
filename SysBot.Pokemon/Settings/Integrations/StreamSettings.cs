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
    private const string Operation = nameof(Operation);

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

    [Category(Operation), DisplayName("完成交易格式"), Description("显示已完成交易数量的格式。{0} = 次数")]
    public string CompletedTradesFormat { get; set; } = "Completed Trades: {0}";

    [Category(Operation), DisplayName("复制图像文件"), Description("若存在 TradeBlockFile 则复制该图像，否则复制占位图像。")]
    public bool CopyImageFile { get; set; } = true;

    [Category(Operation), DisplayName("生成资源"), Description("生成直播资源；关闭后将不会生成任何资产。")]
    public bool CreateAssets { get; set; }

    [Category(Operation), DisplayName("生成完成交易文件"), Description("在新交易开始时写入已完成交易数量文件。")]
    public bool CreateCompletedTrades { get; set; } = true;

    [Category(Operation), DisplayName("生成预计等待文件"), Description("生成一个文件，列出加入队列后预计需要等待的时间。")]
    public bool CreateEstimatedTime { get; set; } = true;

    [Category(Operation), DisplayName("生成候场列表"), Description("生成当前候场用户列表文件。")]
    public bool CreateOnDeck { get; set; } = true;

    [Category(Operation), DisplayName("生成候场列表 #2"), Description("生成候场列表 #2 的用户列表文件。")]
    public bool CreateOnDeck2 { get; set; } = true;

    [Category(Operation), DisplayName("生成交易开始详情(对象)"), Description("生成包含机器人正在与谁交易的详情文件。")]
    public bool CreateTradeStart { get; set; } = true;

    [Category(Operation), DisplayName("生成交易开始详情(精灵)"), Description("生成包含机器人正在交易何种宝可梦的详情文件。")]
    public bool CreateTradeStartSprite { get; set; } = true;

    [Category(Operation), DisplayName("生成交易中列表"), Description("生成当前正在交易的用户列表文件。")]
    public bool CreateUserList { get; set; } = true;

    [Category(Operation), DisplayName("生成排队人数文件"), Description("生成一个文件，表示当前队列中的用户数量。")]
    public bool CreateUsersInQueue { get; set; } = true;

    [Category(Operation), DisplayName("生成等待时间文件"), Description("生成一个文件，记录最近出队用户所等待的时间。")]
    public bool CreateWaitedTime { get; set; } = true;

    [Category(Operation), DisplayName("预计完成时间格式"), Description("显示预计完成（时间戳）的格式。")]
    public string EstimatedFulfillmentFormat { get; set; } = @"hh\:mm\:ss";

    // Estimated Time
    [Category(Operation), DisplayName("预计等待时间格式"), Description("显示预计等待时间的格式。")]
    public string EstimatedTimeFormat { get; set; } = "Estimated time: {0:F1} minutes";

    [Category(Operation), DisplayName("候场列表格式"), Description("显示候场列表用户的格式。{0} = ID，{3} = 用户")]
    public string OnDeckFormat { get; set; } = "(ID {0}) - {3}";

    [Category(Operation), DisplayName("候场列表 #2 格式"), Description("显示候场 #2 列表用户的格式。{0} = ID，{3} = 用户")]
    public string OnDeckFormat2 { get; set; } = "(ID {0}) - {3}";

    [Category(Operation), DisplayName("候场列表分隔符"), Description("候场列表用户之间使用的分隔符。")]
    public string OnDeckSeparator { get; set; } = "\n";

    [Category(Operation), DisplayName("候场列表 #2 分隔符"), Description("候场 #2 列表用户之间使用的分隔符。")]
    public string OnDeckSeparator2 { get; set; } = "\n";

    [Category(Operation), DisplayName("候场跳过人数"), Description("候场列表顶部需要跳过的用户数量；若想隐藏当前处理中用户，可设置为主机数量。")]
    public int OnDeckSkip { get; set; }

    [Category(Operation), DisplayName("候场 #2 跳过人数"), Description("候场 #2 列表顶部需要跳过的用户数量；若想隐藏处理中用户，可设置为主机数量。")]
    public int OnDeckSkip2 { get; set; }

    // On Deck
    [Category(Operation), DisplayName("候场显示人数"), Description("候场列表中要显示的用户数量。")]
    public int OnDeckTake { get; set; } = 5;

    // On Deck 2
    [Category(Operation), DisplayName("候场 #2 显示人数"), Description("候场 #2 列表中要显示的用户数量。")]
    public int OnDeckTake2 { get; set; } = 5;

    // TradeCodeBlock
    [Category(Operation), DisplayName("阻挡图源文件"), Description("输入联机代码时要复制的图像源文件名；为空时将创建占位图像。")]
    public string TradeBlockFile { get; set; } = string.Empty;

    [Category(Operation), DisplayName("阻挡图目标格式"), Description("联机代码阻挡图的目标文件名。{0} 会被本地 IP 地址替换。")]
    public string TradeBlockFormat { get; set; } = "block_{0}.png";

    [Category(Operation), DisplayName("交易开始（用户）格式"), Description("显示“正在交易”详情的格式。{0} = ID，{1} = 用户。")]
    public string TrainerTradeStart { get; set; } = "(ID {0}) {1}";

    [Category(Operation), DisplayName("用户列表格式"), Description("显示列表用户的格式。{0} = ID，{3} = 用户。")]
    public string UserListFormat { get; set; } = "(ID {0}) - {3}";

    [Category(Operation), DisplayName("用户列表分隔符"), Description("列表用户之间使用的分隔符。")]
    public string UserListSeparator { get; set; } = ", ";

    [Category(Operation), DisplayName("用户列表跳过人数"), Description("用户列表顶部需要跳过的数量；若想隐藏处理中用户，可设置为主机数量。")]
    public int UserListSkip { get; set; }

    // User List
    [Category(Operation), DisplayName("用户列表显示人数"), Description("用户列表中要显示的数量。-1 表示全部显示。")]
    public int UserListTake { get; set; } = -1;

    // Users in Queue
    [Category(Operation), DisplayName("排队人数格式"), Description("显示队列人数的格式。{0} = 数量。")]
    public string UsersInQueueFormat { get; set; } = "Users in Queue: {0}";

    // Waited Time
    [Category(Operation), DisplayName("等待时间格式"), Description("显示最近出队用户等待时间的格式。")]
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
                File.WriteAllText("estimatedTime.txt", "Estimated time: 0 minutes");
                File.WriteAllText("estimatedTimestamp.txt", "");
            }
            if (CreateOnDeck)
                File.WriteAllText("ondeck.txt", "Waiting...");
            if (CreateOnDeck2)
                File.WriteAllText("ondeck2.txt", "Queue is empty!");
            if (CreateUserList)
                File.WriteAllText("users.txt", "None");
            if (CreateUsersInQueue)
                File.WriteAllText("queuecount.txt", "Users in Queue: 0");
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
