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

    [Category(Operation), DisplayName("启用优先特权"), Description("启用后优先用户可插队；禁用时所有用户一视同仁。")]
    public bool EnableFavoritism { get; set; } = true;

    [Category(Configure), DisplayName("跳过比例 (0-100)"), Description("优先用户可跳过的普通用户比例，例如 50% 表示优先用户插入队伍中段。比例越高越偏向优先用户。")]
    public int SkipPercentage
    {
        get => _skipPercentage;
        set => _skipPercentage = Math.Clamp(value, MinSkipPercentage, MaxSkipPercentage);
    }

    [Category(Configure), DisplayName("先处理的普通用户数"), Description("在允许任何优先用户插队前，必须处理的普通用户数量，防止优先用户占满队列。")]
    public int MinimumRegularUsersFirst
    {
        get => _minimumRegularUsersFirst;
        set => _minimumRegularUsersFirst = Math.Max(MinRegularUsers, value);
    }

    public override string ToString() => "优先队列设置";
}
