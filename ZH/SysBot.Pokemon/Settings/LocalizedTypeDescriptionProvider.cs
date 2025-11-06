using System;
using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// 本地化的TypeDescriptionProvider
/// </summary>
public class LocalizedTypeDescriptionProvider : TypeDescriptionProvider
{
    private readonly TypeDescriptionProvider _baseProvider;

    public LocalizedTypeDescriptionProvider(Type type)
        : base(TypeDescriptor.GetProvider(type))
    {
        _baseProvider = TypeDescriptor.GetProvider(type);
    }

    public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object? instance)
    {
        var baseDescriptor = _baseProvider.GetTypeDescriptor(objectType, instance);
        if (baseDescriptor == null)
            throw new InvalidOperationException($"Could not get type descriptor for {objectType}");
        return new LocalizedTypeDescriptor(baseDescriptor, objectType);
    }
}

/// <summary>
/// 本地化的TypeDescriptor
/// </summary>
public class LocalizedTypeDescriptor : ICustomTypeDescriptor
{
    private readonly ICustomTypeDescriptor _baseDescriptor;
    private readonly Type _objectType;

    public LocalizedTypeDescriptor(ICustomTypeDescriptor baseDescriptor, Type objectType)
    {
        _baseDescriptor = baseDescriptor;
        _objectType = objectType;
    }

    public AttributeCollection GetAttributes() => _baseDescriptor.GetAttributes();
    public string? GetClassName() => _baseDescriptor.GetClassName();
    public string? GetComponentName() => _baseDescriptor.GetComponentName();
    public TypeConverter? GetConverter() => _baseDescriptor.GetConverter();
    public EventDescriptor? GetDefaultEvent() => _baseDescriptor.GetDefaultEvent();
    public PropertyDescriptor? GetDefaultProperty() => _baseDescriptor.GetDefaultProperty();
    public object? GetEditor(Type editorBaseType) => _baseDescriptor.GetEditor(editorBaseType);
    public EventDescriptorCollection GetEvents() => _baseDescriptor.GetEvents();
    public EventDescriptorCollection GetEvents(Attribute[]? attributes) => _baseDescriptor.GetEvents(attributes);

    public PropertyDescriptorCollection GetProperties()
    {
        return GetProperties(null);
    }

    public PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var baseProperties = _baseDescriptor.GetProperties(attributes);
        var localizedProperties = new PropertyDescriptor[baseProperties.Count];

        // 构建类型名称键，如果是嵌套类，包含父类名称
        string typeNameKey = GetTypeNameKey(_objectType);

        for (int i = 0; i < baseProperties.Count; i++)
        {
            var baseProperty = baseProperties[i];
            
            // 获取Category和Description的Attribute
            var categoryAttr = baseProperty.Attributes[typeof(CategoryAttribute)] as CategoryAttribute;
            var descAttr = baseProperty.Attributes[typeof(DescriptionAttribute)] as DescriptionAttribute;
            var displayNameAttr = baseProperty.Attributes[typeof(DisplayNameAttribute)] as DisplayNameAttribute;

            string categoryKey = categoryAttr?.Category ?? string.Empty;
            
            // 使用属性的声明类型（ComponentType）而不是实例类型，以正确处理继承的属性
            var propertyType = baseProperty.ComponentType;
            string propertyTypeKey = propertyType != null ? GetTypeNameKey(propertyType) : typeNameKey;
            
            string descriptionKey = $"{propertyTypeKey}_{baseProperty.Name}";
            string displayNameKey = $"{propertyTypeKey}_{baseProperty.Name}";

            localizedProperties[i] = new LocalizedPropertyDescriptor(
                baseProperty,
                categoryKey,
                descriptionKey,
                displayNameKey);
        }

        return new PropertyDescriptorCollection(localizedProperties);
    }

    private static string GetTypeNameKey(Type type)
    {
        // 如果是嵌套类，构建包含父类名称的键
        if (type.DeclaringType != null)
        {
            return $"{type.DeclaringType.Name}_{type.Name}";
        }
        return type.Name;
    }

    public object? GetPropertyOwner(PropertyDescriptor? pd) => _baseDescriptor.GetPropertyOwner(pd);
}
