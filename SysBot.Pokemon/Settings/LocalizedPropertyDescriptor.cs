using System;
using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// 本地化的PropertyDescriptor包装器
/// </summary>
public class LocalizedPropertyDescriptor : PropertyDescriptor
{
    private readonly PropertyDescriptor _baseDescriptor;
    private readonly string _categoryKey;
    private readonly string _descriptionKey;
    private readonly string _displayNameKey;

    public LocalizedPropertyDescriptor(PropertyDescriptor baseDescriptor, string categoryKey, string descriptionKey, string displayNameKey)
        : base(baseDescriptor)
    {
        _baseDescriptor = baseDescriptor;
        _categoryKey = categoryKey;
        _descriptionKey = descriptionKey;
        _displayNameKey = displayNameKey;
    }

    public override string Category
    {
        get
        {
            if (!string.IsNullOrEmpty(_categoryKey))
            {
                var localized = ConfigLocalizationHelper.GetCategoryName(_categoryKey, _baseDescriptor.Category);
                return localized;
            }
            return _baseDescriptor.Category;
        }
    }

    public override string Description
    {
        get
        {
            if (!string.IsNullOrEmpty(_descriptionKey))
            {
                var localized = ConfigLocalizationHelper.GetDescription(_descriptionKey, _baseDescriptor.Description);
                return localized;
            }
            return _baseDescriptor.Description;
        }
    }

    public override string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(_displayNameKey))
            {
                var localized = ConfigLocalizationHelper.GetDisplayName(_displayNameKey, _baseDescriptor.DisplayName);
                return localized;
            }
            return _baseDescriptor.DisplayName;
        }
    }

    public override Type ComponentType => _baseDescriptor.ComponentType;
    public override bool IsReadOnly => _baseDescriptor.IsReadOnly;
    public override Type PropertyType => _baseDescriptor.PropertyType;

    public override bool CanResetValue(object component) => _baseDescriptor.CanResetValue(component);
    public override object? GetValue(object? component) => _baseDescriptor.GetValue(component);
    public override void ResetValue(object component) => _baseDescriptor.ResetValue(component);
    public override void SetValue(object? component, object? value) => _baseDescriptor.SetValue(component, value);
    public override bool ShouldSerializeValue(object component) => _baseDescriptor.ShouldSerializeValue(component);
}
