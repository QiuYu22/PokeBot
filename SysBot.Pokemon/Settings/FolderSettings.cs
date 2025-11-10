using System.ComponentModel;
using System.IO;

namespace SysBot.Pokemon;

public class FolderSettings : IDumper
{
    private const string FeatureToggle = "功能开关";

    private const string Files = "文件路径";

    [DisplayName("分发来源目录")]
    [Category(Files), Description("分发来源目录：用于选择要发送的 PKM 文件。")]
    public string DistributeFolder { get; set; } = string.Empty;

    [DisplayName("保存接收结果")]
    [Category(FeatureToggle), Description("启用后，将收到的 PKM（交易结果）保存到结果保存目录。")]
    public bool Dump { get; set; }

    [DisplayName("结果保存目录")]
    [Category(Files), Description("结果保存目录：保存所有接收的 PKM 文件。")]
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

    public override string ToString() => "目录与转储设置";
}
