using System.ComponentModel;
using System.IO;

namespace SysBot.Pokemon;

public class FolderSettings : IDumper
{
    private const string FeatureToggle = "功能开关";

    private const string Files = "文件";

    [Category(Files), DisplayName("派发源目录"), Description("派发 PKM 时选取的源目录。")]
    public string DistributeFolder { get; set; } = string.Empty;

    [Category(FeatureToggle), DisplayName("启用转储"), Description("启用后将收到的 PKM（交易结果）保存到转储目录。")]
    public bool Dump { get; set; }

    [Category(Files), DisplayName("转储目标目录"), Description("存放所有收到的 PKM 文件的目录。")]
    public string DumpFolder { get; set; } = string.Empty;

    public void CreateDefaults(string path)
    {
        var dump = Path.Combine(path, "dump");
        Directory.CreateDirectory(dump);
        DumpFolder = dump;
        Dump = true;

        var distribute = Path.Combine(path, "distribute");
        Directory.CreateDirectory(distribute);
        DistributeFolder = distribute;
    }

    public override string ToString() => "文件/转储设置";
}
