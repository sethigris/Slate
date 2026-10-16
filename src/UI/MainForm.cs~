using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Slate.Configuration;
using Slate.Core;
using Slate.Infrastructure;
using Slate.Serialization;
using Slate.Services;
using WinTimer = System.Windows.Forms.Timer;

namespace Slate.UI
{
    public sealed class MainForm : Form
    {
        private static readonly Color WindowColor = Color.FromArgb(246, 246, 248);
        private static readonly Color SidebarColor = Color.FromArgb(242, 242, 247);
        private static readonly Color CardColor = Color.White;
        private static readonly Color TextColor = Color.FromArgb(29, 29, 31);
        private static readonly Color SecondaryColor = Color.FromArgb(110, 110, 115);
        private static readonly Color LineColor = Color.FromArgb(225, 225, 230);
        private static readonly Color AccentColor = Color.FromArgb(0, 122, 255);
        private static readonly Color GreenColor = Color.FromArgb(36, 138, 61);
        private static readonly Color RedColor = Color.FromArgb(215, 54, 55);
        private static readonly Color AmberColor = Color.FromArgb(176, 111, 0);

        private readonly string _baseDirectory;
        private readonly string _configPath;
        private readonly WinTimer _healthTimer;
        private readonly Dictionary<string, Button> _navigation;
        private readonly SystemInfoProvider _systemProvider;
        private readonly ProcessProvider _processProvider;
        private readonly DriveProvider _driveProvider;
        private readonly WindowsServiceProvider _serviceProvider;

        private AppConfig _config;
        private FileLogger _logger;
        private ApiServer _server;
        private bool _configCreated;
        private bool _revealedKey;
        private bool _closing;
        private bool _manuallyStopped;
        private bool _apiOnline;
        private bool _apiResponding;
        private int _serverGeneration;
        private int _healthCheckActive;
        private string _startupError;
        private string _healthMessage;
        private DateTime _lastHealthCheck;
        private string _currentPage;

        private Panel _sidebar;
        private Panel _mainArea;
        private Panel _header;
        private Panel _pageHost;
        private Label _headerTitle;
        private Label _headerSubtitle;
        private Label _headerStatus;
        private Label _sidebarStatus;
        private Label _overviewStatus;
        private Label _overviewDescription;
        private Label _overviewUrl;
        private Label _lastCheckLabel;
        private Button _overviewAction;
        private Label _metricProcesses;
        private Label _metricDrives;
        private Label _metricServices;
        private Label _metricUptime;
        private TextBox _apiKeyBox;
        private Button _revealKeyButton;
        private FlowLayoutPanel _pageStack;

        public MainForm()
        {
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _configPath = Path.Combine(_baseDirectory, "slate.config.json");
            _navigation = new Dictionary<string, Button>();
            _systemProvider = new SystemInfoProvider();
            _processProvider = new ProcessProvider();
            _driveProvider = new DriveProvider();
            _serviceProvider = new WindowsServiceProvider();
            _currentPage = "Overview";
            _healthMessage = "Waiting for the first health check.";

            Directory.SetCurrentDirectory(_baseDirectory);
            InitializeWindow();
            BuildShell();

            _healthTimer = new WinTimer();
            _healthTimer.Interval = 4000;
            _healthTimer.Tick += delegate(object sender, EventArgs eventArgs)
            {
                BeginHealthCheck();
            };

            Shown += delegate(object sender, EventArgs eventArgs)
            {
                ShowPage("Overview");
                TryStartApi(false);
                _healthTimer.Start();
                BeginHealthCheck();
                RefreshDashboard();
            };

            FormClosing += delegate(object sender, FormClosingEventArgs eventArgs)
            {
                _closing = true;
                _healthTimer.Stop();
                StopApi(false);
                if (_logger != null)
                {
                    _logger.Dispose();
                    _logger = null;
                }
            };
        }

        private void InitializeWindow()
        {
            Text = "Slate";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 650);
            Size = new Size(1210, 790);
            BackColor = WindowColor;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Font;
            DoubleBuffered = true;
        }

        private void BuildShell()
        {
            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = WindowColor;
            Controls.Add(root);

            _mainArea = new Panel();
            _mainArea.Dock = DockStyle.Fill;
            _mainArea.BackColor = WindowColor;

            _header = new Panel();
            _header.Dock = DockStyle.Top;
            _header.Height = 84;
            _header.BackColor = Color.White;
            _header.Paint += DrawBottomRule;

            _headerTitle = MakeLabel("Overview", 19F, true, TextColor);
            _headerTitle.Location = new Point(28, 14);
            _headerTitle.AutoSize = true;
            _header.Controls.Add(_headerTitle);

            _headerSubtitle = MakeLabel("A clear view of your local system API.", 9F, false, SecondaryColor);
            _headerSubtitle.Location = new Point(30, 47);
            _headerSubtitle.AutoSize = true;
            _header.Controls.Add(_headerSubtitle);

            _headerStatus = MakeLabel("● STARTING", 9F, true, AmberColor);
            _headerStatus.TextAlign = ContentAlignment.MiddleCenter;
            _headerStatus.BackColor = Color.FromArgb(255, 246, 222);
            _headerStatus.Size = new Size(132, 31);
            _headerStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _headerStatus.Location = new Point(Width - 395, 26);
            _header.Controls.Add(_headerStatus);
            _header.Resize += delegate(object sender, EventArgs eventArgs)
            {
                _headerStatus.Location = new Point(_header.ClientSize.Width - _headerStatus.Width - 28, 26);
            };

            _pageHost = new Panel();
            _pageHost.Dock = DockStyle.Fill;
            _pageHost.BackColor = WindowColor;

            _mainArea.Controls.Add(_pageHost);
            _mainArea.Controls.Add(_header);

            BuildSidebar();
            root.Controls.Add(_mainArea);
            root.Controls.Add(_sidebar);
        }

        private void BuildSidebar()
        {
            _sidebar = new Panel();
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = 224;
            _sidebar.BackColor = SidebarColor;
            _sidebar.Paint += DrawRightRule;

            Panel logo = new Panel();
            logo.Location = new Point(21, 22);
            logo.Size = new Size(42, 42);
            logo.BackColor = TextColor;
            logo.Paint += delegate(object sender, PaintEventArgs eventArgs)
            {
                Rectangle rectangle = new Rectangle(0, 0, 41, 41);
                using (Pen pen = new Pen(TextColor, 1F))
                {
                    eventArgs.Graphics.DrawRectangle(pen, rectangle);
                }
                using (Font font = new Font("Segoe UI", 21F, FontStyle.Bold, GraphicsUnit.Point))
                using (Brush brush = new SolidBrush(Color.White))
                {
                    eventArgs.Graphics.DrawString("S", font, brush, new PointF(9F, 3F));
                }
            };
            _sidebar.Controls.Add(logo);

            Label brand = MakeLabel("slate", 22F, true, TextColor);
            brand.Location = new Point(73, 19);
            brand.AutoSize = true;
            _sidebar.Controls.Add(brand);

            Label brandSub = MakeLabel("SYSTEM API", 8F, true, SecondaryColor);
            brandSub.Location = new Point(75, 49);
            brandSub.AutoSize = true;
            _sidebar.Controls.Add(brandSub);

            Label section = MakeLabel("WORKSPACE", 8F, true, SecondaryColor);
            section.Location = new Point(24, 103);
            section.AutoSize = true;
            _sidebar.Controls.Add(section);

            AddNavigation("Overview", "Overview", 132);
            AddNavigation("System", "System", 179);
            AddNavigation("Processes", "Processes", 226);
            AddNavigation("Storage", "Storage", 273);
            AddNavigation("Services", "Services", 320);
            AddNavigation("Diagnostics", "Diagnostics", 367);
            AddNavigation("API access", "API access", 414);
            AddNavigation("Activity log", "Activity log", 461);

            Panel bottom = new Panel();
            bottom.Height = 82;
            bottom.Dock = DockStyle.Bottom;
            bottom.BackColor = SidebarColor;
            bottom.Padding = new Padding(23, 10, 12, 8);

            _sidebarStatus = MakeLabel("● Checking local API", 9F, true, AmberColor);
            _sidebarStatus.Location = new Point(22, 11);
            _sidebarStatus.AutoSize = true;
            bottom.Controls.Add(_sidebarStatus);

            Label version = MakeLabel("Version " + Program.Version + "  ·  Loopback only", 8F, false, SecondaryColor);
            version.Location = new Point(23, 38);
            version.AutoSize = true;
            bottom.Controls.Add(version);
            _sidebar.Controls.Add(bottom);
        }

        private void AddNavigation(string page, string caption, int top)
        {
            Button button = new Button();
            button.Text = caption;
            button.Location = new Point(13, top);
            button.Size = new Size(198, 40);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(13, 0, 0, 0);
            button.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            button.BackColor = SidebarColor;
            button.ForeColor = TextColor;
            button.Click += delegate(object sender, EventArgs eventArgs)
            {
                ShowPage(page);
            };
            _navigation[page] = button;
            _sidebar.Controls.Add(button);
        }

        private void ShowPage(string page)
        {
            _currentPage = page;
            Button nav;
            foreach (KeyValuePair<string, Button> pair in _navigation)
            {
                nav = pair.Value;
                bool selected = String.Equals(pair.Key, page, StringComparison.OrdinalIgnoreCase);
                nav.BackColor = selected ? Color.White : SidebarColor;
                nav.ForeColor = selected ? AccentColor : TextColor;
                nav.Font = new Font("Segoe UI", 9.5F, selected ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            }

            _headerTitle.Text = page;
            _headerSubtitle.Text = GetPageSubtitle(page);

            Control[] oldControls = new Control[_pageHost.Controls.Count];
            _pageHost.Controls.CopyTo(oldControls, 0);
            _pageHost.Controls.Clear();
            int index;
            for (index = 0; index < oldControls.Length; index++)
            {
                oldControls[index].Dispose();
            }

            _pageStack = new FlowLayoutPanel();
            _pageStack.Dock = DockStyle.Fill;
            _pageStack.AutoScroll = true;
            _pageStack.WrapContents = false;
            _pageStack.FlowDirection = FlowDirection.TopDown;
            _pageStack.Padding = new Padding(24, 22, 24, 26);
            _pageStack.BackColor = WindowColor;
            _pageStack.SizeChanged += delegate(object sender, EventArgs eventArgs)
            {
                ResizePageCards();
            };
            _pageHost.Controls.Add(_pageStack);

            try
            {
                if (page == "Overview")
                {
                    BuildOverviewPage();
                }
                else if (page == "System")
                {
                    BuildSystemPage();
                }
                else if (page == "Processes")
                {
                    BuildProcessesPage(String.Empty);
                }
                else if (page == "Storage")
                {
                    BuildDrivesPage();
                }
                else if (page == "Services")
                {
                    BuildServicesPage(String.Empty);
                }
                else if (page == "Diagnostics")
                {
                    BuildDiagnosticsPage();
                }
                else if (page == "API access")
                {
                    BuildApiAccessPage();
                }
                else if (page == "Activity log")
                {
                    BuildActivityPage();
                }
            }
            catch (Exception exception)
            {
                AddErrorCard("This view could not be loaded", exception.Message);
            }
            ResizePageCards();
            UpdateStatusVisuals();
            if (page == "Overview")
            {
                RefreshDashboard();
            }
        }

        private string GetPageSubtitle(string page)
        {
            if (page == "Overview") return "A clear view of your local system API.";
            if (page == "System") return "Hardware, operating system, and runtime details.";
            if (page == "Processes") return "Inspect the processes currently visible to your account.";
            if (page == "Storage") return "Check attached drives and available disk space.";
            if (page == "Services") return "Review Windows service status without changing services.";
            if (page == "Diagnostics") return "A combined snapshot of the current machine.";
            if (page == "API access") return "Manage your local API key and integration details.";
            return "Recent local API activity and request outcomes.";
        }

        private void ResizePageCards()
        {
            if (_pageStack == null || _pageStack.IsDisposed)
            {
                return;
            }

            int availableWidth = _pageStack.ClientSize.Width - _pageStack.Padding.Horizontal - 28;
            if (_pageStack.VerticalScroll.Visible)
            {
                availableWidth -= SystemInformation.VerticalScrollBarWidth;
            }
            availableWidth = Math.Max(540, availableWidth);
            int index;
            for (index = 0; index < _pageStack.Controls.Count; index++)
            {
                Control control = _pageStack.Controls[index];
                control.Width = availableWidth;
                if (control is TableLayoutPanel)
                {
                    control.Width = availableWidth;
                }
            }
        }

        private void BuildOverviewPage()
        {
            Panel hero = CreateCard(220);
            Label eyebrow = MakeLabel("SLATE  /  LOCAL SYSTEM API", 8F, true, AccentColor);
            eyebrow.Location = new Point(23, 21);
            eyebrow.AutoSize = true;
            hero.Controls.Add(eyebrow);

            _overviewStatus = MakeLabel("Starting Slate…", 21F, true, TextColor);
            _overviewStatus.Location = new Point(22, 49);
            _overviewStatus.AutoSize = true;
            hero.Controls.Add(_overviewStatus);

            _overviewDescription = MakeLabel("The API is initializing. You can use this window to inspect your system and manage local API access.", 9.5F, false, SecondaryColor);
            _overviewDescription.Location = new Point(24, 88);
            _overviewDescription.Size = new Size(660, 34);
            _overviewDescription.AutoSize = false;
            _overviewDescription.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            hero.Controls.Add(_overviewDescription);

            Label urlCaption = MakeLabel("LOCAL ADDRESS", 8F, true, SecondaryColor);
            urlCaption.Location = new Point(24, 143);
            urlCaption.AutoSize = true;
            hero.Controls.Add(urlCaption);

            _overviewUrl = MakeLabel(GetBaseUrl(), 10F, false, TextColor);
            _overviewUrl.Location = new Point(24, 164);
            _overviewUrl.AutoSize = true;
            hero.Controls.Add(_overviewUrl);

            Button copyUrl = CreateButton("Copy address", 112, 34, Color.FromArgb(242, 242, 247), TextColor);
            copyUrl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            copyUrl.Location = new Point(hero.Width - copyUrl.Width - 22, 153);
            copyUrl.Click += delegate(object sender, EventArgs eventArgs)
            {
                CopyToClipboard(GetBaseUrl(), "Local API address copied.");
            };
            hero.Controls.Add(copyUrl);

            _overviewAction = CreateButton("Stop API", 146, 38, AccentColor, Color.White);
            _overviewAction.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _overviewAction.Location = new Point(hero.Width - _overviewAction.Width - 22, 25);
            _overviewAction.Click += delegate(object sender, EventArgs eventArgs)
            {
                if (_server == null)
                {
                    if (IsUrlAclError(_startupError))
                    {
                        FixUrlPermission();
                    }
                    else
                    {
                        TryStartApi(true);
                    }
                }
                else
                {
                    StopApi(true);
                }
            };
            hero.Controls.Add(_overviewAction);

            _pageStack.Controls.Add(hero);

            TableLayoutPanel metrics = new TableLayoutPanel();
            metrics.Height = 122;
            metrics.Margin = new Padding(0, 0, 0, 16);
            metrics.ColumnCount = 4;
            metrics.RowCount = 1;
            metrics.BackColor = WindowColor;
            metrics.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            int col;
            for (col = 0; col < 4; col++)
            {
                metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }
            metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _metricProcesses = MakeLabel("—", 23F, true, TextColor);
            _metricDrives = MakeLabel("—", 23F, true, TextColor);
            _metricServices = MakeLabel("—", 23F, true, TextColor);
            _metricUptime = MakeLabel("—", 17F, true, TextColor);
            metrics.Controls.Add(CreateMetricCard("PROCESSES", _metricProcesses, "Visible to your account"), 0, 0);
            metrics.Controls.Add(CreateMetricCard("READY DRIVES", _metricDrives, "Mounted and available"), 1, 0);
            metrics.Controls.Add(CreateMetricCard("WINDOWS SERVICES", _metricServices, "Installed service total"), 2, 0);
            metrics.Controls.Add(CreateMetricCard("WINDOWS UPTIME", _metricUptime, "Since last restart"), 3, 0);
            _pageStack.Controls.Add(metrics);

            Panel quick = CreateCard(152);
            Label quickTitle = MakeLabel("Everything in one place", 13F, true, TextColor);
            quickTitle.Location = new Point(22, 18);
            quickTitle.AutoSize = true;
            quick.Controls.Add(quickTitle);
            Label quickBody = MakeLabel("Browse system details, processes, drive capacity, Windows services, diagnostics, and API access from the sidebar. Slate also keeps the HTTP API available to trusted local tools while it is running.", 9.5F, false, SecondaryColor);
            quickBody.Location = new Point(23, 48);
            quickBody.Size = new Size(850, 45);
            quickBody.AutoSize = false;
            quickBody.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            quick.Controls.Add(quickBody);
            _lastCheckLabel = MakeLabel("Health check has not run yet.", 8.5F, false, SecondaryColor);
            _lastCheckLabel.Location = new Point(23, 108);
            _lastCheckLabel.AutoSize = true;
            quick.Controls.Add(_lastCheckLabel);
            _pageStack.Controls.Add(quick);

            if (!String.IsNullOrEmpty(_startupError))
            {
                AddErrorCard("Slate needs attention", FriendlyStartupError(_startupError));
            }
        }

        private Control CreateMetricCard(string caption, Label value, string description)
        {
            Panel card = new Panel();
            card.Height = 108;
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 12, 0);
            card.BackColor = CardColor;
            card.Paint += DrawCardRule;

            Label heading = MakeLabel(caption, 7.5F, true, SecondaryColor);
            heading.Location = new Point(15, 13);
            heading.AutoSize = true;
            card.Controls.Add(heading);

            value.Location = new Point(14, 34);
            value.AutoSize = true;
            card.Controls.Add(value);

            Label detail = MakeLabel(description, 7.5F, false, SecondaryColor);
            detail.Location = new Point(15, 78);
            detail.AutoSize = true;
            card.Controls.Add(detail);
            return card;
        }

        private void BuildSystemPage()
        {
            IDictionary<string, object> info = _systemProvider.GetSystemInfo();
            DataGridView grid = CreateGrid();
            AddColumn(grid, "property", "PROPERTY", 36F);
            AddColumn(grid, "value", "VALUE", 64F);
            AddFlattenedRows(grid, String.Empty, info);
            Panel card = CreateCard(560);
            grid.Dock = DockStyle.Fill;
            card.Controls.Add(grid);
            _pageStack.Controls.Add(card);
        }

        private void BuildProcessesPage(string filter)
        {
            Panel card = CreateCard(570);
            DataGridView grid = CreateGrid();
            AddColumn(grid, "pid", "PID", 12F);
            AddColumn(grid, "name", "PROCESS", 28F);
            AddColumn(grid, "memory", "MEMORY", 18F);
            AddColumn(grid, "private", "PRIVATE MEMORY", 18F);
            AddColumn(grid, "threads", "THREADS", 12F);
            AddColumn(grid, "started", "STARTED (UTC)", 28F);

            IDictionary<string, object> result = _processProvider.ListProcesses(250, filter);
            object raw;
            IList<IDictionary<string, object>> rows = result.TryGetValue("processes", out raw)
                ? raw as IList<IDictionary<string, object>>
                : null;
            if (rows != null)
            {
                int index;
                for (index = 0; index < rows.Count; index++)
                {
                    IDictionary<string, object> item = rows[index];
                    grid.Rows.Add(
                        GetString(item, "id"),
                        GetString(item, "name"),
                        FormatBytes(GetLong(item, "workingSetBytes")),
                        FormatBytes(GetLong(item, "privateMemoryBytes")),
                        GetString(item, "threadCount"),
                        FormatTimestamp(GetString(item, "startTimeUtc")));
                }
            }

            Panel toolbar = BuildFilterToolbar(filter, "Filter processes by name", delegate(string value)
            {
                BuildFilteredPageReplace(delegate { BuildProcessesPage(value); });
            }, delegate
            {
                BuildFilteredPageReplace(delegate { BuildProcessesPage(filter); });
            });
            card.Controls.Add(grid);
            card.Controls.Add(toolbar);
            _pageStack.Controls.Add(card);
        }

        private void BuildDrivesPage()
        {
            Panel card = CreateCard(570);
            DataGridView grid = CreateGrid();
            AddColumn(grid, "drive", "DRIVE", 12F);
            AddColumn(grid, "type", "TYPE", 15F);
            AddColumn(grid, "volume", "VOLUME LABEL", 22F);
            AddColumn(grid, "status", "STATUS", 13F);
            AddColumn(grid, "total", "TOTAL", 16F);
            AddColumn(grid, "free", "AVAILABLE", 16F);
            AddColumn(grid, "used", "USED", 12F);

            IDictionary<string, object> result = _driveProvider.ListDrives();
            object raw;
            IList<IDictionary<string, object>> rows = result.TryGetValue("drives", out raw)
                ? raw as IList<IDictionary<string, object>>
                : null;
            if (rows != null)
            {
                int index;
                for (index = 0; index < rows.Count; index++)
                {
                    IDictionary<string, object> item = rows[index];
                    bool ready = GetBool(item, "isReady");
                    grid.Rows.Add(
                        GetString(item, "name"),
                        GetString(item, "driveType"),
                        GetString(item, "volumeLabel"),
                        ready ? "Ready" : "Unavailable",
                        ready ? FormatBytes(GetLong(item, "totalBytes")) : "—",
                        ready ? FormatBytes(GetLong(item, "availableBytes")) : "—",
                        ready ? GetString(item, "usedPercent") + "%" : "—");
                }
            }

            Button refresh = CreateButton("Refresh", 92, 32, Color.FromArgb(242, 242, 247), TextColor);
            refresh.Location = new Point(20, 10);
            refresh.Click += delegate(object sender, EventArgs eventArgs) { ShowPage("Storage"); };
            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 50;
            toolbar.BackColor = Color.White;
            toolbar.Controls.Add(refresh);
            card.Controls.Add(grid);
            card.Controls.Add(toolbar);
            _pageStack.Controls.Add(card);
        }

        private void BuildServicesPage(string filter)
        {
            Panel card = CreateCard(570);
            DataGridView grid = CreateGrid();
            AddColumn(grid, "name", "SERVICE NAME", 27F);
            AddColumn(grid, "displayName", "DISPLAY NAME", 34F);
            AddColumn(grid, "status", "STATUS", 15F);
            AddColumn(grid, "type", "TYPE", 18F);
            AddColumn(grid, "canStop", "CAN STOP", 11F);

            IDictionary<string, object> result = _serviceProvider.ListServices(500, filter);
            object raw;
            IList<IDictionary<string, object>> rows = result.TryGetValue("services", out raw)
                ? raw as IList<IDictionary<string, object>>
                : null;
            if (rows != null)
            {
                int index;
                for (index = 0; index < rows.Count; index++)
                {
                    IDictionary<string, object> item = rows[index];
                    grid.Rows.Add(
                        GetString(item, "name"),
                        GetString(item, "displayName"),
                        GetString(item, "status"),
                        GetString(item, "serviceType"),
                        GetBool(item, "canStop") ? "Yes" : "No");
                }
            }

            Panel toolbar = BuildFilterToolbar(filter, "Filter services by name", delegate(string value)
            {
                BuildFilteredPageReplace(delegate { BuildServicesPage(value); });
            }, delegate
            {
                BuildFilteredPageReplace(delegate { BuildServicesPage(filter); });
            });
            card.Controls.Add(grid);
            card.Controls.Add(toolbar);
            _pageStack.Controls.Add(card);
        }

        private Panel BuildFilterToolbar(string filter, string prompt, Action<string> searchAction, Action refreshAction)
        {
            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 52;
            toolbar.BackColor = Color.White;

            TextBox search = new TextBox();
            search.Text = String.IsNullOrEmpty(filter) ? prompt : filter;
            search.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            search.BorderStyle = BorderStyle.FixedSingle;
            search.Location = new Point(20, 12);
            search.Size = new Size(260, 28);
            search.ForeColor = String.IsNullOrEmpty(filter) ? SecondaryColor : TextColor;
            search.Tag = prompt;
            search.AccessibleName = prompt;
            search.Enter += delegate(object sender, EventArgs eventArgs)
            {
                if (String.Equals(search.Text, prompt, StringComparison.Ordinal))
                {
                    search.Text = String.Empty;
                    search.ForeColor = TextColor;
                }
            };
            search.Leave += delegate(object sender, EventArgs eventArgs)
            {
                if (String.IsNullOrWhiteSpace(search.Text))
                {
                    search.Text = prompt;
                    search.ForeColor = SecondaryColor;
                }
            };
            toolbar.Controls.Add(search);

            Button find = CreateButton("Search", 76, 30, Color.FromArgb(242, 242, 247), TextColor);
            find.Location = new Point(291, 10);
            find.Click += delegate(object sender, EventArgs eventArgs)
            {
                searchAction(String.Equals(search.Text, prompt, StringComparison.Ordinal) ? String.Empty : search.Text.Trim());
            };
            toolbar.Controls.Add(find);

            Button refresh = CreateButton("Refresh", 82, 30, Color.FromArgb(242, 242, 247), TextColor);
            refresh.Location = new Point(377, 10);
            refresh.Click += delegate(object sender, EventArgs eventArgs)
            {
                refreshAction();
            };
            toolbar.Controls.Add(refresh);
            search.KeyDown += delegate(object sender, KeyEventArgs eventArgs)
            {
                if (eventArgs.KeyCode == Keys.Enter)
                {
                    searchAction(String.Equals(search.Text, prompt, StringComparison.Ordinal) ? String.Empty : search.Text.Trim());
                    eventArgs.SuppressKeyPress = true;
                }
            };
            return toolbar;
        }

        private void BuildFilteredPageReplace(Action buildAction)
        {
            Control[] oldControls = new Control[_pageStack.Controls.Count];
            _pageStack.Controls.CopyTo(oldControls, 0);
            _pageStack.Controls.Clear();
            int index;
            for (index = 0; index < oldControls.Length; index++)
            {
                oldControls[index].Dispose();
            }
            buildAction();
            ResizePageCards();
        }

        private void BuildDiagnosticsPage()
        {
            Panel card = CreateCard(570);
            TextBox output = CreateOutputBox();
            output.Dock = DockStyle.Fill;
            RefreshDiagnostics(output);

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 52;
            toolbar.BackColor = Color.White;
            Label caption = MakeLabel("READ-ONLY SNAPSHOT", 8F, true, SecondaryColor);
            caption.Location = new Point(22, 17);
            caption.AutoSize = true;
            toolbar.Controls.Add(caption);
            Button refresh = CreateButton("Refresh snapshot", 132, 31, Color.FromArgb(242, 242, 247), TextColor);
            refresh.Location = new Point(156, 10);
            refresh.Click += delegate(object sender, EventArgs eventArgs) { RefreshDiagnostics(output); };
            toolbar.Controls.Add(refresh);

            card.Controls.Add(output);
            card.Controls.Add(toolbar);
            _pageStack.Controls.Add(card);
        }

        private void RefreshDiagnostics(TextBox output)
        {
            try
            {
                DiagnosticsProvider provider = new DiagnosticsProvider(
                    _systemProvider, _processProvider, _driveProvider, _serviceProvider);
                IDictionary<string, object> result = provider.GetSnapshot(false);
                output.Text = PrettyJson(JsonUtil.Serialize(result));
            }
            catch (Exception exception)
            {
                output.Text = "Diagnostics could not be collected." + Environment.NewLine + exception.Message;
            }
        }

        private void BuildApiAccessPage()
        {
            Panel addressCard = CreateCard(166);
            AddSectionHeading(addressCard, "Local API address", "This address is available only on this PC.", 22, 20);
            TextBox url = CreateReadOnlyLine(GetBaseUrl());
            url.Location = new Point(23, 76);
            url.Size = new Size(550, 30);
            url.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            addressCard.Controls.Add(url);
            Button copyAddress = CreateButton("Copy address", 112, 32, Color.FromArgb(242, 242, 247), TextColor);
            copyAddress.Location = new Point(23, 116);
            copyAddress.Click += delegate(object sender, EventArgs eventArgs)
            {
                CopyToClipboard(GetBaseUrl(), "Local API address copied.");
            };
            addressCard.Controls.Add(copyAddress);
            _pageStack.Controls.Add(addressCard);

            Panel keyCard = CreateCard(214);
            AddSectionHeading(keyCard, "Private API key", "Other local tools use this key in an Authorization: Bearer header.", 22, 20);
            _apiKeyBox = CreateReadOnlyLine(_config == null ? "Configuration not available" : _config.ApiKey);
            _apiKeyBox.Location = new Point(23, 77);
            _apiKeyBox.Size = new Size(550, 30);
            _apiKeyBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _apiKeyBox.UseSystemPasswordChar = !_revealedKey;
            keyCard.Controls.Add(_apiKeyBox);

            _revealKeyButton = CreateButton(_revealedKey ? "Hide key" : "Reveal key", 94, 32, Color.FromArgb(242, 242, 247), TextColor);
            _revealKeyButton.Location = new Point(23, 119);
            _revealKeyButton.Click += delegate(object sender, EventArgs eventArgs)
            {
                _revealedKey = !_revealedKey;
                _apiKeyBox.UseSystemPasswordChar = !_revealedKey;
                _revealKeyButton.Text = _revealedKey ? "Hide key" : "Reveal key";
            };
            keyCard.Controls.Add(_revealKeyButton);

            Button copyKey = CreateButton("Copy key", 86, 32, AccentColor, Color.White);
            copyKey.Location = new Point(125, 119);
            copyKey.Click += delegate(object sender, EventArgs eventArgs)
            {
                if (_config != null)
                {
                    CopyToClipboard(_config.ApiKey, "API key copied. Keep it private.");
                }
            };
            keyCard.Controls.Add(copyKey);

            Button rotateKey = CreateButton("Generate new key", 136, 32, Color.FromArgb(255, 242, 242), RedColor);
            rotateKey.Location = new Point(220, 119);
            rotateKey.Click += delegate(object sender, EventArgs eventArgs) { RotateApiKey(); };
            keyCard.Controls.Add(rotateKey);
            _pageStack.Controls.Add(keyCard);

            Panel endpointsCard = CreateCard(250);
            AddSectionHeading(endpointsCard, "Available endpoints", "Read-only requests; each route requires the API key.", 22, 20);
            string[] endpointLines = new string[]
            {
                "GET  /api/v1/health       Confirm that the API is responding",
                "GET  /api/v1/system       Operating system and uptime",
                "GET  /api/v1/processes    Running processes",
                "GET  /api/v1/drives       Drive space and usage",
                "GET  /api/v1/services     Windows service status",
                "GET  /api/v1/diagnostics  Combined diagnostic snapshot"
            };
            Label endpoints = MakeLabel(String.Join(Environment.NewLine, endpointLines), 9F, false, TextColor);
            endpoints.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            endpoints.Location = new Point(24, 68);
            endpoints.Size = new Size(800, 150);
            endpoints.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            endpoints.AutoSize = false;
            endpointsCard.Controls.Add(endpoints);
            _pageStack.Controls.Add(endpointsCard);

            if (_configCreated)
            {
                AddInfoCard("First-run setup complete", "Slate created its configuration and generated an API key. Use Reveal key or Copy key above to connect your local tools.");
                _configCreated = false;
            }
        }

        private void BuildActivityPage()
        {
            Panel card = CreateCard(570);
            TextBox output = CreateOutputBox();
            output.Dock = DockStyle.Fill;
            RefreshActivity(output);

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 52;
            toolbar.BackColor = Color.White;
            Label caption = MakeLabel("LATEST 300 LINES", 8F, true, SecondaryColor);
            caption.Location = new Point(22, 17);
            caption.AutoSize = true;
            toolbar.Controls.Add(caption);
            Button refresh = CreateButton("Refresh log", 105, 31, Color.FromArgb(242, 242, 247), TextColor);
            refresh.Location = new Point(145, 10);
            refresh.Click += delegate(object sender, EventArgs eventArgs) { RefreshActivity(output); };
            toolbar.Controls.Add(refresh);
            Button openFolder = CreateButton("Open app folder", 121, 31, Color.FromArgb(242, 242, 247), TextColor);
            openFolder.Location = new Point(258, 10);
            openFolder.Click += delegate(object sender, EventArgs eventArgs)
            {
                try
                {
                    Process.Start("explorer.exe", _baseDirectory);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, exception.Message, "Could not open folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            toolbar.Controls.Add(openFolder);
            card.Controls.Add(output);
            card.Controls.Add(toolbar);
            _pageStack.Controls.Add(card);
        }

        private void RefreshActivity(TextBox output)
        {
            try
            {
                string logPath = _config == null ? Path.Combine(_baseDirectory, "slate.log") : Path.GetFullPath(_config.LogFile);
                if (!File.Exists(logPath))
                {
                    output.Text = "No activity has been logged yet." + Environment.NewLine + logPath;
                    return;
                }
                string[] lines = File.ReadAllLines(logPath);
                int start = Math.Max(0, lines.Length - 300);
                StringBuilder builder = new StringBuilder();
                int index;
                for (index = start; index < lines.Length; index++)
                {
                    builder.AppendLine(lines[index]);
                }
                output.Text = builder.ToString();
                output.SelectionStart = output.TextLength;
                output.ScrollToCaret();
            }
            catch (Exception exception)
            {
                output.Text = "The log could not be read." + Environment.NewLine + exception.Message;
            }
        }

        private void TryStartApi(bool showFeedback)
        {
            _manuallyStopped = false;
            try
            {
                EnsureConfigured();
                if (_server != null)
                {
                    UpdateStatusVisuals();
                    return;
                }

                ApiServer candidate = new ApiServer(_config, _logger);
                try
                {
                    candidate.Start();
                    _server = candidate;
                    Interlocked.Increment(ref _serverGeneration);
                    _startupError = null;
                    _apiOnline = false;
                    _apiResponding = false;
                    _lastHealthCheck = DateTime.MinValue;
                    _healthMessage = "The API started; waiting for its first health response.";
                    if (_logger != null)
                    {
                        _logger.Info("Slate desktop interface started the local API.");
                    }
                }
                catch
                {
                    candidate.Dispose();
                    throw;
                }
                UpdateStatusVisuals();
                if (_currentPage == "Overview")
                {
                    RefreshDashboard();
                }
                BeginHealthCheck();
                if (showFeedback)
                {
                    _headerSubtitle.Text = "Slate is starting the local API.";
                }
            }
            catch (Exception exception)
            {
                _startupError = exception.Message;
                _apiOnline = false;
                _apiResponding = false;
                _healthMessage = FriendlyStartupError(exception.Message);
                UpdateStatusVisuals();
                if (showFeedback)
                {
                    MessageBox.Show(this, FriendlyStartupError(exception.Message), "Slate could not start", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void FixUrlPermission()
        {
            if (_config == null)
            {
                TryStartApi(true);
                return;
            }

            DialogResult answer = MessageBox.Show(
                this,
                "Slate needs Windows permission to listen on its local API address. The next step opens the built-in Windows network configuration tool with administrator approval; it grants this local URL to your Windows account only.",
                "Allow Slate to listen locally",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);
            if (answer != DialogResult.OK)
            {
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe");
                startInfo.Arguments = "http add urlacl url=" + _config.ListenPrefix +
                    " user=\"" + Environment.UserDomainName + "\\" + Environment.UserName + "\"";
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
                Process elevated = Process.Start(startInfo);
                if (elevated == null)
                {
                    throw new InvalidOperationException("Windows did not start the permission helper.");
                }

                _headerSubtitle.Text = "Waiting for Windows to finish the local permission setup…";
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    int exitCode = 1;
                    string waitError = null;
                    try
                    {
                        elevated.WaitForExit();
                        exitCode = elevated.ExitCode;
                        elevated.Close();
                    }
                    catch (Exception exception)
                    {
                        waitError = exception.Message;
                    }

                    if (!_closing && IsHandleCreated && !IsDisposed)
                    {
                        try
                        {
                            BeginInvoke(new Action(delegate
                            {
                                if (_closing) return;
                                if (exitCode == 0)
                                {
                                    _startupError = null;
                                    TryStartApi(true);
                                }
                                else
                                {
                                    _startupError = String.IsNullOrEmpty(waitError)
                                        ? "Windows URL permission setup failed with exit code " + exitCode.ToString(CultureInfo.InvariantCulture) + "."
                                        : "Could not verify Windows URL permission setup: " + waitError;
                                    UpdateStatusVisuals();
                                    MessageBox.Show(this, FriendlyStartupError(_startupError), "Permission setup did not complete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            }));
                        }
                        catch (InvalidOperationException)
                        {
                            // The user closed the app while Windows was configuring the URL.
                        }
                    }
                });
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                _headerSubtitle.Text = "Windows permission setup was cancelled or could not start.";
                MessageBox.Show(this, "Windows did not complete the administrator request. " + exception.Message,
                    "Permission setup unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Permission setup unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static bool IsUrlAclError(string message)
        {
            return !String.IsNullOrEmpty(message) &&
                (message.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void EnsureConfigured()
        {
            if (_config == null)
            {
                _config = ConfigManager.LoadOrCreate(_configPath, out _configCreated);
            }
            if (_logger == null)
            {
                string logPath = Path.IsPathRooted(_config.LogFile)
                    ? _config.LogFile
                    : Path.Combine(_baseDirectory, _config.LogFile);
                _config.LogFile = logPath;
                ConfigManager.Save(_configPath, _config);
                _logger = new FileLogger(_config.LogFile);
            }
        }

        private void StopApi(bool userInitiated)
        {
            if (_server != null)
            {
                try
                {
                    _server.Stop();
                    _server.Dispose();
                }
                catch (Exception exception)
                {
                    if (_logger != null)
                    {
                        _logger.Warn("API shutdown reported " + exception.GetType().Name + ".");
                    }
                }
                _server = null;
            }
            Interlocked.Increment(ref _serverGeneration);
            _apiOnline = false;
            _apiResponding = false;
            _manuallyStopped = userInitiated;
            _healthMessage = userInitiated ? "The API was stopped from Slate." : "The API is not running.";
            if (_logger != null && userInitiated)
            {
                _logger.Info("Slate desktop interface stopped the local API.");
            }
            UpdateStatusVisuals();
        }

        private void EnsureLoggerPath()
        {
            if (_config == null)
            {
                return;
            }
            if (!Path.IsPathRooted(_config.LogFile))
            {
                _config.LogFile = Path.Combine(_baseDirectory, _config.LogFile);
            }
        }

        private void BeginHealthCheck()
        {
            if (_closing)
            {
                return;
            }
            if (_server == null || _config == null)
            {
                UpdateStatusVisuals();
                return;
            }
            if (Interlocked.Exchange(ref _healthCheckActive, 1) != 0)
            {
                return;
            }

            string url = _config.ListenPrefix + "api/v1/health";
            string key = _config.ApiKey;
            int generation = Interlocked.CompareExchange(ref _serverGeneration, 0, 0);
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                bool healthy = false;
                bool responding = false;
                string message = "No response received from the health endpoint.";
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "GET";
                    request.Proxy = null;
                    request.Timeout = 1800;
                    request.ReadWriteTimeout = 1800;
                    request.Headers[HttpRequestHeader.Authorization] = "Bearer " + key;
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        IDictionary<string, object> body = JsonUtil.Deserialize<Dictionary<string, object>>(json);
                        object status;
                        responding = true;
                        healthy = response.StatusCode == HttpStatusCode.OK &&
                            body != null && body.TryGetValue("status", out status) &&
                            String.Equals(Convert.ToString(status, CultureInfo.InvariantCulture), "healthy", StringComparison.OrdinalIgnoreCase);
                        message = healthy ? "The health endpoint returned a healthy response." : "The health endpoint returned an unexpected response.";
                    }
                }
                catch (WebException exception)
                {
                    HttpWebResponse response = exception.Response as HttpWebResponse;
                    if (response != null)
                    {
                        responding = true;
                        message = "The API responded with HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + ".";
                        response.Close();
                    }
                    else
                    {
                        message = exception.Status == WebExceptionStatus.Timeout
                            ? "The health check timed out."
                            : "The API did not accept the connection.";
                    }
                }
                catch (Exception exception)
                {
                    message = exception.GetType().Name + ": " + exception.Message;
                }
                finally
                {
                    Interlocked.Exchange(ref _healthCheckActive, 0);
                }

                if (!_closing && IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            if (_closing || generation != Interlocked.CompareExchange(ref _serverGeneration, 0, 0))
                            {
                                return;
                            }
                            _apiOnline = healthy;
                            _apiResponding = responding;
                            _healthMessage = message;
                            _lastHealthCheck = DateTime.Now;
                            UpdateStatusVisuals();
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                        // The window can close between the background check and UI dispatch.
                    }
                }
            });
        }

        private void UpdateStatusVisuals()
        {
            string status;
            string description;
            Color color;
            Color soft;
            if (_server != null && _apiOnline)
            {
                status = "● API ONLINE";
                description = "Slate is responding and ready for authenticated local requests.";
                color = GreenColor;
                soft = Color.FromArgb(229, 247, 234);
            }
            else if (_server != null && _lastHealthCheck == DateTime.MinValue)
            {
                status = "● STARTING";
                description = "The local listener has started. Slate is waiting for the first health response.";
                color = AmberColor;
                soft = Color.FromArgb(255, 246, 222);
            }
            else if (_server != null && _apiResponding)
            {
                status = "● NEEDS ATTENTION";
                description = "The listener responded, but the health check was not valid. Check the API key and recent activity.";
                color = AmberColor;
                soft = Color.FromArgb(255, 246, 222);
            }
            else if (_server != null)
            {
                status = "● NO RESPONSE";
                description = "Slate is started, but its health endpoint is not responding. Check the activity log or restart the API.";
                color = RedColor;
                soft = Color.FromArgb(255, 235, 235);
            }
            else if (_manuallyStopped)
            {
                status = "● STOPPED";
                description = "The local API is stopped. Select Start API to make it available again.";
                color = SecondaryColor;
                soft = Color.FromArgb(235, 235, 240);
            }
            else if (!String.IsNullOrEmpty(_startupError))
            {
                status = "● NEEDS ATTENTION";
                description = FriendlyStartupError(_startupError);
                color = RedColor;
                soft = Color.FromArgb(255, 235, 235);
            }
            else
            {
                status = "● STARTING";
                description = "Starting the local API and checking its health endpoint.";
                color = AmberColor;
                soft = Color.FromArgb(255, 246, 222);
            }

            _headerStatus.Text = status;
            _headerStatus.ForeColor = color;
            _headerStatus.BackColor = soft;
            _sidebarStatus.Text = status;
            _sidebarStatus.ForeColor = color;
            if (_overviewStatus != null && !_overviewStatus.IsDisposed)
            {
                _overviewStatus.Text = (_server != null && _apiOnline) ? "Your system is ready." :
                    (_manuallyStopped ? "The API is stopped." :
                    (!String.IsNullOrEmpty(_startupError) ? "A setup step is needed." :
                    (_server != null && _lastHealthCheck == DateTime.MinValue ? "Starting local API…" :
                    (_server != null && !_apiResponding ? "The API is not responding." :
                    (_server != null ? "The API needs attention." : "Slate is offline.")))));
                _overviewStatus.ForeColor = (_server != null && _apiOnline) ? GreenColor :
                    ((_server == null && !_manuallyStopped) || (_server != null && _lastHealthCheck != DateTime.MinValue && !_apiResponding) ? RedColor : TextColor);
            }
            if (_overviewDescription != null && !_overviewDescription.IsDisposed)
            {
                _overviewDescription.Text = description;
            }
            if (_overviewUrl != null && !_overviewUrl.IsDisposed)
            {
                _overviewUrl.Text = GetBaseUrl();
            }
            if (_overviewAction != null && !_overviewAction.IsDisposed)
            {
                _overviewAction.Text = _server != null ? "Stop API" :
                    (IsUrlAclError(_startupError) ? "Fix permissions" : "Start API");
                _overviewAction.BackColor = _server == null ? AccentColor : Color.FromArgb(242, 242, 247);
                _overviewAction.ForeColor = _server == null ? Color.White : TextColor;
            }
            if (_lastCheckLabel != null && !_lastCheckLabel.IsDisposed)
            {
                string suffix = _lastHealthCheck == DateTime.MinValue ? "No health check recorded yet." : "Last checked " + _lastHealthCheck.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + ".";
                _lastCheckLabel.Text = suffix + " " + _healthMessage;
            }
        }

        private void RefreshDashboard()
        {
            try
            {
                IDictionary<string, object> system = _systemProvider.GetSystemInfo();
                int processCount = _processProvider.GetTotalProcessCount();
                int readyDriveCount = _driveProvider.GetReadyDriveCount();
                IDictionary<string, object> counts = _serviceProvider.GetServiceCounts();
                if (_metricProcesses != null && !_metricProcesses.IsDisposed) _metricProcesses.Text = processCount.ToString(CultureInfo.InvariantCulture);
                if (_metricDrives != null && !_metricDrives.IsDisposed) _metricDrives.Text = readyDriveCount.ToString(CultureInfo.InvariantCulture);
                if (_metricServices != null && !_metricServices.IsDisposed) _metricServices.Text = GetString(counts, "total");
                if (_metricUptime != null && !_metricUptime.IsDisposed) _metricUptime.Text = GetString(system, "uptime");
            }
            catch (Exception exception)
            {
                if (_lastCheckLabel != null && !_lastCheckLabel.IsDisposed)
                {
                    _lastCheckLabel.Text = "System summary could not be refreshed: " + exception.Message;
                }
            }
        }

        private void RotateApiKey()
        {
            if (_config == null)
            {
                MessageBox.Show(this, "Slate's configuration is not available yet.", "Cannot generate a key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult answer = MessageBox.Show(
                this,
                "Generate a new API key? Any scripts or tools using the current key must be updated.",
                "Replace API key",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _config.ApiKey = ApiKeyFactory.Create();
                ConfigManager.Save(_configPath, _config);
                _revealedKey = false;
                if (_server != null)
                {
                    StopApi(false);
                }
                TryStartApi(true);
                ShowPage("API access");
                _headerSubtitle.Text = "New key generated. Update any trusted local integrations.";
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Could not replace API key", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddErrorCard(string title, string message)
        {
            Panel card = CreateCard(126);
            Label heading = MakeLabel(title, 12F, true, RedColor);
            heading.Location = new Point(22, 18);
            heading.AutoSize = true;
            card.Controls.Add(heading);
            Label details = MakeLabel(message, 9F, false, SecondaryColor);
            details.Location = new Point(23, 47);
            details.Size = new Size(840, 56);
            details.AutoSize = false;
            details.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Controls.Add(details);
            _pageStack.Controls.Add(card);
        }

        private void AddInfoCard(string title, string message)
        {
            Panel card = CreateCard(100);
            Label heading = MakeLabel(title, 11F, true, GreenColor);
            heading.Location = new Point(22, 16);
            heading.AutoSize = true;
            card.Controls.Add(heading);
            Label details = MakeLabel(message, 9F, false, SecondaryColor);
            details.Location = new Point(23, 42);
            details.Size = new Size(840, 40);
            details.AutoSize = false;
            details.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Controls.Add(details);
            _pageStack.Controls.Add(card);
        }

        private Panel CreateCard(int height)
        {
            Panel card = new Panel();
            card.Size = new Size(900, height);
            card.Margin = new Padding(0, 0, 0, 16);
            card.Padding = new Padding(0);
            card.BackColor = CardColor;
            card.Paint += DrawCardRule;
            return card;
        }

        private void AddSectionHeading(Control container, string title, string subtitle, int left, int top)
        {
            Label heading = MakeLabel(title, 13F, true, TextColor);
            heading.Location = new Point(left, top);
            heading.AutoSize = true;
            container.Controls.Add(heading);
            Label description = MakeLabel(subtitle, 9F, false, SecondaryColor);
            description.Location = new Point(left + 1, top + 27);
            description.AutoSize = true;
            container.Controls.Add(description);
        }

        private DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView();
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 38;
            grid.RowTemplate.Height = 32;
            grid.GridColor = LineColor;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 250);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = SecondaryColor;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold, GraphicsUnit.Point);
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = TextColor;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 241, 255);
            grid.DefaultCellStyle.SelectionForeColor = TextColor;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            grid.DefaultCellStyle.Padding = new Padding(5, 0, 5, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 253);
            return grid;
        }

        private void AddColumn(DataGridView grid, string name, string heading, float fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = heading;
            column.SortMode = DataGridViewColumnSortMode.Automatic;
            column.FillWeight = fillWeight;
            column.MinimumWidth = 55;
            grid.Columns.Add(column);
        }

        private void AddFlattenedRows(DataGridView grid, string prefix, IDictionary<string, object> values)
        {
            foreach (KeyValuePair<string, object> pair in values)
            {
                string name = String.IsNullOrEmpty(prefix) ? pair.Key : prefix + "." + pair.Key;
                IDictionary<string, object> nested = pair.Value as IDictionary<string, object>;
                if (nested != null)
                {
                    AddFlattenedRows(grid, name, nested);
                }
                else
                {
                    string value = pair.Value == null ? "—" : Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
                    if (String.IsNullOrEmpty(value)) value = "—";
                    grid.Rows.Add(name, value);
                }
            }
        }

        private TextBox CreateReadOnlyLine(string value)
        {
            TextBox box = new TextBox();
            box.Text = value;
            box.ReadOnly = true;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Color.FromArgb(248, 248, 250);
            box.ForeColor = TextColor;
            box.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            box.ShortcutsEnabled = true;
            return box;
        }

        private TextBox CreateOutputBox()
        {
            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.WordWrap = false;
            box.ScrollBars = ScrollBars.Both;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Color.White;
            box.ForeColor = TextColor;
            box.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            box.AcceptsReturn = true;
            box.AcceptsTab = true;
            return box;
        }

        private Button CreateButton(string caption, int width, int height, Color background, Color foreground)
        {
            Button button = new Button();
            button.Text = caption;
            button.Size = new Size(width, height);
            button.BackColor = background;
            button.ForeColor = foreground;
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private Label MakeLabel(string text, float size, bool bold, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            return label;
        }

        private void CopyToClipboard(string value, string message)
        {
            try
            {
                Clipboard.SetText(value ?? String.Empty);
                _headerSubtitle.Text = message;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "The clipboard could not be accessed. " + exception.Message, "Clipboard unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string GetBaseUrl()
        {
            return _config == null ? "http://127.0.0.1:8765/" : _config.ListenPrefix;
        }

        private static string FriendlyStartupError(string message)
        {
            if (String.IsNullOrEmpty(message))
            {
                return "Slate could not start. Check the configuration and activity log.";
            }
            if (message.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Windows requires permission to listen on this local address. Select Fix permissions and approve the Windows administrator prompt.";
            }
            if (message.IndexOf("already in use", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("Only one usage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "The configured port is already in use. Stop the other listener or change the Port setting in slate.config.json.";
            }
            return "Slate could not start the API: " + message;
        }

        private static string GetString(IDictionary<string, object> values, string key)
        {
            object value;
            return values != null && values.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : String.Empty;
        }

        private static long GetLong(IDictionary<string, object> values, string key)
        {
            object value;
            long parsed;
            if (values != null && values.TryGetValue(key, out value) && value != null &&
                Int64.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            return 0L;
        }

        private static bool GetBool(IDictionary<string, object> values, string key)
        {
            object value;
            bool parsed;
            return values != null && values.TryGetValue(key, out value) && value != null &&
                Boolean.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) && parsed;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "—";
            string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int index = 0;
            while (size >= 1024.0 && index < units.Length - 1)
            {
                size /= 1024.0;
                index++;
            }
            return size.ToString(index == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[index];
        }

        private static string FormatTimestamp(string value)
        {
            if (String.IsNullOrEmpty(value)) return "—";
            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
            {
                return parsed.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
            return "—";
        }

        private static string PrettyJson(string json)
        {
            if (String.IsNullOrEmpty(json)) return String.Empty;
            StringBuilder output = new StringBuilder();
            bool inString = false;
            bool escaped = false;
            int indent = 0;
            int index;
            for (index = 0; index < json.Length; index++)
            {
                char current = json[index];
                if (inString)
                {
                    output.Append(current);
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    output.Append(current);
                }
                else if (current == '{' || current == '[')
                {
                    output.Append(current);
                    output.AppendLine();
                    indent++;
                    AppendIndent(output, indent);
                }
                else if (current == '}' || current == ']')
                {
                    output.AppendLine();
                    indent = Math.Max(0, indent - 1);
                    AppendIndent(output, indent);
                    output.Append(current);
                }
                else if (current == ',')
                {
                    output.Append(current);
                    output.AppendLine();
                    AppendIndent(output, indent);
                }
                else if (current == ':')
                {
                    output.Append(": ");
                }
                else if (!Char.IsWhiteSpace(current))
                {
                    output.Append(current);
                }
            }
            return output.ToString();
        }

        private static void AppendIndent(StringBuilder builder, int indent)
        {
            int count;
            for (count = 0; count < indent; count++) builder.Append("  ");
        }

        private static void DrawBottomRule(object sender, PaintEventArgs eventArgs)
        {
            Control control = sender as Control;
            if (control != null)
            {
                using (Pen pen = new Pen(LineColor))
                {
                    eventArgs.Graphics.DrawLine(pen, 0, control.Height - 1, control.Width, control.Height - 1);
                }
            }
        }

        private static void DrawRightRule(object sender, PaintEventArgs eventArgs)
        {
            Control control = sender as Control;
            if (control != null)
            {
                using (Pen pen = new Pen(LineColor))
                {
                    eventArgs.Graphics.DrawLine(pen, control.Width - 1, 0, control.Width - 1, control.Height);
                }
            }
        }

        private static void DrawCardRule(object sender, PaintEventArgs eventArgs)
        {
            Control control = sender as Control;
            if (control != null)
            {
                using (Pen pen = new Pen(LineColor))
                {
                    eventArgs.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, control.Width - 1), Math.Max(0, control.Height - 1));
                }
            }
        }
    }
}
