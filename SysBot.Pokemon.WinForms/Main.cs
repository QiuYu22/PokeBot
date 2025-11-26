using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Discord;
using SysBot.Pokemon.Helpers;
using SysBot.Pokemon.WinForms.Properties;
using SysBot.Pokemon.WinForms.Helpers;
using SysBot.Pokemon.Z3;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SysBot.Pokemon.WinForms
{
    public sealed partial class Main : Form
    {
        // Windows API for forcing window frame update
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, 
            int X, int Y, int cx, int cy, uint uFlags);
        
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;
        
        // Performance optimization flags
        private bool _suspendLayout = false;
        private bool _deferredInvalidate = false;
        private DateTime _lastInvalidate = DateTime.MinValue;
        private const int INVALIDATE_THROTTLE_MS = 16; // 60 FPS max
        private readonly List<PokeBotState> Bots = [];

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal ProgramConfig Config { get; set; } = null!;

        private IPokeBotRunner RunningEnvironment { get; set; } = null!;

        public readonly ISwitchConnectionAsync? SwitchConnection;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static volatile bool IsUpdating = false;
        private System.Windows.Forms.Timer? _autoSaveTimer;
        private System.Windows.Forms.Timer? _logCleanupTimer;
        private bool _isFormLoading = true;

        private SearchManager _searchManager = null!;
        private TextBoxForwarder _textBoxForwarder = null!;

        internal bool hasUpdate = false;
        private bool _isRestoringFromTray = false;
        private LinearGradientBrush? _logoBrush;
        private Image? _currentModeImage = null;

        private readonly Color CuztomBackground = Color.FromArgb(27, 40, 56);
        private readonly Color CuztomDarkBackground = Color.FromArgb(22, 32, 45);
        private readonly Color CuztomAccent = Color.FromArgb(102, 192, 244);
        private readonly Color CuztomText = Color.FromArgb(239, 239, 239);
        private readonly Color CuztomSubText = Color.FromArgb(139, 179, 217);

        public Main()
        {
            // Enable DPI awareness
            this.AutoScaleMode = AutoScaleMode.Dpi;
            
            InitializeComponent();
            
            // Performance optimizations
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                    ControlStyles.UserPaint | 
                    ControlStyles.DoubleBuffer | 
                    ControlStyles.ResizeRedraw |
                    ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
            
            // Apply dark mode to the main window
            DarkModeHelper.SetDarkMode(this.Handle);
            
            Load += async (sender, e) => await InitializeAsync();

            TC_Main = new TabControl { Visible = false };
            Tab_Bots = new TabPage();
            Tab_Hub = new TabPage();
            Tab_Logs = new TabPage();
            TC_Main.TabPages.AddRange([Tab_Bots, Tab_Hub, Tab_Logs]);
            TC_Main.SendToBack();

            _searchManager = new SearchManager(RTB_Logs, searchStatusLabel);
            ConfigureSearchEventHandlers();
        }

        private void ConfigureSearchEventHandlers()
        {
            btnCaseSensitive.CheckedChanged += (s, e) => _searchManager.ToggleCaseSensitive();
            btnRegex.CheckedChanged += (s, e) => _searchManager.ToggleRegex();
            btnWholeWord.CheckedChanged += (s, e) => _searchManager.ToggleWholeWord();
        }

        private void CreateNewConfig()
        {
            Config = new ProgramConfig();
            RunningEnvironment = GetRunner(Config);
            Config.Hub.Folder.CreateDefaults(Program.WorkingDirectory);
        }

        private async Task InitializeAsync()
        {
            if (IsUpdating)
                return;
            string discordName = string.Empty;

            PokeTradeBotSWSH.SeedChecker = new Z3SeedSearchHandler<PK8>();
            UpdateChecker updateChecker = new();

            try
            {
                var (updateAvailable, _, _) = await UpdateChecker.CheckForUpdatesAsync();
                hasUpdate = updateAvailable;
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"更新检查失败: {ex.Message}", "更新管理器");
            }

            if (File.Exists(Program.ConfigPath))
            {
                try
                {
                    var lines = File.ReadAllText(Program.ConfigPath);

                    // Check for corrupted file (null bytes)
                    if (string.IsNullOrWhiteSpace(lines) || lines.Contains('\0'))
                    {
                        throw new JsonException("配置文件包含空字节或为空");
                    }

                    Config = JsonSerializer.Deserialize(lines, ProgramConfigContext.Default.ProgramConfig) ?? new ProgramConfig();
                    LogConfig.MaxArchiveFiles = Config.Hub.MaxArchiveFiles;
                    LogConfig.LoggingEnabled = Config.Hub.LoggingEnabled;
                    Config.Hub.Distribution.CurrentMode = Config.Mode;
                    comboBox1.SelectedValue = (int)Config.Mode;

                    RunningEnvironment = GetRunner(Config);
                    foreach (var bot in Config.Bots)
                    {
                        bot.Initialize();
                        AddBot(bot);
                    }
                }
                catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
                {
                    LogUtil.LogError($"配置文件已损坏: {ex.Message}。正在尝试从备份恢复。", "配置");

                    // Try to recover from backup
                    var backupPath = Program.ConfigPath + ".bak";
                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            var backupLines = File.ReadAllText(backupPath);
                            Config = JsonSerializer.Deserialize(backupLines, ProgramConfigContext.Default.ProgramConfig) ?? new ProgramConfig();

                            // Restore the main config from backup
                            File.Copy(backupPath, Program.ConfigPath, true);
                            LogUtil.LogInfo("配置", "已成功从备份恢复配置。");

                            LogConfig.MaxArchiveFiles = Config.Hub.MaxArchiveFiles;
                            LogConfig.LoggingEnabled = Config.Hub.LoggingEnabled;
                            Config.Hub.Distribution.CurrentMode = Config.Mode;
                            comboBox1.SelectedValue = (int)Config.Mode;

                            RunningEnvironment = GetRunner(Config);
                            foreach (var bot in Config.Bots)
                            {
                                bot.Initialize();
                                AddBot(bot);
                            }
                        }
                        catch (Exception backupEx)
                        {
                            LogUtil.LogError("配置", $"从备份恢复失败: {backupEx.Message}。正在创建新配置。");
                            CreateNewConfig();
                        }
                    }
                    else
                    {
                        LogUtil.LogError("配置", "未找到备份文件。正在创建新配置。");
                        CreateNewConfig();
                    }
                }
            }
            else
            {
                CreateNewConfig();
            }

            RTB_Logs.MaxLength = 2_000_000; // Limit to 2MB of text to prevent memory issues
            LoadControls();
            Text = $"{(string.IsNullOrEmpty(Config.Hub.BotName) ? "GenPKM.com" : Config.Hub.BotName)} {PokeBot.Version} ({Config.Mode})";
            trayIcon.Text = Text;
            _ = Task.Run(BotMonitor);
            InitUtil.InitializeStubs(Config.Mode);
            _isFormLoading = false;
            UpdateBackgroundImage(Config.Mode);
            UpdateStatusIndicatorColor();
            
            this.ActiveControl = null;
            LogUtil.LogInfo("系统", $"机器人初始化完成");
            _ = Task.Run(() =>
            {
                try
                {
                    this.InitWebServer();
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"初始化 Web 服务器失败: {ex.Message}", "系统");
                }
            });
        }

        #region Enhanced Search Implementation

        private void LogSearchBox_TextChanged(object sender, EventArgs e)
        {
            _searchManager.UpdateSearch(logSearchBox.Text);
        }

        private void LogSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    e.SuppressKeyPress = true;
                    if (e.Shift)
                        _searchManager.FindPrevious();
                    else
                        _searchManager.FindNext();
                    break;

                case Keys.Escape:
                    e.SuppressKeyPress = true;
                    _searchManager.ClearSearch();
                    logSearchBox.Clear();
                    break;

                case Keys.F when e.Control:
                    e.SuppressKeyPress = true;
                    logSearchBox.Focus();
                    logSearchBox.SelectAll();
                    break;
            }
        }

        private void RTB_Logs_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F when e.Control:
                    e.SuppressKeyPress = true;
                    logSearchBox.Focus();
                    logSearchBox.SelectAll();
                    break;

                case Keys.F3:
                    e.SuppressKeyPress = true;
                    if (e.Shift)
                        _searchManager.FindPrevious();
                    else
                        _searchManager.FindNext();
                    break;
            }
        }

        #endregion

        private static IPokeBotRunner GetRunner(ProgramConfig cfg) => cfg.Mode switch
        {
            ProgramMode.SWSH => new PokeBotRunnerImpl<PK8>(cfg.Hub, new BotFactory8SWSH(), cfg),
            ProgramMode.BDSP => new PokeBotRunnerImpl<PB8>(cfg.Hub, new BotFactory8BS(), cfg),
            ProgramMode.LA => new PokeBotRunnerImpl<PA8>(cfg.Hub, new BotFactory8LA(), cfg),
            ProgramMode.SV => new PokeBotRunnerImpl<PK9>(cfg.Hub, new BotFactory9SV(), cfg),
            ProgramMode.LGPE => new PokeBotRunnerImpl<PB7>(cfg.Hub, new BotFactory7LGPE(), cfg),
            ProgramMode.PLZA => new PokeBotRunnerImpl<PA9>(cfg.Hub, new BotFactory9PLZA(), cfg),
            _ => throw new IndexOutOfRangeException("不支持的模式。"),
        };

        private async Task BotMonitor()
        {
            while (!Disposing)
            {
                try
                {
                    // Only update UI if form is visible and not suspended
                    if (WindowState != FormWindowState.Minimized && !_suspendLayout)
                    {
                        // Batch updates to reduce UI thread blocking
                        var controllers = FLP_Bots.Controls.OfType<BotController>().ToList();
                        if (controllers.Count > 0)
                        {
                            BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                            {
                                SuspendLayout();
                                foreach (var c in controllers)
                                    c.ReadState();
                                ResumeLayout(false);
                            }));
                        }

                        UpdateControlButtonStates();
                    }

                    if (trayIcon != null && trayIcon.Visible && Config != null)
                    {
                        // Get bot counts in a thread-safe manner
                        int runningBots = 0;
                        int totalBots = 0;

                        if (InvokeRequired)
                        {
                            // Use BeginInvoke to avoid blocking the monitoring thread
                            BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                            {
                                runningBots = FLP_Bots.Controls.OfType<BotController>().Count(c => c.GetBot()?.IsRunning ?? false);
                                totalBots = FLP_Bots.Controls.OfType<BotController>().Count();
                                
                                // Update tray icon text from UI thread
                                string botTitle = string.IsNullOrWhiteSpace(Config.Hub.BotName) ? "宝可梦机器人" : Config.Hub.BotName;
                                trayIcon.Text = totalBots == 0
                                    ? $"{botTitle} - 未配置机器人"
                                    : $"{botTitle} - {runningBots}/{totalBots} 个机器人运行中";
                            }));
                        }
                        else
                        {
                            runningBots = FLP_Bots.Controls.OfType<BotController>().Count(c => c.GetBot()?.IsRunning ?? false);
                            totalBots = FLP_Bots.Controls.OfType<BotController>().Count();
                            
                            string botTitle = string.IsNullOrWhiteSpace(Config.Hub.BotName) ? "宝可梦机器人" : Config.Hub.BotName;
                            trayIcon.Text = totalBots == 0
                                ? $"{botTitle} - 未配置机器人"
                                : $"{botTitle} - {runningBots}/{totalBots} 个机器人运行中";
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"机器人监控器错误: {ex.Message}", "监控器");
                }
                await Task.Delay(3_000).ConfigureAwait(false); // Reduced frequency for better performance
            }
        }

        private void UpdateControlButtonStates()
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateControlButtonStates());
                return;
            }

            var botControllers = FLP_Bots.Controls.OfType<BotController>().ToList(); // Cache the collection
            var runningBots = botControllers.Count(c => c.GetBot()?.IsRunning ?? false);
            var totalBots = botControllers.Count;
            var anyRunning = runningBots > 0;

            if (btnStart?.Tag is EnhancedButtonAnimationState startState)
            {
                startState.IsActive = !anyRunning && totalBots > 0;
            }

            if (btnStop?.Tag is EnhancedButtonAnimationState stopState)
            {
                stopState.IsActive = anyRunning;
            }

            if (btnReboot?.Tag is EnhancedButtonAnimationState rebootState)
            {
                rebootState.IsActive = anyRunning;
            }
        }

        private void LoadControls()
        {
            PG_Hub.SelectedObject = RunningEnvironment.Config;
            _autoSaveTimer = new System.Windows.Forms.Timer
            {
                Interval = 10_000,
                Enabled = true
            };
            _autoSaveTimer.Tick += (s, e) =>
            {
                // Run auto-save on background thread to avoid blocking UI
                Task.Run(() =>
                {
                    try
                    {
                        SaveCurrentConfig();
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"自动保存失败: {ex.Message}", "配置");
                    }
                });
            };
            var routines = ((PokeRoutineType[])Enum.GetValues(typeof(PokeRoutineType))).Where(z => RunningEnvironment.SupportsRoutine(z));
            var list = routines.Select(z => new ComboItem(z.ToString(), (int)z)).ToArray();
            CB_Routine.DisplayMember = nameof(ComboItem.Text);
            CB_Routine.ValueMember = nameof(ComboItem.Value);
            CB_Routine.DataSource = list;
            CB_Routine.SelectedValue = (int)PokeRoutineType.FlexTrade;

            var protocols = (SwitchProtocol[])Enum.GetValues(typeof(SwitchProtocol));
            var listP = protocols.Select(z => new ComboItem(z.ToString(), (int)z)).ToArray();
            CB_Protocol.DisplayMember = nameof(ComboItem.Text);
            CB_Protocol.ValueMember = nameof(ComboItem.Value);
            CB_Protocol.DataSource = listP;
            CB_Protocol.SelectedIndex = (int)SwitchProtocol.WiFi;

            var gameModes = Enum.GetValues(typeof(ProgramMode))
                .Cast<ProgramMode>()
                .Where(m => m != ProgramMode.None)
                .Select(mode => new { Text = mode.ToString(), Value = (int)mode })
                .ToList();
            comboBox1.DisplayMember = "Text";
            comboBox1.ValueMember = "Value";
            comboBox1.DataSource = gameModes;
            comboBox1.SelectedValue = (int)Config.Mode;
            
            // Apply enhanced styling to the game selector
            ConfigureGameSelector();

            _textBoxForwarder = new TextBoxForwarder(RTB_Logs);
            LogUtil.Forwarders.Add(_textBoxForwarder);

            // Initialize log cleanup timer - runs every 30 minutes
            _logCleanupTimer = new System.Windows.Forms.Timer
            {
                Interval = 30 * 60 * 1000, // 30 minutes
                Enabled = true
            };
            _logCleanupTimer.Tick += (s, e) =>
            {
                try
                {
                    // Clean up logs if they're getting too large
                    if (RTB_Logs.TextLength > RTB_Logs.MaxLength * 0.8)
                    {
                        LogUtil.LogInfo("正在执行自动日志清理以保持性能", "系统");

                        // Keep only the last 25% of logs
                        BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                        {
                            var lines = RTB_Logs.Lines;
                            var linesToKeep = lines.Length / 4;
                            RTB_Logs.Lines = lines[^linesToKeep..];
                            _searchManager.ClearSearch(); // Clear search after cleanup
                        }));
                    }

                    // Also clean up old log files on disk
                    CleanupOldLogFiles();
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"日志清理失败: {ex.Message}", "系统");
                }
            };
        }

        private ProgramConfig GetCurrentConfiguration()
        {
            if (Config == null)
            {
                throw new InvalidOperationException("配置尚未初始化，因为未输入有效的许可证。");
            }
            Config.Bots = [.. Bots];
            return Config;
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IsUpdating) return;

            // Remove log forwarders to prevent memory leaks
            LogUtil.Forwarders.Remove(_textBoxForwarder);

            // Let the form close normally when X button is clicked
            // No longer minimizing to tray on close
            this.StopWebServer();

            try
            {
                string? exePath = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    string? dirPath = Path.GetDirectoryName(exePath);
                    if (!string.IsNullOrEmpty(dirPath))
                    {
                        string portInfoPath = Path.Combine(dirPath, $"MergeBot_{Environment.ProcessId}.port");
                        if (File.Exists(portInfoPath))
                            File.Delete(portInfoPath);
                    }
                }
            }
            catch { }

            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Dispose();
            }

            if (_logCleanupTimer != null)
            {
                _logCleanupTimer.Stop();
                _logCleanupTimer.Dispose();
            }

            // Animation timer removed

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            if (_logoBrush != null)
            {
                _logoBrush.Dispose();
                _logoBrush = null;
            }

            SaveCurrentConfig();
            var bots = RunningEnvironment;
            if (!bots.IsRunning)
                return;

            async Task WaitUntilNotRunning()
            {
                while (bots.IsRunning)
                    await Task.Delay(10).ConfigureAwait(false);
            }

            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            bots.StopAll();
            Task.WhenAny(WaitUntilNotRunning(), Task.Delay(5_000)).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        private void SaveCurrentConfig()
        {
            try
            {
                var cfg = GetCurrentConfiguration();
                var json = JsonSerializer.Serialize(cfg, ProgramConfigContext.Default.ProgramConfig);

                // Use atomic write operation to prevent corruption
                var tempPath = Program.ConfigPath + ".tmp";
                var backupPath = Program.ConfigPath + ".bak";

                // Write to temporary file first
                File.WriteAllText(tempPath, json);

                // Create backup of existing config if it exists
                if (File.Exists(Program.ConfigPath))
                {
                    File.Copy(Program.ConfigPath, backupPath, true);
                }

                // Atomic rename operation
                File.Move(tempPath, Program.ConfigPath, true);

                // Delete backup after successful save
                if (File.Exists(backupPath))
                {
                    try { File.Delete(backupPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"保存配置失败: {ex.Message}", "配置");
            }
        }

        [JsonSerializable(typeof(ProgramConfig))]
        [JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
        public sealed partial class ProgramConfigContext : JsonSerializerContext
        { }

        private void B_Start_Click(object sender, EventArgs e)
        {
            SaveCurrentConfig();

            LogUtil.LogInfo("窗体", "正在启动所有机器人...");
            RunningEnvironment.InitializeStart();
            SendAll(BotControlCommand.Start);

            SetButtonActiveState(btnStart, true);
            SetButtonActiveState(btnStop, false);
            SetButtonActiveState(btnReboot, false);

            // Keep the Bots tab selected when starting
            foreach (Button navBtn in navButtonsPanel.Controls.OfType<Button>())
            {
                if (navBtn.Tag is NavButtonState state)
                {
                    // Keep Bots tab (index 0) selected, deselect others
                    state.IsSelected = (state.Index == 0);
                    navBtn.Invalidate();
                }
            }

            // Stay on Bots tab instead of switching to Logs
            TransitionPanels(0);
            titleLabel.Text = "机器人管理";

            if (Bots.Count == 0)
                WinFormsUtil.Alert("未配置机器人，但所有支持服务已启动。");
        }

        private void B_RebootStop_Click(object sender, EventArgs e)
        {
            SetButtonActiveState(btnReboot, true);
            SetButtonActiveState(btnStart, false);
            SetButtonActiveState(btnStop, false);

            Task.Run(async () =>
            {
                try
                {
                    LogUtil.LogInfo("窗体", "正在开始重置流程...");
                    SaveCurrentConfig();

                    // Phase 1: Stop all bots gracefully
                    LogUtil.LogInfo("窗体", "阶段 1: 正在停止所有机器人...");
                    SendAll(BotControlCommand.Stop);

                    // Phase 2: Wait for all bots to fully stop
                    var stopTimeout = DateTime.Now.AddSeconds(30);
                    while (DateTime.Now < stopTimeout)
                    {
                        if (AreAllBotsStopped())
                        {
                            LogUtil.LogInfo("窗体", "所有机器人已成功停止");
                            break;
                        }
                        await Task.Delay(500).ConfigureAwait(false);
                    }

                    if (!AreAllBotsStopped())
                    {
                        LogUtil.LogInfo("窗体", "部分机器人未能及时停止，正在强制停止...");
                        SendAll(BotControlCommand.Stop);
                        await Task.Delay(2000).ConfigureAwait(false);
                    }

                    // Phase 3: Stop all services
                    LogUtil.LogInfo("窗体", "阶段 3: 正在停止所有服务...");
                    await Task.Delay(2000).ConfigureAwait(false); // Give services time to fully stop

                    // Phase 4: Reinitialize environment
                    LogUtil.LogInfo("窗体", "阶段 4: 正在重新初始化环境...");
                    RunningEnvironment.InitializeStart();
                    await Task.Delay(1000).ConfigureAwait(false);

                    // Phase 5: Reboot consoles
                    LogUtil.LogInfo("窗体", "阶段 5: 正在重启所有主机...");
                    SendAll(BotControlCommand.RebootAndStop);
                    await Task.Delay(8000).ConfigureAwait(false); // Give consoles time to reboot

                    // Phase 6: Restart all bots with staggered timing
                    LogUtil.LogInfo("窗体", "阶段 6: 正在启动所有机器人...");
                    await StartBotsStaggeredAsync();

                    BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        SetButtonActiveState(btnReboot, false);
                        SetButtonActiveState(btnStop, true);
                        SetButtonActiveState(btnStart, false);

                        foreach (Button navBtn in navButtonsPanel.Controls.OfType<Button>())
                        {
                            if (navBtn.Tag is NavButtonState state)
                            {
                                state.IsSelected = false;
                                navBtn.Invalidate();
                            }
                        }

                        if (btnNavLogs.Tag is NavButtonState logsState)
                        {
                            logsState.IsSelected = true;
                            btnNavLogs.Invalidate();
                        }

                        TransitionPanels(2);
                        titleLabel.Text = "系统日志";
                    }));

                    LogUtil.LogInfo("重置流程已成功完成", "窗体");

                    if (Bots.Count == 0)
                        WinFormsUtil.Alert("未配置机器人，但所有支持服务已发送重启命令。");
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"重置流程失败: {ex.Message}", "窗体");
                    BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        SetButtonActiveState(btnReboot, false);
                        SetButtonActiveState(btnStop, false);
                        SetButtonActiveState(btnStart, false);
                        WinFormsUtil.Error($"重置失败: {ex.Message}");
                    }));
                }
            });
        }

        private async void Updater_Click(object sender, EventArgs e)
        {
            var (updateAvailable, updateRequired, newVersion) = await UpdateChecker.CheckForUpdatesAsync();
            hasUpdate = updateAvailable;

            if (!updateAvailable)
            {
                var result = MessageBox.Show(
                    "您已是最新版本。是否要重新下载当前版本？",
                    "更新检查",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    UpdateForm updateForm = new(updateRequired, newVersion, updateAvailable: false);
                    updateForm.ShowDialog();
                }
            }
            else
            {
                UpdateForm updateForm = new(updateRequired, newVersion, updateAvailable: true);
                updateForm.ShowDialog();
            }
        }

        private void SetButtonActiveState(Button button, bool isActive)
        {
            if (button?.Tag is EnhancedButtonAnimationState state)
            {
                state.IsActive = isActive;
                button.Invalidate();
            }
        }

        private void SendAll(BotControlCommand cmd)
        {
            foreach (var c in FLP_Bots.Controls.OfType<BotController>())
                c.SendCommand(cmd, false);

            LogUtil.LogText($"已向所有机器人发送 {cmd} 命令。");
        }

        private void BtnTray_Click(object sender, EventArgs e)
        {
            // Send to Tray - minimizes to system tray
            MinimizeToTray();
        }

        private void UpdateAddButtonPosition()
        {
            if (B_New != null && CB_Routine != null)
            {
                B_New.Location = new Point(CB_Routine.Right + 10, 16);
            }
        }

        private void AddBotPanel_Layout(object sender, EventArgs e)
        {
            UpdateAddButtonPosition();
        }

        private void CB_Routine_SizeChanged(object sender, EventArgs e)
        {
            UpdateAddButtonPosition();
        }

        private void CB_Routine_LocationChanged(object sender, EventArgs e)
        {
            UpdateAddButtonPosition();
        }

        private void FLP_Bots_Scroll(object sender, ScrollEventArgs e)
        {
            FLP_Bots.Invalidate();
        }

        private void FLP_Bots_ControlAdded(object sender, ControlEventArgs e)
        {
            FLP_Bots.Invalidate();
        }

        private void FLP_Bots_ControlRemoved(object sender, ControlEventArgs e)
        {
            FLP_Bots.Invalidate();
        }

        private void BtnClearLogs_Click(object sender, EventArgs e)
        {
            RTB_Logs.Clear();
            _searchManager.ClearSearch();
        }


        private void B_Stop_Click(object sender, EventArgs e)
        {
            var env = RunningEnvironment;
            if (!env.IsRunning && (ModifierKeys & Keys.Alt) == 0)
            {
                WinFormsUtil.Alert("当前没有正在运行的内容。");
                return;
            }

            var cmd = BotControlCommand.Stop;

            if ((ModifierKeys & Keys.Control) != 0 || (ModifierKeys & Keys.Shift) != 0)
            {
                if (env.IsRunning)
                {
                    WinFormsUtil.Alert("正在命令所有机器人进入空闲状态。", "按停止键（不带修饰键）可硬停止并解锁控制，或再次按带修饰键的停止键可恢复运行。");
                    cmd = BotControlCommand.Idle;
                    SetButtonActiveState(btnStop, true);
                }
                else
                {
                    WinFormsUtil.Alert("正在命令所有机器人恢复其原始任务。", "按停止键（不带修饰键）可硬停止并解锁控制。");
                    cmd = BotControlCommand.Resume;
                    SetButtonActiveState(btnStop, false);
                }
            }
            else
            {
                env.StopAll();
                SetButtonActiveState(btnStart, false);
                SetButtonActiveState(btnStop, false);
                SetButtonActiveState(btnReboot, false);
            }
            SendAll(cmd);
        }

        private void B_New_Click(object sender, EventArgs e)
        {
            var cfg = CreateNewBotConfig();
            if (!AddBot(cfg))
            {
                WinFormsUtil.Alert("无法添加机器人；请确保详细信息有效且不与已存在的机器人重复。");
                return;
            }
            System.Media.SystemSounds.Asterisk.Play();
        }

        private bool AddBot(PokeBotState cfg)
        {
            if (!cfg.IsValid())
                return false;

            if (Bots.Any(z => z.Connection.Equals(cfg.Connection)))
                return false;

            PokeRoutineExecutorBase newBot;
            try
            {
                LogUtil.LogError("机器人", $"当前模式 ({Config.Mode}) 不支持此类型的机器人 ({cfg.CurrentRoutineType})。");
                newBot = RunningEnvironment.CreateBotFromConfig(cfg);
            }
            catch
            {
                return false;
            }

            try
            {
                RunningEnvironment.Add(newBot);
            }
            catch (ArgumentException ex)
            {
                WinFormsUtil.Error(ex.Message);
                return false;
            }

            AddBotControl(cfg);
            Bots.Add(cfg);
            return true;
        }

        private void AddBotControl(PokeBotState cfg)
        {
            int scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
            int availableWidth = FLP_Bots.ClientSize.Width;

            if (FLP_Bots.VerticalScroll.Visible)
            {
                availableWidth -= scrollBarWidth;
            }

            int botWidth = Math.Max(400, availableWidth - 10);

            var row = new BotController { Width = botWidth };
            row.Initialize(RunningEnvironment, cfg);
            FLP_Bots.Controls.Add(row);
            FLP_Bots.SetFlowBreak(row, true);

            row.Click += (s, e) =>
            {
                var details = cfg.Connection;
                TB_IP.Text = details.IP;
                NUD_Port.Value = details.Port;
                CB_Protocol.SelectedIndex = (int)details.Protocol;
                CB_Routine.SelectedValue = (int)cfg.InitialRoutine;
            };

            EventHandler removeHandler = null!;
            removeHandler = (s, e) =>
            {
                row.Remove -= removeHandler; // Unsubscribe to prevent memory leak
                Bots.Remove(row.State);
                RunningEnvironment.Remove(row.State, !RunningEnvironment.Config.SkipConsoleBotCreation);
                FLP_Bots.Controls.Remove(row);
                row.Dispose(); // Ensure proper disposal
            };
            row.Remove += removeHandler;
        }

        private PokeBotState CreateNewBotConfig()
        {
            var ip = TB_IP.Text;
            var port = (int)NUD_Port.Value;
            var cfg = BotConfigUtil.GetConfig<SwitchConnectionConfig>(ip, port);
            cfg.Protocol = (SwitchProtocol)WinFormsUtil.GetIndex(CB_Protocol);

            var pk = new PokeBotState { Connection = cfg };
            var type = (PokeRoutineType)WinFormsUtil.GetIndex(CB_Routine);
            pk.Initialize(type);
            return pk;
        }

        private void FLP_Bots_Resize(object sender, EventArgs e)
        {
            int scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
            int availableWidth = FLP_Bots.ClientSize.Width;

            if (FLP_Bots.VerticalScroll.Visible)
            {
                availableWidth -= scrollBarWidth;
            }

            int botWidth = Math.Max(400, availableWidth - 10);

            foreach (var c in FLP_Bots.Controls.OfType<BotController>())
            {
                c.Width = botWidth;
            }
        }

        private void CB_Protocol_SelectedIndexChanged(object sender, EventArgs e)
        {
            TB_IP.Visible = CB_Protocol.SelectedIndex == 0;
        }

        private void ComboBox1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isFormLoading) return;
            if (comboBox1.SelectedValue is int selectedValue)
            {
                ProgramMode newMode = (ProgramMode)selectedValue;
                Config.Mode = newMode;
                Config.Hub.Distribution.CurrentMode = newMode;
                SaveCurrentConfig();
                UpdateRunnerAndUI();
                UpdateBackgroundImage(newMode);

                // Refresh PropertyGrid to update visibility of mode-specific settings
                if (PG_Hub != null)
                {
                    var currentConfig = PG_Hub.SelectedObject;
                    PG_Hub.SelectedObject = null;
                    PG_Hub.SelectedObject = currentConfig;
                    PG_Hub.Refresh();
                }
            }
        }

        private void ConfigureGameSelector()
        {
            // Enhanced styling for the game selector
            comboBox1.DrawMode = DrawMode.OwnerDrawFixed;
            comboBox1.ItemHeight = 24;
            comboBox1.DrawItem += (sender, e) =>
            {
                if (e.Index < 0) return;

                // Custom background colors
                var backgroundColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                    ? Color.FromArgb(45, 125, 200)
                    : Color.FromArgb(32, 38, 48);
                    
                using (var bgBrush = new SolidBrush(backgroundColor))
                {
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                }

                // Get the item text properly
                string text = "";
                var item = comboBox1.Items[e.Index];
                
                if (item != null)
                {
                    // Handle anonymous type from DataSource
                    var textProp = item.GetType().GetProperty("Text");
                    if (textProp != null)
                    {
                        text = textProp.GetValue(item)?.ToString() ?? "";
                    }
                    else
                    {
                        text = item.ToString() ?? "";
                    }
                }

                // Draw text with proper colors
                var textColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                    ? Color.White
                    : Color.FromArgb(239, 239, 239);
                    
                using (var textBrush = new SolidBrush(textColor))
                {
                    var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                    var format = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    e.Graphics.DrawString(text, e.Font ?? comboBox1.Font, textBrush, textRect, format);
                }
                
                if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
                {
                    e.DrawFocusRectangle();
                }
            };
        }

        private void UpdateRunnerAndUI()
        {
            RunningEnvironment = GetRunner(Config);
            Text = $"{(string.IsNullOrEmpty(Config.Hub.BotName) ? "GenPKM.com" : Config.Hub.BotName)} {PokeBot.Version} ({Config.Mode})";
        }

        private void UpdateStatusIndicatorPulse()
        {
            // Animation removed - just update color
            UpdateStatusIndicatorColor();
        }

        private void UpdateStatusIndicatorColor()
        {
            if (statusIndicator == null) return;

            // Simple static color - no animation
            Color newColor = hasUpdate ? Color.FromArgb(255, 102, 192, 244) : Color.FromArgb(100, 100, 100);
            statusIndicator.BackColor = newColor;
        }

        private void UpdateBackgroundImage(ProgramMode mode)
        {
            try
            {
                _currentModeImage = mode switch
                {
                    ProgramMode.SV => Resources.sv_mode_image,
                    ProgramMode.SWSH => Resources.swsh_mode_image,
                    ProgramMode.BDSP => Resources.bdsp_mode_image,
                    ProgramMode.LA => Resources.pla_mode_image,
                    ProgramMode.LGPE => Resources.lgpe_mode_image,
                    //Todo: Add Resources.plza_mode_image when asset is available
                    ProgramMode.PLZA => null,
                    _ => null,
                };
                FLP_Bots.Invalidate();
            }
            catch
            {
                _currentModeImage = null;
            }
        }

        #region Tray Icon Methods

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowFromTray();
        }

        private void TrayMenuShow_Click(object sender, EventArgs e)
        {
            ShowFromTray();
        }

        private void TrayMenuExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ShowFromTray()
        {
            // Set flag to prevent re-minimizing
            _isRestoringFromTray = true;
            
            // Make visible in taskbar first
            ShowInTaskbar = true;
            trayIcon.Visible = false;
            
            // Show the form without suspending layout
            Show();
            
            // Force normal window state
            WindowState = FormWindowState.Normal;

            // Ensure window is properly restored and focused
            BringToFront();
            Activate();
            Focus();

            // Apply dark mode after the window is fully shown
            // Use BeginInvoke to ensure it happens after the UI thread processes the show event
            BeginInvoke((MethodInvoker)(() =>
            {
                DarkModeHelper.SetDarkMode(this.Handle);

                // Force a repaint of the non-client area
                SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

                // Force proper panel layout after tray restore
                EnsurePanelLayout();
            }));

            // Clear the flag after a delay
            Task.Run(async () =>
            {
                await Task.Delay(500);
                _isRestoringFromTray = false;
                _suspendLayout = false;
            });

            // Update bots asynchronously without blocking UI
            if (TC_Main.SelectedTab == Tab_Bots && FLP_Bots.Controls.Count > 0)
            {
                // Use BeginInvoke to update bots after UI has settled
                BeginInvoke((MethodInvoker)(() =>
                {
                    // Resume animations for all bots
                    foreach (var bot in FLP_Bots.Controls.OfType<BotController>())
                    {
                        bot.ResumeAnimations();
                    }
                    
                    // Schedule bot state updates asynchronously
                    Task.Run(async () =>
                    {
                        // Small delay to let UI fully restore
                        await Task.Delay(200);
                        
                        BeginInvoke((MethodInvoker)(() =>
                        {
                            // Only update visible bots in viewport
                            var scrollPos = FLP_Bots.VerticalScroll.Value;
                            var viewportHeight = FLP_Bots.ClientSize.Height;
                            
                            foreach (var bot in FLP_Bots.Controls.OfType<BotController>())
                            {
                                // Check if bot is in visible viewport
                                if (bot.Top >= scrollPos - bot.Height && 
                                    bot.Top <= scrollPos + viewportHeight)
                                {
                                    bot.ReadState();
                                }
                            }
                        }));
                    });
                }));
            }
        }

        private void MinimizeToTray()
        {
            _suspendLayout = true;

            // Pause animations on all bot controllers before hiding
            foreach (var bot in FLP_Bots.Controls.OfType<BotController>())
            {
                bot.PauseAnimations();
            }

            Hide();
            ShowInTaskbar = false;
            trayIcon.Visible = true;

            var runningBots = FLP_Bots.Controls.OfType<BotController>().Count(c => c.GetBot()?.IsRunning ?? false);
            var totalBots = FLP_Bots.Controls.OfType<BotController>().Count();

            string message = totalBots == 0
                ? "未配置机器人"
                : $"{runningBots}/{totalBots} 个机器人运行中";

            trayIcon.ShowBalloonTip(2000, "宝可梦机器人已最小化", message, ToolTipIcon.Info);
        }

        private void Main_Resize(object sender, EventArgs e)
        {
            // Don't minimize to tray on minimize button - only on close (X) button
            // The minimize button should just minimize normally to taskbar

            // Handle window state changes to manage animations
            if (WindowState == FormWindowState.Minimized)
            {
                // Pause animations when minimized
                foreach (var bot in FLP_Bots.Controls.OfType<BotController>())
                {
                    bot.PauseAnimations();
                }
            }
            else if (WindowState == FormWindowState.Normal || WindowState == FormWindowState.Maximized)
            {
                // Resume animations when restored
                foreach (var bot in FLP_Bots.Controls.OfType<BotController>())
                {
                    bot.ResumeAnimations();
                }
            }
        }

        #endregion

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Reapply dark mode when form is shown (helps with tray restore)
            DarkModeHelper.SetDarkMode(this.Handle);

            // Ensure panels are properly positioned
            EnsurePanelLayout();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            // Apply dark mode when window is activated
            if (_isRestoringFromTray)
            {
                DarkModeHelper.SetDarkMode(this.Handle);
                // Also ensure proper panel layout when restoring from tray
                EnsurePanelLayout();
            }
        }
        
        private void EnsurePanelLayout()
        {
            // Skip if controls aren't ready
            if (contentPanel == null || headerPanel == null)
                return;
                
            // Force proper layout recalculation
            contentPanel.SuspendLayout();
            
            // Fix z-order: headerPanel must be last (on top) for DockStyle.Top to work correctly
            // The order matters: panels docked with Fill should be added first, then Top-docked panels
            contentPanel.Controls.SetChildIndex(botsPanel, 0);
            contentPanel.Controls.SetChildIndex(hubPanel, 0);
            contentPanel.Controls.SetChildIndex(logsPanel, 0);
            contentPanel.Controls.SetChildIndex(headerPanel, contentPanel.Controls.Count - 1);
            
            // Reset docking to force recalculation
            headerPanel.Dock = DockStyle.None;
            botsPanel.Dock = DockStyle.None;
            hubPanel.Dock = DockStyle.None;
            logsPanel.Dock = DockStyle.None;
            
            // Force layout update
            contentPanel.PerformLayout();
            
            // Reapply docking in correct order
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 60;
            
            botsPanel.Dock = DockStyle.Fill;
            hubPanel.Dock = DockStyle.Fill;
            logsPanel.Dock = DockStyle.Fill;
            
            contentPanel.ResumeLayout(true);
            contentPanel.PerformLayout();
        }
        
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            
            // Update all controls for new DPI
            DpiHelper.UpdateDpiForControl(this);
            
            // Update specific UI elements
            if (statusIndicator != null)
            {
                var scaledSize = DpiHelper.Scale(20);
                statusIndicator.Size = new Size(scaledSize, scaledSize);
                statusIndicator.Location = DpiHelper.Scale(new Point(10, 6));
            }
            
            // Update bot controllers
            foreach (var controller in FLP_Bots.Controls.OfType<BotController>())
            {
                controller.PerformLayout();
            }
            
            // Force layout update
            PerformLayout();
        }

        #region Performance Optimization Methods

        private void InvalidateThrottled(Control control, Rectangle? rect = null)
        {
            if (_suspendLayout) return;
            
            var now = DateTime.Now;
            if ((now - _lastInvalidate).TotalMilliseconds < INVALIDATE_THROTTLE_MS)
            {
                _deferredInvalidate = true;
                return;
            }

            _lastInvalidate = now;
            if (rect.HasValue)
                control.Invalidate(rect.Value);
            else
                control.Invalidate();
                
            if (_deferredInvalidate)
            {
                _deferredInvalidate = false;
                // Schedule deferred invalidation
                BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                {
                    if (!_suspendLayout)
                        control.Invalidate();
                }));
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCPAINT = 0x0085;
            
            // Skip non-client area painting for performance
            if (m.Msg == WM_NCPAINT && WindowState != FormWindowState.Normal)
            {
                return;
            }
            
            base.WndProc(ref m);
        }

        #endregion

        #region Log Management

        private void CleanupOldLogFiles()
        {
            try
            {
                var workingDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
                var logDirectory = Path.Combine(workingDirectory, "logs");

                if (!Directory.Exists(logDirectory))
                    return;

                var logFiles = Directory.GetFiles(logDirectory, "SysBotLog.*.txt")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                // Keep only the last 7 days of logs
                var cutoffDate = DateTime.Now.AddDays(-7);
                foreach (var file in logFiles.Where(f => f.LastWriteTime < cutoffDate))
                {
                    try
                    {
                        file.Delete();
                        LogUtil.LogInfo($"已删除旧日志文件: {file.Name}", "系统");
                    }
                    catch
                    {
                        // File might be in use, ignore
                    }
                }

                // Also check current log file size
                var currentLogFile = Path.Combine(logDirectory, "SysBotLog.txt");
                if (File.Exists(currentLogFile))
                {
                    var fileInfo = new FileInfo(currentLogFile);
                    // If current log file is over 100MB, force rotation
                    if (fileInfo.Length > 100 * 1024 * 1024)
                    {
                        LogUtil.LogInfo("当前日志文件超过 100MB，正在强制轮换", "系统");
                        // NLog will handle the rotation on next write
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"清理旧日志文件失败: {ex.Message}", "系统");
            }
        }

        #endregion

        #region Reset Helper Methods

        private bool AreAllBotsStopped()
        {
            foreach (var controller in FLP_Bots.Controls.OfType<BotController>())
            {
                var state = controller.ReadBotState();
                if (state != "STOPPED" && state != "IDLE")
                    return false;
            }
            return true;
        }

        private async Task StartBotsStaggeredAsync()
        {
            var controllers = FLP_Bots.Controls.OfType<BotController>().ToList();

            if (controllers.Count == 0)
            {
                SendAll(BotControlCommand.Start);
                return;
            }

            // Start bots in groups with delays to prevent overwhelming the system
            const int batchSize = 3;
            const int delayBetweenBatches = 2000; // 2 seconds between batches

            for (int i = 0; i < controllers.Count; i += batchSize)
            {
                var batch = controllers.Skip(i).Take(batchSize);
                foreach (var controller in batch)
                {
                    controller.SendCommand(BotControlCommand.Start, false);
                }

                if (i + batchSize < controllers.Count)
                {
                    await Task.Delay(delayBetweenBatches).ConfigureAwait(false);
                }
            }

            LogUtil.LogText($"已分批启动 {controllers.Count} 个机器人");
        }

        #endregion
    }

    public sealed class SearchManager
    {
        private readonly RichTextBox _textBox;
        private readonly Label _statusLabel;
        private readonly List<SearchMatch> _matches = [];
        private int _currentIndex = -1;
        private string _lastSearchText = string.Empty;
        private bool _caseSensitive = false;
        private bool _useRegex = false;
        private bool _wholeWord = false;

        private readonly Color HighlightColor = Color.FromArgb(102, 192, 244);
        private readonly Color CurrentHighlightColor = Color.FromArgb(57, 255, 221);

        public SearchManager(RichTextBox textBox, Label statusLabel)
        {
            _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
        }

        public void UpdateSearch(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                ClearSearch();
                return;
            }

            if (searchText == _lastSearchText)
                return;

            _lastSearchText = searchText;
            PerformSearch(searchText);
        }

        public void FindNext()
        {
            if (_matches.Count == 0)
                return;

            _currentIndex = (_currentIndex + 1) % _matches.Count;
            HighlightCurrentMatch();
        }

        public void FindPrevious()
        {
            if (_matches.Count == 0)
                return;

            _currentIndex = _currentIndex == 0 ? _matches.Count - 1 : _currentIndex - 1;
            HighlightCurrentMatch();
        }

        public void ClearSearch()
        {
            ClearHighlights();
            _matches.Clear();
            _matches.TrimExcess(); // Free up memory
            _currentIndex = -1;
            _lastSearchText = string.Empty;
            _statusLabel.Text = string.Empty;
        }

        public void ToggleCaseSensitive()
        {
            _caseSensitive = !_caseSensitive;
            if (!string.IsNullOrEmpty(_lastSearchText))
                PerformSearch(_lastSearchText);
        }

        public void ToggleRegex()
        {
            _useRegex = !_useRegex;
            if (!string.IsNullOrEmpty(_lastSearchText))
                PerformSearch(_lastSearchText);
        }

        public void ToggleWholeWord()
        {
            _wholeWord = !_wholeWord;
            if (!string.IsNullOrEmpty(_lastSearchText))
                PerformSearch(_lastSearchText);
        }

        private void PerformSearch(string searchText)
        {
            ClearHighlights();
            _matches.Clear();
            _currentIndex = -1;

            if (string.IsNullOrEmpty(searchText))
            {
                _statusLabel.Text = string.Empty;
                return;
            }

            try
            {
                var text = _textBox.Text;
                var matches = _useRegex ? FindRegexMatches(text, searchText) : FindTextMatches(text, searchText);

                _matches.AddRange(matches);

                if (_matches.Count > 0)
                {
                    HighlightAllMatches();
                    _currentIndex = 0;
                    HighlightCurrentMatch();
                    _statusLabel.Text = $"第 1 个，共 {_matches.Count} 个";
                }
                else
                {
                    _statusLabel.Text = "未找到匹配项";
                }
            }
            catch (ArgumentException)
            {
                _statusLabel.Text = "无效的正则表达式模式";
            }
        }

        private IEnumerable<SearchMatch> FindTextMatches(string text, string searchText)
        {
            var comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var searchPattern = _wholeWord ? $@"\b{Regex.Escape(searchText)}\b" : searchText;

            if (_wholeWord)
            {
                var regex = new Regex(searchPattern, _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                return regex.Matches(text).Cast<Match>()
                    .Select(m => new SearchMatch(m.Index, m.Length));
            }

            var matches = new List<SearchMatch>();
            int index = 0;
            while ((index = text.IndexOf(searchText, index, comparison)) != -1)
            {
                matches.Add(new SearchMatch(index, searchText.Length));
                index += searchText.Length;
            }
            return matches;
        }

        private IEnumerable<SearchMatch> FindRegexMatches(string text, string pattern)
        {
            var options = _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(pattern, options);
            return regex.Matches(text).Cast<Match>()
                .Select(m => new SearchMatch(m.Index, m.Length));
        }

        private void HighlightAllMatches()
        {
            foreach (var match in _matches)
            {
                _textBox.Select(match.Start, match.Length);
                _textBox.SelectionBackColor = HighlightColor;
            }
        }

        private void HighlightCurrentMatch()
        {
            if (_currentIndex < 0 || _currentIndex >= _matches.Count)
                return;

            ClearCurrentHighlight();

            var currentMatch = _matches[_currentIndex];
            _textBox.Select(currentMatch.Start, currentMatch.Length);
            _textBox.SelectionBackColor = CurrentHighlightColor;
            _textBox.ScrollToCaret();

            _statusLabel.Text = $"第 {_currentIndex + 1} 个，共 {_matches.Count} 个";
        }

        private void ClearCurrentHighlight()
        {
            if (_currentIndex >= 0 && _currentIndex < _matches.Count)
            {
                var match = _matches[_currentIndex];
                _textBox.Select(match.Start, match.Length);
                _textBox.SelectionBackColor = HighlightColor;
            }
        }

        private void ClearHighlights()
        {
            _textBox.SelectAll();
            _textBox.SelectionBackColor = _textBox.BackColor;
            _textBox.DeselectAll();
        }
    }

    public readonly record struct SearchMatch(int Start, int Length);
}
