using System;
using System.Globalization;
using System.Resources;

namespace SysBot.Pokemon;

/// <summary>
/// 配置项本地化辅助类
/// </summary>
public static class ConfigLocalizationHelper
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo _currentCulture = CultureInfo.CurrentCulture;

    /// <summary>
    /// 初始化配置本地化系统
    /// </summary>
    public static void Initialize()
    {
        try
        {
            _resourceManager = new ResourceManager("SysBot.Pokemon.Settings.ConfigStrings", typeof(ConfigLocalizationHelper).Assembly);
            
            // 检测系统语言
            var systemLang = CultureInfo.CurrentUICulture.Name;
            if (systemLang.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                _currentCulture = new CultureInfo("zh-CN");
            }
            else
            {
                _currentCulture = new CultureInfo("en-US");
            }
        }
        catch
        {
            _currentCulture = CultureInfo.CurrentCulture;
        }
    }

    /// <summary>
    /// 获取本地化的Category名称
    /// </summary>
    public static string GetCategoryName(string categoryKey, string defaultValue)
    {
        if (_resourceManager == null)
            Initialize();

        try
        {
            var key = $"Category_{categoryKey}";
            var value = _resourceManager?.GetString(key, _currentCulture);
            return value ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 获取本地化的Description
    /// </summary>
    public static string GetDescription(string propertyKey, string defaultValue)
    {
        if (_resourceManager == null)
            Initialize();

        try
        {
            var key = $"Desc_{propertyKey}";
            var value = _resourceManager?.GetString(key, _currentCulture);
            return value ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 获取本地化的DisplayName
    /// </summary>
    public static string GetDisplayName(string propertyKey, string defaultValue)
    {
        if (_resourceManager == null)
            Initialize();

        try
        {
            var key = $"Display_{propertyKey}";
            var value = _resourceManager?.GetString(key, _currentCulture);
            return value ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}
