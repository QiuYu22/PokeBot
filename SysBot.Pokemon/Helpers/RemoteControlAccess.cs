using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

[TypeConverter(typeof(ExpandableObjectConverter))]
public class RemoteControlAccess
{
    [DisplayName("备注"), Description("用于标记此 ID 的说明信息。")]
    public string Comment { get; set; } = string.Empty;

    [DisplayName("ID"), Description("Discord 用户 / 频道 / 角色的雪花 ID。")]
    public ulong ID { get; set; }

    [DisplayName("名称"), Description("该 ID 对应的可读名称，便于识别。")]
    public string Name { get; set; } = string.Empty;

    public override string ToString() => $"{Name} = {ID} // {Comment}";
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public class RemoteControlAccessList
{
    /// <summary>
    /// Allows Bot-Specific role lists to permit anyone to use the bot if there are no roles specified.
    /// </summary>
    /// <remarks>
    /// Also used by Channel White-lists. No channels whitelisted &amp; true => any channel can be used for bot commands.
    /// </remarks>
    [DisplayName("若为空则允许所有人"), Description("当列表为空时，若开启该选项，则允许所有人（或所有频道）使用。")]
    public bool AllowIfEmpty { get; set; } = true;

    /// <summary>
    /// Don't mutate this list; use <see cref="AddIfNew"/> and <see cref="RemoveAll"/>.
    /// This is public for serialization purposes.
    /// </summary>
    [DisplayName("访问列表"), Description("允许使用的用户、频道或角色集合。")]
    public List<RemoteControlAccess> List { get; set; } = [];

    /// <summary>
    /// Adds new items if not already present by <see cref="RemoteControlAccess.ID"/>.
    /// </summary>
    /// <param name="list">List of items to add</param>
    public void AddIfNew(IEnumerable<RemoteControlAccess> list)
    {
        foreach (var item in list)
        {
            if (!Contains(item.ID))
                List.Add(item);
        }
    }

    public void Clear() => List.Clear();

    /// <summary> Checks if any item has the requested <see cref="id"/> property. </summary>
    /// <returns>True if any item matches the <see cref="id"/></returns>
    /// <remarks>Used for unique IDs such as Users or Channels</remarks>
    public bool Contains(ulong id) => List.Any(z => z.ID == id);

    /// <summary> Checks if any item has the requested <see cref="name"/> property. </summary>
    /// <returns>True if any item matches the <see cref="name"/></returns>
    /// <remarks>Used for checking Role Names</remarks>
    public bool Contains(string name) => List.Any(z => z.Name == name);

    /// <summary>
    /// Used with foreach iterating. You shouldn't be calling this manually.
    /// </summary>
    public IEnumerator<RemoteControlAccess> GetEnumerator() => List.GetEnumerator();

    public int RemoveAll(Predicate<RemoteControlAccess> item) => List.RemoveAll(item);

    /// <summary>
    /// Gets an enumerable summary of the items in this object.
    /// </summary>
    public IEnumerable<string> Summarize() => List.Select(z => z.ToString());

    /// <summary>
    /// Used for summarizing this object in a PropertyGrid.
    /// </summary>
    public override string ToString()
    {
        return List.Count == 0
            ? (AllowIfEmpty ? "允许所有人" : "不允许（未指定）。")
            : $"已指定 {List.Count} 条记录。";
    }
}
