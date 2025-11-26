using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SysBot.Pokemon.WinForms
{
    public class UpdateForm : Form
    {
        private Button buttonDownload;
        private Label labelUpdateInfo;
        private readonly Label labelChangelogTitle = new();
        private TextBox textBoxChangelog;
        private readonly bool isUpdateRequired;
        private readonly bool isUpdateAvailable;
        private readonly string newVersion;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public UpdateForm(bool updateRequired, string newVersion, bool updateAvailable)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            isUpdateRequired = updateRequired;
            this.newVersion = newVersion;
            isUpdateAvailable = updateAvailable;
            InitializeComponent();
            Load += async (sender, e) => await FetchAndDisplayChangelog();
            UpdateFormText();
        }

        private void InitializeComponent()
        {
            labelUpdateInfo = new Label();
            buttonDownload = new Button();

            ClientSize = new Size(500, 300);

            labelUpdateInfo.AutoSize = true;
            labelUpdateInfo.Location = new Point(12, 20);
            labelUpdateInfo.Size = new Size(460, 60);

            if (isUpdateRequired)
            {
                labelUpdateInfo.Text = "有一个必须安装的更新可用。您必须更新才能继续使用此应用程序。";
                ControlBox = false;
            }
            else if (isUpdateAvailable)
            {
                labelUpdateInfo.Text = "有新版本可用。请下载最新版本。";
            }
            else
            {
                labelUpdateInfo.Text = "您已是最新版本。如有需要可重新下载。";
                buttonDownload.Text = "重新下载最新版本";
            }

            buttonDownload.Size = new Size(130, 23);
            int buttonX = (ClientSize.Width - buttonDownload.Size.Width) / 2;
            int buttonY = ClientSize.Height - buttonDownload.Size.Height - 20;
            buttonDownload.Location = new Point(buttonX, buttonY);
            if (string.IsNullOrEmpty(buttonDownload.Text))
            {
                buttonDownload.Text = "下载更新";
            }
            buttonDownload.Click += ButtonDownload_Click;

            labelChangelogTitle.AutoSize = true;
            labelChangelogTitle.Location = new Point(10, 60);
            labelChangelogTitle.Size = new Size(70, 15);
            labelChangelogTitle.Font = new Font(labelChangelogTitle.Font.FontFamily, 11, FontStyle.Bold);
            labelChangelogTitle.Text = $"更新日志 ({newVersion}):";

            textBoxChangelog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(10, 90),
                Size = new Size(480, 150),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right
            };

            Controls.Add(labelUpdateInfo);
            Controls.Add(buttonDownload);
            Controls.Add(labelChangelogTitle);
            Controls.Add(textBoxChangelog);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "UpdateForm";
            StartPosition = FormStartPosition.CenterScreen;
            UpdateFormText();
        }

        private void UpdateFormText()
        {
            if (isUpdateAvailable)
            {
                Text = $"有可用更新 ({newVersion})";
            }
            else
            {
                Text = "重新下载最新版本";
            }
        }

        public async void PerformUpdate()
        {
            buttonDownload.Enabled = false;
            buttonDownload.Text = "正在下载...";

            try
            {
                string? downloadUrl = await UpdateChecker.FetchDownloadUrlAsync();
                if (!string.IsNullOrWhiteSpace(downloadUrl))
                {
                    string downloadedFilePath = await StartDownloadProcessAsync(downloadUrl);
                    if (!string.IsNullOrEmpty(downloadedFilePath))
                    {
                        InstallUpdate(downloadedFilePath);
                    }
                }
            }
            catch { }
        }

        private async Task FetchAndDisplayChangelog()
        {
            _ = new UpdateChecker();
            textBoxChangelog.Text = await UpdateChecker.FetchChangelogAsync();
        }

        private async void ButtonDownload_Click(object? sender, EventArgs? e)
        {
            buttonDownload.Enabled = false;
            buttonDownload.Text = "正在下载...";

            try
            {
                string? downloadUrl = await UpdateChecker.FetchDownloadUrlAsync();
                if (!string.IsNullOrWhiteSpace(downloadUrl))
                {
                    string downloadedFilePath = await StartDownloadProcessAsync(downloadUrl);
                    if (!string.IsNullOrEmpty(downloadedFilePath))
                    {
                        InstallUpdate(downloadedFilePath);
                    }
                }
                else
                {
                    MessageBox.Show("获取下载链接失败。请检查您的网络连接后重试。",
                        "下载错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新失败: {ex.Message}", "更新错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                buttonDownload.Enabled = true;
                buttonDownload.Text = isUpdateAvailable ? "下载更新" : "重新下载最新版本";
            }
        }

        private static async Task<string> StartDownloadProcessAsync(string downloadUrl)
        {
            Main.IsUpdating = true;
            string tempPath = Path.Combine(Path.GetTempPath(), $"SysBot.Pokemon.WinForms_{Guid.NewGuid()}.exe");
            
            const int maxRetries = 3;
            Exception? lastException = null;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                if (retry > 0)
                {
                    // Wait before retry (exponential backoff)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)));
                    Console.WriteLine($"Retrying download attempt {retry + 1}/{maxRetries}...");
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10); // 10 minute timeout for downloads on slow connections
                    client.DefaultRequestHeaders.Add("User-Agent", "PokeBot");
                    // No auth token needed for public repo
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                    try
                    {
                        var response = await client.GetAsync(downloadUrl);
                        response.EnsureSuccessStatusCode();
                        
                        // Download with progress tracking for large files
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            var totalBytes = response.Content.Headers.ContentLength ?? 0;
                            var bytesRead = 0;
                            var buffer = new byte[8192];
                            
                            using (var ms = new MemoryStream())
                            {
                                int read;
                                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await ms.WriteAsync(buffer, 0, read);
                                    bytesRead += read;
                                    
                                    if (totalBytes > 0)
                                    {
                                        var progress = (int)((bytesRead * 100L) / totalBytes);
                                        Console.WriteLine($"Download progress: {progress}%");
                                    }
                                }
                                
                                var fileBytes = ms.ToArray();
                                await File.WriteAllBytesAsync(tempPath, fileBytes);
                            }
                        }
                        Console.WriteLine($"Successfully downloaded update to {tempPath}");
                        return tempPath;
                    }
                    catch (TaskCanceledException ex)
                    {
                        Console.WriteLine($"Download timed out on attempt {retry + 1}: {ex.Message}");
                        lastException = ex;
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.WriteLine($"Download failed on attempt {retry + 1}: {ex.Message}");
                        lastException = ex;
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during download on attempt {retry + 1}: {ex.Message}");
                        lastException = ex;
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                }
            }

            // All retries failed
            Console.WriteLine($"Failed to download update after {maxRetries} attempts");
            throw lastException ?? new Exception("所有重试尝试后下载失败");
        }

        private static void InstallUpdate(string downloadedFilePath)
        {
            try
            {
                string currentExePath = Application.ExecutablePath;
                string applicationDirectory = Path.GetDirectoryName(currentExePath) ?? "";
                string executableName = Path.GetFileName(currentExePath);
                string backupPath = Path.Combine(applicationDirectory, $"{executableName}.backup");

                // Create batch file for update process
                string batchPath = Path.Combine(Path.GetTempPath(), "UpdateSysBot.bat");
                string batchContent = @$"
                                            @echo off
                                            timeout /t 2 /nobreak >nul
                                            echo 正在更新 SysBot...

                                            rem 备份当前版本
                                            if exist ""{currentExePath}"" (
                                                if exist ""{backupPath}"" (
                                                    del ""{backupPath}""
                                                )
                                                move ""{currentExePath}"" ""{backupPath}""
                                            )

                                            rem 安装新版本
                                            move ""{downloadedFilePath}"" ""{currentExePath}""

                                            rem 启动新版本
                                            start """" ""{currentExePath}""

                                            rem 清理
                                            del ""%~f0""
                                            ";

                File.WriteAllText(batchPath, batchContent);

                // Start the update batch file
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = batchPath,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(startInfo);

                // Exit the current instance
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"安装更新失败: {ex.Message}", "更新错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}