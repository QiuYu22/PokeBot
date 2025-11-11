using System;
using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// Settings for priority user favoritism in the trade queue.
/// Priority users can skip ahead of regular users while ensuring regular users still get processed.
/// </summary>
public class FavoredPrioritySettings : IFavoredCPQSetting
{
    private const int MinSkipPercentage = 0;
    private const int MaxSkipPercentage = 100;
    private const int MinRegularUsers = 0;

    private const string Configure = "配置";
    private const string Operation = "运行";

    private int _skipPercentage = 50;
    private int _minimumRegularUsersFirst = 3;

    [DisplayName("启用优先队列"), Category(Operation), Description("启用后，优先用户可以跳过部分普通用户；关闭则所有用户一视同仁。")]
    public bool EnableFavoritism { get; set; } = true;

    [DisplayName("跳过比例"), Category(Configure), Description("优先用户可跳过的普通用户百分比（0-100）。例如 50% 表示优先用户会插入到队列中间；数值越高越偏向优先用户。")]
    public int SkipPercentage
    {
        get => _skipPercentage;
        set => _skipPercentage = Math.Clamp(value, MinSkipPercentage, MaxSkipPercentage);
    }

    [DisplayName("最少普通用户数"), Category(Configure), Description("在允许优先用户插队前必须处理的普通用户数量，防止优先用户长期占用队列。")]
    public int MinimumRegularUsersFirst
    {
        get => _minimumRegularUsersFirst;
        set => _minimumRegularUsersFirst = Math.Max(MinRegularUsers, value);
    }

    public override string ToString() => "优先级设置";
}
