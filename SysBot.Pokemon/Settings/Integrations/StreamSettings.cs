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
    private const string Operation = "运行";

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

    [DisplayName("完成交易格式")]
    [Category(Operation), Description("显示已完成交易数量的格式，{0} 表示数量。")]
    public string CompletedTradesFormat { get; set; } = "已完成交易：{0}";

    [DisplayName("复制遮挡图片")]
    [Category(Operation), Description("若存在 TradeBlockFile 则复制该图像，否则复制占位图。")]
    public bool CopyImageFile { get; set; } = true;

    [DisplayName("生成直播素材")]
    [Category(Operation), Description("生成直播素材；关闭后将不再生成。")]
    public bool CreateAssets { get; set; }

    [DisplayName("生成完成交易文件")]
    [Category(Operation), Description("每当开始新交易时生成记录已完成交易数的文件。")]
    public bool CreateCompletedTrades { get; set; } = true;

    [DisplayName("生成预计等待文件")]
    [Category(Operation), Description("生成估算加入队列后等待时间的文件。")]
    public bool CreateEstimatedTime { get; set; } = true;

    [DisplayName("生成候场列表")]
    [Category(Operation), Description("生成当前候场（On-Deck）人员列表。")]
    public bool CreateOnDeck { get; set; } = true;

    [DisplayName("生成候场列表 #2")]
    [Category(Operation), Description("生成候场列表 #2。")]
    public bool CreateOnDeck2 { get; set; } = true;

    [DisplayName("生成交易开始信息")]
    [Category(Operation), Description("生成交易开始信息，标注机器人正在与谁交易。")]
    public bool CreateTradeStart { get; set; } = true;

    [DisplayName("生成交易宝可梦信息")]
    [Category(Operation), Description("生成交易开始信息，标注机器人所交易的宝可梦。")]
    public bool CreateTradeStartSprite { get; set; } = true;

    [DisplayName("生成交易中列表")]
    [Category(Operation), Description("生成当前正在交易的人员列表。")]
    public bool CreateUserList { get; set; } = true;

    [DisplayName("生成队列人数文件")]
    [Category(Operation), Description("生成队列人数统计文件。")]
    public bool CreateUsersInQueue { get; set; } = true;

    [DisplayName("生成等待时长文件")]
    [Category(Operation), Description("记录最近出队用户的等待时长。")]
    public bool CreateWaitedTime { get; set; } = true;

    [DisplayName("预计完成时间格式")]
    [Category(Operation), Description("显示预计完成时间戳的格式。")]
    public string EstimatedFulfillmentFormat { get; set; } = @"hh\:mm\:ss";

    // Estimated Time
    [DisplayName("预计等待时间格式")]
    [Category(Operation), Description("显示预计等待时长的格式。")]
    public string EstimatedTimeFormat { get; set; } = "预计等待：{0:F1} 分钟";

    [DisplayName("候场列表格式")]
    [Category(Operation), Description("候场列表的显示格式，{0}=ID，{3}=用户名。")]
    public string OnDeckFormat { get; set; } = "(ID {0}) - {3}";

    [DisplayName("候场列表 #2 格式")]
    [Category(Operation), Description("候场列表 #2 的显示格式，{0}=ID，{3}=用户名。")]
    public string OnDeckFormat2 { get; set; } = "(ID {0}) - {3}";

    [DisplayName("候场列表分隔符")]
    [Category(Operation), Description("候场列表的分隔符。")]
    public string OnDeckSeparator { get; set; } = "\n";

    [DisplayName("候场列表 #2 分隔符")]
    [Category(Operation), Description("候场列表 #2 的分隔符。")]
    public string OnDeckSeparator2 { get; set; } = "\n";

    [DisplayName("候场列表跳过人数")]
    [Category(Operation), Description("候场列表开头跳过的人数；可用于隐藏正在处理的用户。")]
    public int OnDeckSkip { get; set; }

    [DisplayName("候场列表 #2 跳过人数")]
    [Category(Operation), Description("候场列表 #2 开头跳过的人数。")]
    public int OnDeckSkip2 { get; set; }

    // On Deck
    [DisplayName("候场列表显示数量")]
    [Category(Operation), Description("候场列表显示的用户数量。")]
    public int OnDeckTake { get; set; } = 5;

    // On Deck 2
    [DisplayName("候场列表 #2 显示数量")]
    [Category(Operation), Description("候场列表 #2 显示的用户数量。")]
    public int OnDeckTake2 { get; set; } = 5;

    // TradeCodeBlock
    [DisplayName("遮挡图片源文件")]
    [Category(Operation), Description("输入交易密码时复制的源图像文件；为空则生成占位图。")]
    public string TradeBlockFile { get; set; } = string.Empty;

    [DisplayName("遮挡图片目标格式")]
    [Category(Operation), Description("遮挡联机密码图像的目标文件名，{0} 会替换为本地 IP。")]
    public string TradeBlockFormat { get; set; } = "block_{0}.png";

    [DisplayName("交易信息格式")]
    [Category(Operation), Description("“正在交易”信息的显示格式，{0}=ID，{1}=用户名。")]
    public string TrainerTradeStart { get; set; } = "(ID {0}) {1}";

    [DisplayName("用户列表格式")]
    [Category(Operation), Description("用户列表的显示格式，{0}=ID，{3}=用户名。")]
    public string UserListFormat { get; set; } = "(ID {0}) - {3}";

    [DisplayName("用户列表分隔符")]
    [Category(Operation), Description("用户列表的分隔符。")]
    public string UserListSeparator { get; set; } = ", ";

    [DisplayName("用户列表跳过数量")]
    [Category(Operation), Description("用户列表开头跳过的数量。")]
    public int UserListSkip { get; set; }

    // User List
    [DisplayName("用户列表显示数量")]
    [Category(Operation), Description("用户列表显示的数量，上限 -1 表示全部。")]
    public int UserListTake { get; set; } = -1;

    // Users in Queue
    [DisplayName("队列人数格式")]
    [Category(Operation), Description("队列人数的显示格式，{0}=人数。")]
    public string UsersInQueueFormat { get; set; } = "队列人数：{0}";

    // Waited Time
    [DisplayName("等待时长格式")]
    [Category(Operation), Description("最近出队用户等待时间的显示格式。")]
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
                File.WriteAllText("estimatedTime.txt", "预计等待：0 分钟");
                File.WriteAllText("estimatedTimestamp.txt", "");
            }
            if (CreateOnDeck)
                File.WriteAllText("ondeck.txt", "等待中…");
            if (CreateOnDeck2)
                File.WriteAllText("ondeck2.txt", "队列为空！");
            if (CreateUserList)
                File.WriteAllText("users.txt", "暂无用户");
            if (CreateUsersInQueue)
                File.WriteAllText("queuecount.txt", "队列人数：0");
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

    public override string ToString() => "直播素材设置";

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
