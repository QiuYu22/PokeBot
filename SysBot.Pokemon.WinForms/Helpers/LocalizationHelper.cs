using System;
using System.Globalization;
using System.Resources;
using System.Threading;
using System.Windows.Forms;

namespace SysBot.Pokemon.WinForms.Helpers;

/// <summary>
/// 本地化辅助类，用于管理应用程序的语言资源
/// </summary>
public static class LocalizationHelper
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo _currentCulture = CultureInfo.CurrentCulture;

    /// <summary>
    /// 初始化本地化系统
    /// </summary>
    public static void Initialize()
    {
        // 设置默认资源管理器
        _resourceManager = new ResourceManager("SysBot.Pokemon.WinForms.Properties.Strings", typeof(LocalizationHelper).Assembly);
        
        // 尝试从配置文件加载语言设置
        LoadLanguageFromConfig();
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="defaultValue">默认值（如果找不到资源）</param>
    /// <returns>本地化字符串</returns>
    public static string GetString(string key, string? defaultValue = null)
    {
        if (_resourceManager == null)
            Initialize();

        try
        {
            var value = _resourceManager?.GetString(key, _currentCulture);
            return value ?? defaultValue ?? key;
        }
        catch
        {
            return defaultValue ?? key;
        }
    }

    /// <summary>
    /// 获取格式化的本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="args">格式化参数</param>
    /// <returns>格式化后的本地化字符串</returns>
    public static string GetFormattedString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }

    /// <summary>
    /// 设置当前语言
    /// </summary>
    /// <param name="cultureName">文化名称（如 "zh-CN", "en-US"）</param>
    public static void SetLanguage(string cultureName)
    {
        try
        {
            _currentCulture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = _currentCulture;
            Thread.CurrentThread.CurrentUICulture = _currentCulture;
            
            // 保存到配置文件
            SaveLanguageToConfig(cultureName);
        }
        catch
        {
            // 如果设置失败，使用默认文化
            _currentCulture = CultureInfo.CurrentCulture;
        }
    }

    /// <summary>
    /// 获取当前语言代码
    /// </summary>
    public static string GetCurrentLanguage()
    {
        return _currentCulture.Name;
    }

    /// <summary>
    /// 检查是否为中文
    /// </summary>
    public static bool IsChinese()
    {
        return _currentCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从配置文件加载语言设置
    /// </summary>
    private static void LoadLanguageFromConfig()
    {
        try
        {
            // 检测系统语言并自动设置
            var systemLang = CultureInfo.CurrentUICulture.Name;
            if (systemLang.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                SetLanguage("zh-CN");
            }
            else
            {
                SetLanguage("en-US");
            }
        }
        catch
        {
            // 使用系统默认
            var systemLang = CultureInfo.CurrentUICulture.Name;
            if (systemLang.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                _currentCulture = new CultureInfo("zh-CN");
            }
            else
            {
                _currentCulture = new CultureInfo("en-US");
            }
            Thread.CurrentThread.CurrentCulture = _currentCulture;
            Thread.CurrentThread.CurrentUICulture = _currentCulture;
        }
    }

    /// <summary>
    /// 保存语言设置到配置文件
    /// </summary>
    private static void SaveLanguageToConfig(string cultureName)
    {
        try
        {
            // 这里可以添加保存到配置文件的逻辑
            // 暂时只保存在内存中
        }
        catch
        {
            // 忽略保存错误
        }
    }
}
