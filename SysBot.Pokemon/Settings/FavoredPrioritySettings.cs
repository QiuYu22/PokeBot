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
    private const string Operation = "操作";

    private int _skipPercentage = 50;
    private int _minimumRegularUsersFirst = 3;

    [Category(Operation), Description("启用或禁用优先用户功能。禁用时，所有用户将被平等对待。"), DisplayName("启用优先用户")]
    public bool EnableFavoritism { get; set; } = true;

    [Category(Configure), Description("优先用户可以跳过的普通用户百分比（0-100）。例如：50% 表示优先用户将插入到队列中普通用户的中间位置。百分比越高，对优先用户越有利。"), DisplayName("跳过百分比")]
    public int SkipPercentage
    {
        get => _skipPercentage;
        set => _skipPercentage = Math.Clamp(value, MinSkipPercentage, MaxSkipPercentage);
    }

    [Category(Configure), Description("在任何优先用户可以插队之前必须处理的最少普通用户数量。这可以防止优先用户完全阻塞普通用户，即使在大型队列中也是如此。"), DisplayName("最少优先普通用户数")]
    public int MinimumRegularUsersFirst
    {
        get => _minimumRegularUsersFirst;
        set => _minimumRegularUsersFirst = Math.Max(MinRegularUsers, value);
    }

    public override string ToString() => "优先用户设置";
}
