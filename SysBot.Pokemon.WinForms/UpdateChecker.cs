using Newtonsoft.Json;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SysBot.Pokemon.WinForms
{
    public class UpdateChecker
    {
        private const string RepositoryOwner = "hexbyt3";
        private const string RepositoryName = "PokeBot";

        // Reuse HttpClient to prevent socket exhaustion and memory leaks
        // HttpClient is thread-safe and should be reused
        private static readonly HttpClient _sharedClient = CreateGitHubClient();

        private static HttpClient CreateGitHubClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minute timeout for slow connections
            client.DefaultRequestHeaders.Add("User-Agent", "PokeBot");
            // No auth token needed for public repo
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        public static async Task<(bool UpdateAvailable, bool UpdateRequired, string NewVersion)> CheckForUpdatesAsync(bool forceShow = false)
        {
            try
            {
                ReleaseInfo? latestRelease = await FetchLatestReleaseAsync();
                if (latestRelease == null)
                {
                    if (forceShow)
                    {
                        MessageBox.Show("无法获取更新信息，请检查网络连接。", "更新检查失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return (false, false, string.Empty);
                }

                bool updateAvailable = latestRelease.TagName != PokeBot.Version;
                bool updateRequired = !latestRelease.Prerelease && IsUpdateRequired(latestRelease.Body ?? string.Empty);
                string newVersion = latestRelease.TagName ?? string.Empty;

                if (forceShow)
                {
                    var updateForm = new UpdateForm(updateRequired, newVersion, updateAvailable);
                    updateForm.ShowDialog();
                }

                return (updateAvailable, updateRequired, newVersion);
            }
            catch (Exception ex)
            {
                if (forceShow)
                {
                    MessageBox.Show($"检查更新时出错：{ex.Message}", "更新检查失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return (false, false, string.Empty);
            }
        }

        public static async Task<string> FetchChangelogAsync()
        {
            try
            {
                ReleaseInfo? latestRelease = await FetchLatestReleaseAsync();
                return latestRelease?.Body ?? "无法获取最新的版本信息。";
            }
            catch (Exception ex)
            {
                return $"获取更新日志失败：{ex.Message}";
            }
        }

        public static async Task<string?> FetchDownloadUrlAsync()
        {
            try
            {
                ReleaseInfo? latestRelease = await FetchLatestReleaseAsync();
                if (latestRelease?.Assets == null || !latestRelease.Assets.Any())
                {
                    Console.WriteLine("该版本未找到任何资源文件");
                    return null;
                }

                var exeAsset = latestRelease.Assets
                    .FirstOrDefault(a => a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);

                if (exeAsset == null)
                {
                    Console.WriteLine("该版本未包含 .exe 安装包");
                    return null;
                }

                // For public repos, use browser_download_url directly
                if (string.IsNullOrEmpty(exeAsset.BrowserDownloadUrl))
                {
                    Console.WriteLine("下载链接为空");
                    return null;
                }

                Console.WriteLine($"找到下载链接：{exeAsset.BrowserDownloadUrl}");
                return exeAsset.BrowserDownloadUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取下载链接时出错：{ex.Message}");
                return null;
            }
        }

        private static async Task<ReleaseInfo?> FetchLatestReleaseAsync()
        {
            const int maxRetries = 3;
            Exception? lastException = null;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                if (retry > 0)
                {
                    // Wait before retry (exponential backoff)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)));
                    Console.WriteLine($"第 {retry + 1}/{maxRetries} 次获取重试中...");
                }

                // Use shared HttpClient instance to prevent memory leaks
                try
                {
                    string releasesUrl = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
                    Console.WriteLine($"正在从地址获取信息：{releasesUrl}");

                    HttpResponseMessage response = await _sharedClient.GetAsync(releasesUrl);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"GitHub 接口返回错误：{response.StatusCode} - {responseContent}");
                        lastException = new HttpRequestException($"GitHub API 返回了 {response.StatusCode}");
                        continue; // Try again
                    }

                    var releaseInfo = JsonConvert.DeserializeObject<ReleaseInfo>(responseContent);
                    if (releaseInfo == null)
                    {
                        Console.WriteLine("无法解析版本信息");
                        lastException = new InvalidOperationException("无法解析版本信息");
                        continue; // Try again
                    }

                    Console.WriteLine($"已成功获取版本信息，标签：{releaseInfo.TagName}");
                    return releaseInfo;
                }
                catch (TaskCanceledException ex)
                {
                    Console.WriteLine($"第 {retry + 1} 次请求超时：{ex.Message}");
                    lastException = ex;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"第 {retry + 1} 次网络错误：{ex.Message}");
                    lastException = ex;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"第 {retry + 1} 次尝试发生错误：{ex.Message}");
                    lastException = ex;
                }
            }

            // All retries failed
            Console.WriteLine($"连续 {maxRetries} 次尝试后仍无法获取版本信息");
            if (lastException != null)
                Console.WriteLine($"最后一次错误：{lastException.Message}");

            return null;
        }

        private static bool IsUpdateRequired(string changelogBody)
        {
            return !string.IsNullOrWhiteSpace(changelogBody) &&
                   changelogBody.Contains("Required = Yes", StringComparison.OrdinalIgnoreCase);
        }

        private class ReleaseInfo
        {
            [JsonProperty("tag_name")]
            public string? TagName { get; set; }

            [JsonProperty("prerelease")]
            public bool Prerelease { get; set; }

            [JsonProperty("assets")]
            public List<AssetInfo>? Assets { get; set; }

            [JsonProperty("body")]
            public string? Body { get; set; }
        }

        private class AssetInfo
        {
            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("url")]
            public string? Url { get; set; }

            [JsonProperty("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }
    }
}