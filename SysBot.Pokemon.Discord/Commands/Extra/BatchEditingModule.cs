using Discord;
using Discord.Commands;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

// ReSharper disable once UnusedType.Global
public class BatchEditingModule : ModuleBase<SocketCommandContext>
{
    [Command("batchInfo"), Alias("bei")]
    [Summary("尝试获取请求属性的信息。")]
    public async Task GetBatchInfo(string propertyName)
    {
        if (BatchEditing.TryGetPropertyType(propertyName, out string? result))
            await ReplyAsync($"{propertyName}: {result}").ConfigureAwait(false);
        else
            await ReplyAsync($"无法找到 {propertyName} 的信息。").ConfigureAwait(false);
    }

    [Command("batchValidate"), Alias("bev")]
    [Summary("验证批量编辑指令的有效性。")]
    public async Task ValidateBatchInfo(string instructions)
    {
        bool valid = IsValidInstructionSet(instructions, out var invalid);

        if (!valid)
        {
            var msg = invalid.Select(z => $"{z.PropertyName}, {z.PropertyValue}");
            await ReplyAsync($"检测到无效行:\r\n{Format.Code(string.Join(Environment.NewLine, msg))}")
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyAsync($"{invalid.Count} 行无效。").ConfigureAwait(false);
        }
    }

    private static bool IsValidInstructionSet(ReadOnlySpan<char> split, out List<StringInstruction> invalid)
    {
        invalid = [];
        var set = new StringInstructionSet(split);
        foreach (var s in set.Filters.Concat(set.Instructions))
        {
            if (!BatchEditing.TryGetPropertyType(s.PropertyName, out string? _))
                invalid.Add(s);
        }
        return invalid.Count == 0;
    }
}
