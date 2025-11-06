using System;
using System.ComponentModel;
using System.Globalization;

namespace SysBot.Pokemon;

/// <summary>
/// 本地化的ExpandableObjectConverter，用于本地化嵌套对象的显示名称
/// </summary>
public class LocalizedExpandableObjectConverter : ExpandableObjectConverter
{
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value != null)
        {
            // 获取对象的类型名称
            var typeName = value.GetType().Name;
            
            // 尝试从资源文件中获取本地化的显示名称
            var displayName = ConfigLocalizationHelper.GetDisplayName(typeName, value.ToString() ?? typeName);
            
            return displayName;
        }
        
        return base.ConvertTo(context, culture, value, destinationType);
    }
}

