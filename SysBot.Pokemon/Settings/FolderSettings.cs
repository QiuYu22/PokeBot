using System.ComponentModel;
using System.IO;

namespace SysBot.Pokemon;

public class FolderSettings : IDumper
{
    private const string FeatureToggle = "功能开关";

    private const string Files = "文件";

    [Category(Files), Description("源文件夹：从中选择要分发的 PKM 文件。"), DisplayName("分发文件夹")]
    public string DistributeFolder { get; set; } = string.Empty;

    [Category(FeatureToggle), Description("启用后，将所有收到的 PKM 文件（交易结果）转储到转储文件夹。"), DisplayName("转储")]
    public bool Dump { get; set; }

    [Category(Files), Description("目标文件夹：所有收到的 PKM 文件转储到此处。"), DisplayName("转储文件夹")]
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

    public override string ToString() => "文件夹/转储设置";
}
