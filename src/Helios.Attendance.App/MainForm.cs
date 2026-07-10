using System.Diagnostics;
using System.ServiceProcess;
using Helios.Attendance.Core;
using Helios.Attendance.Core.Data;
using Helios.Attendance.Core.Devices;
using Helios.Attendance.Core.Models;
using Helios.Attendance.Core.Sync;

namespace Helios.Attendance.App;

public sealed class MainForm : Form
{
    private static readonly Color StatusReadyColor = Color.SeaGreen;
    private static readonly Color StatusBusyColor = Color.DarkOrange;
    private static readonly Color StatusErrorColor = Color.Firebrick;
    private static readonly Color PrimaryBlue = Color.FromArgb(20, 126, 113);
    private static readonly Color SidebarBackColor = Color.FromArgb(31, 42, 48);
    private static readonly Color SidebarSelectedColor = Color.FromArgb(20, 126, 113);
    private static readonly Color ShellBackColor = Color.FromArgb(244, 246, 248);
    private static readonly Color CardBorderColor = Color.FromArgb(220, 226, 229);
    private static readonly Color FormInputBorderColor = Color.FromArgb(213, 222, 227);
    private static readonly Color FormInputHoverBorderColor = Color.FromArgb(174, 194, 201);
    private static readonly Color FormInputBackColor = Color.FromArgb(252, 254, 254);
    private static readonly Color MutedTextColor = Color.FromArgb(94, 108, 113);
    private readonly AttendanceSyncStore _store = new();
    private readonly IAttendanceDeviceClient _deviceClient = new DeviceTypeAttendanceDeviceClient();
    private readonly SyncEngine _syncEngine;

    private readonly ToolStripStatusLabel _statusText = new();
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly List<Button> _menuButtons = [];

    private readonly Label _serviceStatusValue = ValueLabel();
    private readonly Label _apiStatusValue = ValueLabel();
    private readonly Label _deviceCountValue = ValueLabel();
    private readonly Label _activeDeviceValue = ValueLabel();
    private readonly Label _deviceErrorValue = ValueLabel();
    private readonly Label _lastSyncValue = ValueLabel();
    private readonly Label _sentTodayValue = ValueLabel();
    private readonly Label _pendingValue = ValueLabel();
    private readonly Label _mappingErrorValue = ValueLabel();
    private readonly TextBox _outputText = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };

    private readonly DataGridView _devicesGrid = Grid();
    private readonly DataGridView _homeGrid = Grid();
    private readonly TextBox _homeSearchText = new();
    private readonly TextBox _deviceIdText = new();
    private readonly TextBox _deviceNameText = new();
    private readonly TextBox _storeCodeText = new();
    private readonly ComboBox _deviceTypeInput = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList
    };
    private readonly TextBox _ipAddressText = new();
    private readonly NumericUpDown _portInput = new()
    {
        Minimum = 1,
        Maximum = 65535,
        Value = 4370
    };
    private readonly NumericUpDown _passwordInput = new()
    {
        Minimum = 0,
        Maximum = 999999999,
        Value = 0
    };
    private readonly CheckBox _deviceActiveCheck = new()
    {
        Text = "Đang hoạt động",
        Checked = true,
        AutoSize = true
    };

    private readonly TextBox _apiUrlText = new();
    private readonly TextBox _apiTokenText = new()
    {
        UseSystemPasswordChar = true
    };
    private readonly NumericUpDown _apiTimeoutInput = new()
    {
        Minimum = 1,
        Maximum = 300,
        Value = 30
    };
    private readonly NumericUpDown _syncIntervalInput = new()
    {
        Minimum = 1,
        Maximum = 1440,
        Value = 5
    };
    private readonly NumericUpDown _pushIntervalInput = new()
    {
        Minimum = 1,
        Maximum = 1440,
        Value = 1
    };
    private readonly NumericUpDown _pushBatchSizeInput = new()
    {
        Minimum = 1,
        Maximum = 5000,
        Value = 200
    };
    private readonly NumericUpDown _readBackDaysInput = new()
    {
        Minimum = 0,
        Maximum = 365,
        Value = 1
    };
    private readonly CheckBox _autoPushCheck = new()
    {
        Text = "Tự động đẩy lên server sau khi lấy log",
        AutoSize = true
    };

    private readonly DataGridView _historyGrid = Grid();
    private readonly DataGridView _pendingGrid = Grid();
    private readonly DataGridView _pendingAllGrid = Grid();
    private readonly DataGridView _errorsGrid = Grid();
    private readonly TextBox _searchEmployeeText = new();
    private readonly SearchDatePicker _searchFromDate = new();
    private readonly SearchDatePicker _searchToDate = new();

    private bool _loadingDevices;
    private bool _serviceInstallPromptShown;

    public MainForm()
    {
        _store.Initialize();
        _syncEngine = new SyncEngine(_store, _deviceClient);

        Text = "HOFFICE";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        Font = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 720);
        Size = new Size(1240, 780);

        ConfigureDeviceTypeInput();
        ConfigureControlStyles();
        InitializeLayout();
        LoadSettingsIntoForm();
        RefreshDynamicData();
        Shown += async (_, _) => await OfferServiceInstallIfMissingAsync();
    }

    private void ConfigureDeviceTypeInput()
    {
        _deviceTypeInput.DataSource = AttendanceDeviceTypes.Options.ToList();
        _deviceTypeInput.DisplayMember = nameof(DeviceTypeOption.Label);
        _deviceTypeInput.ValueMember = nameof(DeviceTypeOption.Value);
        _deviceTypeInput.SelectedValue = AttendanceDeviceTypes.ZkRonaldJack;
    }

    private void ConfigureControlStyles()
    {
        foreach (var input in new Control[]
        {
            _homeSearchText,
            _deviceIdText,
            _deviceNameText,
            _storeCodeText,
            _ipAddressText,
            _apiUrlText,
            _apiTokenText,
            _searchEmployeeText,
            _deviceTypeInput,
            _portInput,
            _passwordInput,
            _apiTimeoutInput,
            _syncIntervalInput,
            _pushIntervalInput,
            _pushBatchSizeInput,
            _readBackDaysInput,
            _searchFromDate,
            _searchToDate
        })
        {
            StyleInputControl(input);
        }

        _outputText.BorderStyle = BorderStyle.None;
        _outputText.BackColor = Color.White;
        _outputText.ForeColor = Color.FromArgb(28, 48, 54);
        _outputText.Font = new Font("Consolas", 9F);
    }

    private void InitializeLayout()
    {
        SetStatus("Sẵn sàng", StatusReadyColor);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        BackColor = ShellBackColor;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        ConfigurePageHost();

        _tabs.TabPages.Add(BuildOverviewTab());
        _tabs.TabPages.Add(BuildDevicesTab());
        _tabs.TabPages.Add(BuildSearchTab());
        _tabs.TabPages.Add(BuildPendingTab());
        _tabs.TabPages.Add(BuildHistoryTab());
        _tabs.TabPages.Add(BuildApiTab());
        _tabs.TabPages.Add(BuildErrorsTab());
        _tabs.SelectedIndexChanged += (_, _) => UpdateMenuState();

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_statusText);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = ShellBackColor
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.Controls.Add(BuildSidebar(), 0, 0);
        shell.Controls.Add(_tabs, 1, 0);

        root.Controls.Add(BuildAppHeader(), 0, 0);
        root.Controls.Add(shell, 0, 1);
        root.Controls.Add(statusStrip, 0, 2);
        Controls.Add(root);
        UpdateMenuState();
    }

    private void ConfigurePageHost()
    {
        _tabs.Appearance = TabAppearance.FlatButtons;
        _tabs.ItemSize = new Size(0, 1);
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.Padding = new Point(0, 0);
    }

    private Control BuildAppHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(20, 12, 16, 12),
            BackColor = Color.White
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        var brand = new Label
        {
            Text = "HOFFICE",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 48, 54),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var status = new Label
        {
            Text = "Chấm công tự động | Quản lý thiết bị | Đồng bộ API",
            Dock = DockStyle.Fill,
            ForeColor = MutedTextColor,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var syncButton = new Button
        {
            Text = "Đẩy dữ liệu",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = PrimaryBlue,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0)
        };
        syncButton.FlatAppearance.BorderSize = 0;
        syncButton.Click += async (_, _) => await PushNowAsync();

        header.Controls.Add(brand, 0, 0);
        header.Controls.Add(status, 1, 0);
        header.Controls.Add(syncButton, 2, 0);
        return header;
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12, 18, 12, 12),
            BackColor = SidebarBackColor
        };
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Attendance Sync",
            Dock = DockStyle.Top,
            Height = 36,
            ForeColor = Color.FromArgb(180, 220, 212),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        nav.Controls.Add(SideNavButton("Tổng quan", 0));
        nav.Controls.Add(SideNavButton("Thiết bị", 1));
        nav.Controls.Add(SideNavButton("Tìm kiếm log", 2));
        nav.Controls.Add(SideNavButton("Pending", 3));
        nav.Controls.Add(SideNavButton("Lịch sử đồng bộ", 4));
        nav.Controls.Add(SideNavButton("Cài đặt API", 5));
        nav.Controls.Add(SideNavButton("Hỗ trợ", 6));

        var footer = new Label
        {
            Text = "HOFFICE © 2.0",
            Dock = DockStyle.Bottom,
            Height = 28,
            ForeColor = Color.FromArgb(155, 165, 170),
            TextAlign = ContentAlignment.MiddleLeft
        };

        sidebar.Controls.Add(title, 0, 0);
        sidebar.Controls.Add(nav, 0, 1);
        sidebar.Controls.Add(footer, 0, 2);
        return sidebar;
    }

    private Button SideNavButton(string text, int pageIndex)
    {
        var button = new Button
        {
            Text = text,
            Width = 190,
            Height = 42,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = SidebarBackColor,
            ForeColor = Color.White,
            Tag = pageIndex
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => ShowPage(pageIndex);
        _menuButtons.Add(button);
        return button;
    }

    private static Control MetricCard(string title, Label value)
    {
        var card = CardPanel(new Padding(12), new Padding(0, 0, 12, 0));
        card.MinimumSize = new Size(0, 96);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        value.AutoSize = false;
        value.Dock = DockStyle.Fill;
        value.Margin = new Padding(0);
        value.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        value.TextAlign = ContentAlignment.MiddleLeft;

        layout.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = MutedTextColor,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(value, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static Panel CardPanel(Padding padding)
    {
        return CardPanel(padding, new Padding(0));
    }

    private static Panel CardPanel(Padding padding, Padding margin)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = padding,
            Margin = margin
        };
        panel.Paint += (_, e) =>
        {
            var rect = panel.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            using var pen = new Pen(CardBorderColor);
            e.Graphics.DrawRectangle(pen, rect);
        };
        return panel;
    }

    private void ShowPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _tabs.TabPages.Count)
        {
            return;
        }

        _tabs.SelectedIndex = pageIndex;
        UpdateMenuState();
    }

    private void UpdateMenuState()
    {
        foreach (var button in _menuButtons)
        {
            var selected = button.Tag is int pageIndex && pageIndex == _tabs.SelectedIndex;
            button.BackColor = selected ? SidebarSelectedColor : SidebarBackColor;
            button.ForeColor = selected ? Color.White : Color.FromArgb(220, 226, 228);
        }
    }

    private TabPage BuildOverviewTab()
    {
        var tab = new TabPage("Tổng quan") { BackColor = ShellBackColor };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(16),
            BackColor = ShellBackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12)
        };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.Controls.Add(MetricCard("Service", _serviceStatusValue), 0, 0);
        metrics.Controls.Add(MetricCard("API", _apiStatusValue), 1, 0);
        metrics.Controls.Add(MetricCard("Chờ gửi", _pendingValue), 2, 0);
        metrics.Controls.Add(MetricCard("Lần đồng bộ", _lastSyncValue), 3, 0);

        var search = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 0, 0, 10)
        };
        _homeSearchText.TextChanged += (_, _) => RefreshHome();
        var homeSearchFrame = InputFrame(_homeSearchText);
        homeSearchFrame.Width = 190;
        homeSearchFrame.Height = 32;
        homeSearchFrame.Margin = new Padding(8, 0, 0, 0);
        search.Controls.Add(homeSearchFrame);
        search.Controls.Add(new Label
        {
            Text = "Tìm kiếm",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 3, 0, 0)
        });

        var gridCard = CardPanel(new Padding(1));
        gridCard.Controls.Add(_homeGrid);

        layout.Controls.Add(metrics, 0, 0);
        layout.Controls.Add(search, 0, 1);
        layout.Controls.Add(gridCard, 0, 2);
        tab.Controls.Add(layout);
        return tab;
    }
    private TabPage BuildDevicesTab()
    {
        var tab = new TabPage("Thiết bị") { BackColor = ShellBackColor };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(16),
            BackColor = ShellBackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _devicesGrid.SelectionChanged += (_, _) => LoadSelectedDeviceIntoForm();
        var gridPanel = CardPanel(new Padding(1), new Padding(0, 0, 14, 0));
        gridPanel.Controls.Add(_devicesGrid);
        layout.Controls.Add(gridPanel, 0, 0);

        var form = FormGrid();
        AddRow(form, "Device ID", _deviceIdText);
        AddRow(form, "Tên máy", _deviceNameText);
        AddRow(form, "Chi nhánh", _storeCodeText);
        AddRow(form, "Loại máy", _deviceTypeInput);
        AddRow(form, "IP", _ipAddressText);
        AddRow(form, "Port", _portInput);
        AddRow(form, "Password", _passwordInput);
        AddRow(form, "Trạng thái", _deviceActiveCheck);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        buttons.Controls.Add(Button("Test kết nối", async (_, _) => await TestDeviceAsync()));
        buttons.Controls.Add(Button("Cài driver", async (_, _) => await InstallDeviceDriverAsync()));
        buttons.Controls.Add(Button("Lưu", (_, _) => SaveDevice()));
        buttons.Controls.Add(Button("Xóa", (_, _) => DeleteSelectedDevice()));
        buttons.Controls.Add(Button("Làm mới", (_, _) => RefreshDevices()));

        var sideLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        sideLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sideLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sideLayout.Controls.Add(new Label
        {
            Text = "Cấu hình máy",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 48, 54),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        sideLayout.Controls.Add(form, 0, 1);
        sideLayout.Controls.Add(buttons, 0, 2);

        var sideCard = CardPanel(new Padding(14));
        sideCard.Controls.Add(sideLayout);
        layout.Controls.Add(sideCard, 1, 0);

        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildApiTab()
    {
        var tab = new TabPage("API") { BackColor = ShellBackColor };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16),
            BackColor = ShellBackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 500));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var form = FormGrid();
        AddRow(form, "API URL", _apiUrlText);
        AddRow(form, "API Token", _apiTokenText);
        AddRow(form, "Timeout giây", _apiTimeoutInput);
        AddRow(form, "Tải log (phút/lần)", _syncIntervalInput);
        AddRow(form, "Đẩy log (phút/lần)", _pushIntervalInput);
        AddRow(form, "Số log đẩy/lần", _pushBatchSizeInput);
        AddRow(form, "Đọc lùi ngày", _readBackDaysInput);
        AddRow(form, "Tự động đẩy", _autoPushCheck);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        actions.Controls.Add(Button("Test API", async (_, _) => await TestApiAsync()));
        actions.Controls.Add(Button("Lưu cấu hình", (_, _) => SaveApiSettingsFromForm()));

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3
        };
        content.Controls.Add(new Label
        {
            Text = "Cấu hình kết nối API",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 48, 54),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        content.Controls.Add(form, 0, 1);
        content.Controls.Add(actions, 0, 2);

        var card = CardPanel(new Padding(16));
        card.Controls.Add(content);
        layout.Controls.Add(card, 0, 0);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildHistoryTab()
    {
        var tab = new TabPage("Lịch sử đồng bộ") { BackColor = ShellBackColor };
        tab.Controls.Add(BuildGridPanel(_historyGrid, Button("Làm mới", (_, _) => RefreshHistory())));
        return tab;
    }

    private TabPage BuildSearchTab()
    {
        var tab = new TabPage("Tìm kiếm log") { BackColor = ShellBackColor };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(16),
            BackColor = ShellBackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filterContent = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2
        };
        filterContent.Controls.Add(new Label
        {
            Text = "Tìm kiếm dữ liệu chấm công",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 48, 54),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 8, 0, 0)
        };

        void AddFilter(string label, Control control, int width)
        {
            var field = new TableLayoutPanel
            {
                Width = width,
                Height = 68,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 12, 0)
            };
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            field.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                ForeColor = MutedTextColor,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            field.Controls.Add(SearchInputFrame(control), 0, 1);
            filters.Controls.Add(field);
        }

        AddFilter("Mã chấm công", _searchEmployeeText, 220);
        AddFilter("Từ ngày", _searchFromDate, 190);
        AddFilter("Đến ngày", _searchToDate, 190);

        var searchButton = Button("Tìm kiếm", (_, _) => RefreshPending());
        searchButton.Width = 110;
        searchButton.Height = 32;
        searchButton.Margin = new Padding(0, 25, 8, 0);
        filters.Controls.Add(searchButton);
        filterContent.Controls.Add(filters, 0, 1);

        var filterCard = CardPanel(new Padding(14), new Padding(0, 0, 0, 12));
        filterCard.Controls.Add(filterContent);

        var gridCard = CardPanel(new Padding(1));
        gridCard.Controls.Add(_pendingGrid);

        layout.Controls.Add(filterCard, 0, 0);
        layout.Controls.Add(gridCard, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildPendingTab()
    {
        var tab = new TabPage("Pending") { BackColor = ShellBackColor };
        tab.Controls.Add(BuildGridPanel(
            _pendingAllGrid,
            Button("Đẩy dữ liệu", async (_, _) => await PushNowAsync()),
            Button("Xóa pending", (_, _) => ClearPendingLogs()),
            Button("Làm mới", (_, _) => RefreshPendingList())));
        return tab;
    }

    private TabPage BuildErrorsTab()
    {
        var tab = new TabPage("Hỗ trợ") { BackColor = ShellBackColor };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(16),
            BackColor = ShellBackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var actionContent = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2
        };
        actionContent.Controls.Add(new Label
        {
            Text = "Công cụ vận hành",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 48, 54),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0)
        };
        actions.Controls.Add(Button("Lấy log ngay", async (_, _) => await PollNowAsync()));
        actions.Controls.Add(Button("Đẩy dữ liệu", async (_, _) => await PushNowAsync()));
        actions.Controls.Add(Button("Cài driver", async (_, _) => await InstallDeviceDriverAsync()));
        actions.Controls.Add(Button("Cài/Cập nhật Service", async (_, _) => await InstallServiceAsync()));
        actions.Controls.Add(Button("Restart Service", async (_, _) => await RestartServiceAsync()));
        actions.Controls.Add(Button("Mở thư mục log", (_, _) => OpenDataFolder()));
        actions.Controls.Add(Button("Làm mới", (_, _) => RefreshDynamicData()));
        actionContent.Controls.Add(actions, 0, 1);

        var actionCard = CardPanel(new Padding(14), new Padding(0, 0, 0, 12));
        actionCard.Controls.Add(actionContent);

        var outputCard = CardPanel(new Padding(1));
        outputCard.Controls.Add(_outputText);

        layout.Controls.Add(actionCard, 0, 0);
        layout.Controls.Add(outputCard, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private static Control BuildGridPanel(DataGridView grid, params Button[] buttons)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(16),
            BackColor = ShellBackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 8)
        };
        actions.Controls.AddRange(buttons);

        var gridCard = CardPanel(new Padding(1));
        gridCard.Controls.Add(grid);

        layout.Controls.Add(actions, 0, 0);
        layout.Controls.Add(gridCard, 0, 1);
        return layout;
    }

    private async Task PollNowAsync()
    {
        SaveApiSettingsFromForm(showMessage: false);
        await RunBusyAsync("Đang lấy log...", async () =>
        {
            AppendOutput("Bắt đầu lấy log từ máy chấm công.");
            var result = await _syncEngine.PollDevicesAsync(CancellationToken.None, AppendOutput);
            AppendOutput($"{result.Message} Đọc={result.TotalRead}, lỗi={result.TotalFailed}, mới chờ đẩy={result.PendingCreated}, tổng chờ={_store.GetPendingLogCount()}.");
            RefreshDynamicData();
        });
    }

    private async Task PushNowAsync()
    {
        SaveApiSettingsFromForm(showMessage: false);
        await RunBusyAsync("Đang đẩy dữ liệu...", async () =>
        {
            AppendOutput("Bắt đầu đẩy dữ liệu chờ lên server.");
            var result = await _syncEngine.PushPendingAsync(CancellationToken.None, AppendOutput);
            AppendOutput($"{result.Message} Gửi={result.TotalSent}, lỗi={result.TotalFailed}, còn chờ={result.PendingCreated}.");
            RefreshDynamicData();
        });
    }

    private async Task TestApiAsync()
    {
        SaveApiSettingsFromForm(showMessage: false);
        await RunBusyAsync("Đang test API...", async () =>
        {
            var client = new AttendanceApiClient(ReadApiSettingsFromForm());
            var result = await client.TestAsync(CancellationToken.None);
            _apiStatusValue.Text = result.Success ? "Kết nối thành công" : result.Message;
            _apiStatusValue.ForeColor = result.Success ? Color.SeaGreen : Color.Firebrick;
            AppendOutput(result.Success ? "Test API thành công." : $"Test API lỗi: {result.Message}");
        });
    }

    private async Task TestDeviceAsync()
    {
        await RunBusyAsync("Đang test thiết bị...", async () =>
        {
            var device = ReadDeviceFromForm();
            var result = await _deviceClient.TestConnectionAsync(device, CancellationToken.None);
            AppendOutput(result.Success ? $"Test thiết bị thành công: {result.Message}" : $"Test thiết bị lỗi: {result.Message}");
            if (!result.Success && IsMissingZkSdk(result.Message))
            {
                var confirm = MessageBox.Show(
                    $"{result.Message}{Environment.NewLine}{Environment.NewLine}App có thể tự tìm và cài driver. Bấm Yes để cài ngay.",
                    "Thiếu driver",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    await InstallDeviceDriverCoreAsync();
                }

                return;
            }

            MessageBox.Show(result.Message, result.Success ? "Thành công" : "Lỗi", MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        });
    }

    private async Task InstallDeviceDriverAsync()
    {
        await RunBusyAsync("Đang cài driver...", InstallDeviceDriverCoreAsync);
    }

    private async Task InstallDeviceDriverCoreAsync()
    {
        var deviceType = Convert.ToString(_deviceTypeInput.SelectedValue);
        if (AttendanceDeviceTypes.Normalize(deviceType) == AttendanceDeviceTypes.ZkRonaldJack)
        {
            await InstallZkSdkCoreAsync();
            return;
        }

        var deviceTypeName = AttendanceDeviceTypes.GetDisplayName(deviceType);
        var message = $"Loại máy {deviceTypeName} chưa có bộ cài driver tự động trong bản này. Cần bổ sung SDK/giao thức của hãng trước.";
        AppendOutput(message);
        MessageBox.Show(message, "Chưa hỗ trợ driver", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task InstallZkSdkCoreAsync()
    {
        if (ZkSdkInstaller.IsRegistered())
        {
            AppendOutput("SDK ZK đã sẵn sàng.");
            MessageBox.Show("SDK ZK đã sẵn sàng. Hãy bấm Test kết nối lại.", "SDK ZK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AppendOutput("Đang tìm nhanh SDK ZK (sdk.zip hoặc zkemkeeper.dll)...");
        var dllPath = await Task.Run(() => ZkSdkInstaller.FindSdkSource(TimeSpan.FromSeconds(3)));
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            AppendOutput("Không tìm thấy SDK ZK tự động, mở cửa sổ chọn file.");
            MessageBox.Show(
                "Không tìm thấy driver gốc tự động. Hãy chọn sdk.zip hoặc zkemkeeper.dll trong thư mục cài phần mềm/SDK của nhà cung cấp, ví dụ DTC Software, ZK hoặc ZKTeco.",
                "Chọn driver",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            using var dialog = new OpenFileDialog
            {
                Title = "Chọn sdk.zip hoặc zkemkeeper.dll",
                Filter = "ZK SDK (sdk.zip; zkemkeeper.dll)|sdk.zip;zkemkeeper.dll|ZIP files (*.zip)|*.zip|DLL files (*.dll)|*.dll|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                AppendOutput("Chưa chọn SDK ZK.");
                return;
            }

            dllPath = dialog.FileName;
        }

        AppendOutput($"Đang cài SDK ZK từ {dllPath}.");
        var result = await Task.Run(() => ZkSdkInstaller.RegisterSdk(dllPath));
        AppendOutput(result.Success ? $"Cài SDK ZK thành công: {result.Message}" : $"Cài SDK ZK lỗi: {result.Message}");

        MessageBox.Show(
            result.Message,
            result.Success ? "Cài SDK ZK thành công" : "Cài SDK ZK lỗi",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private async Task RestartServiceAsync()
    {
        await RunBusyAsync("Đang restart service...", async () =>
        {
            if (!ServiceInstaller.IsServiceInstalled())
            {
                var confirm = MessageBox.Show(
                    "Service nền chưa được cài vào Windows. Bạn có muốn cài ngay không?",
                    "Chưa cài service",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    await InstallServiceAsync();
                }

                return;
            }

            await Task.Run(ServiceInstaller.LaunchElevatedRestart);
            await Task.Delay(1200);
            AppendServiceStatus("Đã gửi lệnh restart service");
            RefreshOverview();
        });
    }

    private async Task InstallServiceAsync()
    {
        if (ServiceInstaller.IsServiceInstalled())
        {
            var confirm = MessageBox.Show(
                "Service nền đã được cài. Bạn có muốn cập nhật service sang file app hiện tại không?",
                "Cập nhật service",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                RefreshOverview();
                return;
            }
        }

        await RunBusyAsync("Đang cài service...", async () =>
        {
            await Task.Run(ServiceInstaller.LaunchElevatedInstall);
            await Task.Delay(1200);
            AppendServiceStatus("Đã cài/cập nhật service nền");
            RefreshOverview();
        });
    }

    private async Task OfferServiceInstallIfMissingAsync()
    {
        if (_serviceInstallPromptShown || ServiceInstaller.IsServiceInstalled())
        {
            return;
        }

        _serviceInstallPromptShown = true;
        var confirm = MessageBox.Show(
            "Service nền chưa được cài. Cài service để app tự đồng bộ kể cả khi không mở giao diện?",
            "Cài service nền",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            await InstallServiceAsync();
        }
    }

    private void OpenDataFolder()
    {
        AppPaths.EnsureDirectories();
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.DataDirectory,
            UseShellExecute = true
        });
    }

    private void AppendServiceStatus(string action)
    {
        try
        {
            var status = ServiceInstaller.GetServiceStatus();
            if (status is null)
            {
                AppendOutput($"{action}, nhưng Windows chưa thấy service {AppPaths.ServiceName}.");
                return;
            }

            if (status == ServiceControllerStatus.Running)
            {
                AppendOutput($"{action}. Service đang Running.");
                return;
            }

            AppendOutput($"{action}, nhưng service hiện đang {status}. Nếu trạng thái vẫn là Stopped, hãy bấm Cài/Cập nhật Service lại.");
        }
        catch (Exception ex)
        {
            AppendOutput($"{action}, nhưng chưa đọc được trạng thái service: {ex.Message}");
        }
    }

    private void SaveDevice()
    {
        try
        {
            var device = ReadDeviceFromForm();
            if (string.IsNullOrWhiteSpace(device.DeviceId) || string.IsNullOrWhiteSpace(device.IpAddress))
            {
                MessageBox.Show("Device ID và IP là bắt buộc.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _store.SaveDevice(device);
            AppendOutput($"Đã lưu thiết bị {device.DeviceId}.");
            RefreshDevices();
            RefreshOverview();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void DeleteSelectedDevice()
    {
        if (_devicesGrid.CurrentRow?.DataBoundItem is not Device device || device.Id <= 0)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Xóa thiết bị {device.DeviceId}?",
            "Xác nhận",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _store.DeleteDevice(device.Id);
        AppendOutput($"Đã xóa thiết bị {device.DeviceId}.");
        RefreshDevices();
        RefreshOverview();
    }

    private void ClearPendingLogs()
    {
        var confirm = MessageBox.Show(
            "Xóa toàn bộ pending logs hiện tại?",
            "Xác nhận",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _store.ClearAllPendingLogs();
        RefreshPending();
        RefreshPendingList();
        RefreshOverview();
    }

    private void LoadSettingsIntoForm()
    {
        var settings = _store.GetApiSettings();
        _apiUrlText.Text = settings.ApiUrl;
        _apiTokenText.Text = settings.ApiToken;
        _apiTimeoutInput.Value = Math.Clamp(settings.TimeoutSeconds, 1, 300);
        _syncIntervalInput.Value = Math.Clamp(_store.GetPollIntervalMinutes(), 1, 1440);
        _pushIntervalInput.Value = Math.Clamp(_store.GetPushIntervalMinutes(), 1, 1440);
        _pushBatchSizeInput.Value = Math.Clamp(_store.GetPushBatchSize(), 1, 5000);
        _readBackDaysInput.Value = Math.Clamp(_store.GetReadBackDays(), 0, 365);
        _autoPushCheck.Checked = _store.GetAutoPushEnabled();
        _searchFromDate.Value = DateTime.Today.AddDays(-Math.Max(1, (int)_readBackDaysInput.Value));
        _searchToDate.Value = DateTime.Today;
    }

    private void SaveApiSettingsFromForm(bool showMessage = true)
    {
        _store.SaveApiSettings(ReadApiSettingsFromForm());
        _store.SaveSyncSettings(
            (int)_syncIntervalInput.Value,
            (int)_pushIntervalInput.Value,
            (int)_readBackDaysInput.Value,
            (int)_pushBatchSizeInput.Value,
            _autoPushCheck.Checked);
        RefreshOverview();

        if (showMessage)
        {
            AppendOutput("Đã lưu cấu hình API.");
            MessageBox.Show("Đã lưu cấu hình.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private ApiSettings ReadApiSettingsFromForm() => new()
    {
        ApiUrl = _apiUrlText.Text.Trim(),
        ApiToken = _apiTokenText.Text.Trim(),
        TimeoutSeconds = (int)_apiTimeoutInput.Value
    };

    private Device ReadDeviceFromForm() => new()
    {
        DeviceId = _deviceIdText.Text.Trim(),
        DeviceName = _deviceNameText.Text.Trim(),
        StoreCode = _storeCodeText.Text.Trim(),
        DeviceType = Convert.ToString(_deviceTypeInput.SelectedValue) ?? AttendanceDeviceTypes.ZkRonaldJack,
        IpAddress = _ipAddressText.Text.Trim(),
        Port = (int)_portInput.Value,
        Password = (int)_passwordInput.Value,
        IsActive = _deviceActiveCheck.Checked
    };

    private void LoadSelectedDeviceIntoForm()
    {
        if (_loadingDevices || _devicesGrid.CurrentRow?.DataBoundItem is not Device device)
        {
            return;
        }

        _deviceIdText.Text = device.DeviceId;
        _deviceNameText.Text = device.DeviceName;
        _storeCodeText.Text = device.StoreCode;
        _deviceTypeInput.SelectedValue = AttendanceDeviceTypes.Normalize(device.DeviceType);
        _ipAddressText.Text = device.IpAddress;
        _portInput.Value = Math.Clamp(device.Port, 1, 65535);
        _passwordInput.Value = Math.Clamp(device.Password, 0, 999999999);
        _deviceActiveCheck.Checked = device.IsActive;
    }

    private void RefreshDynamicData()
    {
        RefreshOverview();
        RefreshHome();
        RefreshDevices();
        RefreshHistory();
        RefreshPending();
        RefreshPendingList();
        RefreshErrors();
    }

    private void RefreshHome()
    {
        var filter = _homeSearchText.Text.Trim();
        var pendingByDevice = _store.GetPendingLogs()
            .GroupBy(item => item.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var rows = _store.GetDevices()
            .Select((device, index) => DeviceHomeRow.From(device, index + 1, pendingByDevice.GetValueOrDefault(device.DeviceId)))
            .Where(row => string.IsNullOrWhiteSpace(filter) ||
                row.Location.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                row.Code.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                row.IpAddress.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _homeGrid.DataSource = null;
        _homeGrid.DataSource = rows;
        FormatHomeGrid();
    }

    private void RefreshOverview()
    {
        var stats = _store.GetDashboardStats();
        _serviceStatusValue.Text = GetServiceStatus();
        _apiStatusValue.Text = string.IsNullOrWhiteSpace(_store.GetApiSettings().ApiUrl) ? "Chưa cấu hình" : "Đã cấu hình";
        _apiStatusValue.ForeColor = string.IsNullOrWhiteSpace(_store.GetApiSettings().ApiUrl) ? Color.DarkOrange : SystemColors.ControlText;
        _deviceCountValue.Text = stats.TotalDevices.ToString();
        _activeDeviceValue.Text = stats.ActiveDevices.ToString();
        _deviceErrorValue.Text = stats.DeviceErrors.ToString();
        _deviceErrorValue.ForeColor = stats.DeviceErrors == 0 ? Color.SeaGreen : Color.Firebrick;
        _lastSyncValue.Text = string.IsNullOrWhiteSpace(stats.LastSyncAt) ? "Chưa có" : stats.LastSyncAt;
        _sentTodayValue.Text = stats.SentToday.ToString();
        _pendingValue.Text = stats.PendingLogs.ToString();
        _pendingValue.ForeColor = stats.PendingLogs == 0 ? Color.SeaGreen : Color.DarkOrange;
        _mappingErrorValue.Text = stats.MappingErrorsToday.ToString();
        _mappingErrorValue.ForeColor = stats.MappingErrorsToday == 0 ? Color.SeaGreen : Color.Firebrick;
    }

    private void RefreshDevices()
    {
        _loadingDevices = true;
        _devicesGrid.DataSource = null;
        _devicesGrid.DataSource = _store.GetDevices().ToList();
        FormatDevicesGrid();
        _loadingDevices = false;
        LoadSelectedDeviceIntoForm();
    }

    private void FormatDevicesGrid()
    {
        HideColumn("DeviceType");
        HideColumn("CreatedAt");
        HideColumn("UpdatedAt");

        SetColumn("Id", "#", 44);
        SetColumn("DeviceTypeName", "Loại máy", 130);
        SetColumn("DeviceId", "Mã máy", 90);
        SetColumn("DeviceName", "Tên máy", 130);
        SetColumn("StoreCode", "Chi nhánh", 120);
        SetColumn("IpAddress", "IP", 120);
        SetColumn("Port", "Port", 70);
        SetColumn("Password", "Mật khẩu", 78);
        SetColumn("IsActive", "Active", 64);
        SetColumn("LastSuccessSyncAt", "Lần đồng bộ", 140);
        SetColumn("LastError", "Lỗi gần nhất", 220);
    }

    private void FormatHomeGrid()
    {
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.No), "#", 50);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Location), "Địa điểm", 120);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Status), "Trạng thái", 130);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.LogCount), "Số log", 80);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.UserCount), "Người dùng", 95);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.LastLoadedAt), "Lần tải cuối", 145);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.LoadFrom), "Tải từ ngày", 120);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Code), "Mã", 80);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.IpAddress), "IP", 120);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Port), "Port", 70);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Serial), "Serial", 110);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.DeviceType), "Loại máy", 130);
    }

    private void HideColumn(string name)
    {
        if (_devicesGrid.Columns.Contains(name))
        {
            _devicesGrid.Columns[name].Visible = false;
        }
    }

    private void SetColumn(string name, string header, int width)
    {
        if (!_devicesGrid.Columns.Contains(name))
        {
            return;
        }

        var column = _devicesGrid.Columns[name];
        column.HeaderText = header;
        column.Width = width;
    }

    private void RefreshHistory()
    {
        var devices = _store.GetDevices()
            .ToDictionary(device => device.DeviceId, StringComparer.OrdinalIgnoreCase);

        _historyGrid.DataSource = null;
        _historyGrid.DataSource = _store.GetRecentSyncLogs()
            .Select(log => SyncLogRow.From(log, devices.GetValueOrDefault(log.DeviceId)))
            .ToList();
        FormatHistoryGrid();
    }

    private void RefreshPending()
    {
        var employee = _searchEmployeeText.Text.Trim();
        if (!TryReadSearchDates(out var from, out var to))
        {
            return;
        }

        var toExclusive = to.Date.AddDays(1);

        _pendingGrid.DataSource = null;
        _pendingGrid.DataSource = _store.GetPendingLogs(5000)
            .Where(log => string.IsNullOrWhiteSpace(employee) ||
                log.EmployeeCode.Contains(employee, StringComparison.OrdinalIgnoreCase))
            .Where(log => !DateTime.TryParse(log.PunchTime, out var punchTime) ||
                (punchTime >= from && punchTime < toExclusive))
            .Select(PendingLogRow.From)
            .ToList();
        FormatPendingGrid(_pendingGrid);
    }

    private void RefreshPendingList()
    {
        _pendingAllGrid.DataSource = null;
        _pendingAllGrid.DataSource = _store.GetPendingLogs(5000)
            .Select(PendingLogRow.From)
            .ToList();
        FormatPendingGrid(_pendingAllGrid);
    }

    private bool TryReadSearchDates(out DateTime from, out DateTime to)
    {
        from = DateTime.Today;
        to = DateTime.Today;

        from = _searchFromDate.Value.Date;
        to = _searchToDate.Value.Date;

        from = from.Date;
        to = to.Date;
        if (to < from)
        {
            ShowError(new InvalidOperationException("Đến ngày phải lớn hơn hoặc bằng Từ ngày."));
            return false;
        }

        return true;
    }

    private void RefreshErrors()
    {
        _errorsGrid.DataSource = null;
        _errorsGrid.DataSource = _store.GetRecentErrors()
            .Select(AppErrorRow.From)
            .ToList();
        FormatErrorsGrid();
    }

    private void FormatHistoryGrid()
    {
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Time), "Thời gian", 145);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Location), "Địa điểm", 130);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.IpAddress), "IP", 120);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Content), "Nội dung", 140);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.FromDate), "Từ ngày", 110);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.ToDate), "Đến ngày", 110);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Result), "Kết quả", 120);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Data), "Dữ liệu", 140);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Note), "Ghi chú", 300);
    }

    private void FormatPendingGrid(DataGridView grid)
    {
        SetGridColumn(grid, nameof(PendingLogRow.Id), "#", 48);
        SetGridColumn(grid, nameof(PendingLogRow.DeviceId), "Máy", 80);
        SetGridColumn(grid, nameof(PendingLogRow.StoreCode), "Chi nhánh", 110);
        SetGridColumn(grid, nameof(PendingLogRow.EmployeeCode), "Mã nhân viên", 110);
        SetGridColumn(grid, nameof(PendingLogRow.PunchTime), "Thời gian chấm", 150);
        SetGridColumn(grid, nameof(PendingLogRow.VerifyText), "Kiểu chấm", 150);
        SetGridColumn(grid, nameof(PendingLogRow.StateText), "Vào/Ra", 120);
        SetGridColumn(grid, nameof(PendingLogRow.RetryCount), "Lần gửi", 70);
        SetGridColumn(grid, nameof(PendingLogRow.LastError), "Lỗi gửi", 260);
        SetGridColumn(grid, nameof(PendingLogRow.UpdatedAt), "Cập nhật", 145);
    }

    private void FormatErrorsGrid()
    {
        SetGridColumn(_errorsGrid, nameof(AppErrorRow.Id), "#", 48);
        SetGridColumn(_errorsGrid, nameof(AppErrorRow.ErrorType), "Loại lỗi", 110);
        SetGridColumn(_errorsGrid, nameof(AppErrorRow.DeviceId), "Máy", 90);
        SetGridColumn(_errorsGrid, nameof(AppErrorRow.Message), "Thông báo", 320);
        SetGridColumn(_errorsGrid, nameof(AppErrorRow.Detail), "Chi tiết", 360);
        SetGridColumn(_errorsGrid, nameof(AppErrorRow.CreatedAt), "Thời gian", 145);
    }

    private static void SetGridColumn(DataGridView grid, string name, string header, int width)
    {
        if (!grid.Columns.Contains(name))
        {
            return;
        }

        var column = grid.Columns[name];
        column.HeaderText = header;
        column.Width = width;
    }

    private static string GetServiceStatus()
    {
        try
        {
            return ServiceInstaller.GetServiceStatus()?.ToString() ?? "Chưa cài service";
        }
        catch (InvalidOperationException)
        {
            return "Chưa cài service";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static bool IsMissingZkSdk(string message) =>
        message.Contains(ZkSdkInstaller.ProgId, StringComparison.OrdinalIgnoreCase) ||
        message.Contains("driver ZK SDK", StringComparison.OrdinalIgnoreCase);

    private async Task RunBusyAsync(string status, Func<Task> action)
    {
        try
        {
            UseWaitCursor = true;
            SetStatus(status, StatusBusyColor);
            await action();
            SetStatus("Sẵn sàng", StatusReadyColor);
        }
        catch (Exception ex)
        {
            SetStatus("Có lỗi", StatusErrorColor);
            AppendOutput($"Lỗi: {ex.Message}");
            ShowError(ex);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void SetStatus(string text, Color color)
    {
        _statusText.Text = text;
        _statusText.Image = CreateStatusDot(color);
        _statusText.ImageAlign = ContentAlignment.MiddleLeft;
        _statusText.TextImageRelation = TextImageRelation.ImageBeforeText;
    }

    private static Bitmap CreateStatusDot(Color color)
    {
        const int size = 12;
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var brush = new SolidBrush(color);
        using var border = new Pen(ControlPaint.Dark(color), 1);
        graphics.FillEllipse(brush, 2, 2, size - 5, size - 5);
        graphics.DrawEllipse(border, 2, 2, size - 5, size - 5);
        return bitmap;
    }

    private void AppendOutput(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendOutput(message));
            return;
        }

        _outputText.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static Button Button(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(96, 32),
            Margin = new Padding(0, 0, 8, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(28, 48, 54)
        };
        button.FlatAppearance.BorderColor = CardBorderColor;
        button.FlatAppearance.BorderSize = 1;
        button.Click += onClick;
        return button;
    }

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        GridColor = Color.FromArgb(226, 232, 235),
        EnableHeadersVisualStyles = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        ColumnHeadersHeight = 38,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        RowTemplate = new DataGridViewRow
        {
            Height = 30
        },
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(239, 244, 245),
            ForeColor = Color.FromArgb(28, 48, 54),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Padding = new Padding(8, 5, 8, 5),
            SelectionBackColor = Color.FromArgb(239, 244, 245),
            SelectionForeColor = Color.FromArgb(28, 48, 54)
        },
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 251, 251),
            Padding = new Padding(8, 0, 8, 0)
        },
        DefaultCellStyle = new DataGridViewCellStyle
        {
            SelectionBackColor = PrimaryBlue,
            SelectionForeColor = Color.White,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(28, 48, 54),
            Padding = new Padding(8, 0, 8, 0)
        }
    };

    private static TableLayoutPanel FormGrid()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return panel;
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, control is CheckBox ? 34 : 42));

        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, row);

        if (control is CheckBox)
        {
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 3, 0, 3);
            panel.Controls.Add(control, 1, row);
            return;
        }

        panel.Controls.Add(InputFrame(control), 1, row);
    }

    private static Control InputFrame(Control control)
    {
        StyleFormInputControl(control);
        return BuildStyledInputFrame(control);
    }

    private static Control SearchInputFrame(Control control)
    {
        StyleInputControl(control);
        return BuildStyledInputFrame(control);
    }

    private static Control BuildStyledInputFrame(Control control)
    {
        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 32,
            MinimumSize = new Size(0, 32),
            Margin = new Padding(0, 2, 0, 2),
            BackColor = Color.White,
            TabStop = false
        };

        var border = new Panel
        {
            BackColor = FormInputBorderColor,
            Padding = new Padding(1),
            TabStop = false
        };

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(10, 0, 8, 0),
            BackColor = FormInputBackColor
        };
        var active = false;
        var hovering = false;

        void ApplyVisualState()
        {
            border.BackColor = active ? PrimaryBlue : hovering ? FormInputHoverBorderColor : FormInputBorderColor;
            var backColor = active ? Color.White : FormInputBackColor;
            content.BackColor = backColor;
            control.BackColor = backColor;
            if (control is SearchDatePicker datePicker)
            {
                datePicker.SetChromeBackColor(backColor);
            }
        }

        void LayoutControl()
        {
            border.Bounds = new Rectangle(0, 0, Math.Max(24, frame.ClientSize.Width - 8), frame.ClientSize.Height);
            if (control is SearchDatePicker)
            {
                control.Bounds = new Rectangle(
                    content.Padding.Left,
                    0,
                    Math.Max(24, content.ClientSize.Width - content.Padding.Horizontal),
                    content.ClientSize.Height);
                return;
            }

            var preferredHeight = control.PreferredSize.Height;
            if (control is TextBox)
            {
                preferredHeight = 20;
            }

            var height = Math.Min(preferredHeight, Math.Max(18, content.ClientSize.Height - 4));
            var top = Math.Max(2, (content.ClientSize.Height - height) / 2);
            control.Bounds = new Rectangle(
                content.Padding.Left,
                top,
                Math.Max(24, content.ClientSize.Width - content.Padding.Horizontal),
                height);
        }

        void MouseEntered(object? _, EventArgs __)
        {
            hovering = true;
            ApplyVisualState();
        }

        void MouseLeft(object? _, EventArgs __)
        {
            hovering = frame.ClientRectangle.Contains(frame.PointToClient(Cursor.Position));
            ApplyVisualState();
        }

        void FocusEntered(object? _, EventArgs __)
        {
            active = true;
            ApplyVisualState();
        }

        void FocusLeft(object? _, EventArgs __)
        {
            active = false;
            ApplyVisualState();
        }

        frame.Resize += (_, _) => LayoutControl();
        content.Resize += (_, _) => LayoutControl();
        frame.MouseEnter += MouseEntered;
        border.MouseEnter += MouseEntered;
        content.MouseEnter += MouseEntered;
        control.MouseEnter += MouseEntered;
        frame.MouseLeave += MouseLeft;
        border.MouseLeave += MouseLeft;
        content.MouseLeave += MouseLeft;
        control.MouseLeave += MouseLeft;
        control.Enter += FocusEntered;
        control.Leave += FocusLeft;
        content.Controls.Add(control);
        border.Controls.Add(content);
        frame.Controls.Add(border);
        LayoutControl();
        ApplyVisualState();
        return frame;
    }

    private static void StyleInputControl(Control control)
    {
        control.Font = new Font("Segoe UI", 9F);
        control.ForeColor = Color.FromArgb(28, 48, 54);
        control.BackColor = Color.White;
        control.Margin = new Padding(0);
        control.Dock = DockStyle.None;

        switch (control)
        {
            case TextBox textBox:
                textBox.BorderStyle = BorderStyle.None;
                break;
            case NumericUpDown numeric:
                numeric.BorderStyle = BorderStyle.None;
                numeric.TextAlign = HorizontalAlignment.Left;
                break;
            case ComboBox comboBox:
                comboBox.FlatStyle = FlatStyle.Flat;
                break;
        }
    }

    private static void StyleFormInputControl(Control control)
    {
        control.Font = new Font("Segoe UI", 9F);
        control.ForeColor = Color.FromArgb(28, 48, 54);
        control.BackColor = Color.White;
        control.Margin = new Padding(0);
        control.Dock = DockStyle.None;
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;

        switch (control)
        {
            case TextBox textBox:
                textBox.BorderStyle = BorderStyle.None;
                break;
            case NumericUpDown numeric:
                numeric.BorderStyle = BorderStyle.None;
                numeric.TextAlign = HorizontalAlignment.Left;
                break;
            case ComboBox comboBox:
                comboBox.FlatStyle = FlatStyle.Flat;
                break;
        }
    }

    private static void AddSearchRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 8, 0)
        }, 0, row);

        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 0, 0, 4);
        panel.Controls.Add(control, 1, row);
    }

    private static Label ValueLabel() => new()
    {
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Font = new Font("Segoe UI", 9F, FontStyle.Bold)
    };

    private sealed class SearchDatePicker : UserControl
    {
        private readonly TextBox _textBox = new()
        {
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(28, 48, 54),
            Font = new Font("Segoe UI", 9F)
        };

        private readonly Button _button = new()
        {
            Text = "v",
            Width = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = FormInputBackColor,
            ForeColor = Color.FromArgb(28, 48, 54),
            TabStop = false
        };

        private DateTime _value = DateTime.Today;
        private Form? _calendarPopup;

        public SearchDatePicker()
        {
            Height = 32;
            MinimumSize = new Size(0, 32);
            BackColor = Color.White;
            TabStop = true;

            _button.FlatAppearance.BorderSize = 0;
            _button.FlatAppearance.MouseOverBackColor = Color.FromArgb(244, 248, 249);
            _button.FlatAppearance.MouseDownBackColor = Color.FromArgb(236, 243, 244);
            _button.Click += (_, _) => ShowCalendar();
            _textBox.Click += (_, _) => ShowCalendar();

            Controls.Add(_textBox);
            Controls.Add(_button);
            UpdateText();
        }

        public void SetChromeBackColor(Color color)
        {
            BackColor = color;
            _textBox.BackColor = color;
            _button.BackColor = color;
        }

        public DateTime Value
        {
            get => _value;
            set
            {
                _value = value.Date;
                UpdateText();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _button.Bounds = new Rectangle(Math.Max(0, Width - _button.Width), 0, _button.Width, Height);
            var textHeight = Math.Min(20, Math.Max(18, Height - 4));
            _textBox.Bounds = new Rectangle(0, Math.Max(2, (Height - textHeight) / 2), Math.Max(20, Width - _button.Width - 4), textHeight);
        }

        private void ShowCalendar()
        {
            if (_calendarPopup is { IsDisposed: false, Visible: true })
            {
                _calendarPopup.Activate();
                return;
            }

            CloseCalendar();

            var popup = new CalendarPopup(Value, selectedDate =>
            {
                Value = selectedDate;
                CloseCalendar();
            });

            popup.Deactivate += (_, _) => CloseCalendar();
            popup.FormClosed += (_, _) =>
            {
                if (ReferenceEquals(_calendarPopup, popup))
                {
                    _calendarPopup = null;
                }
            };
            var location = PointToScreen(new Point(0, Height + 1));
            var screen = Screen.FromControl(this).WorkingArea;
            if (location.X + popup.Width > screen.Right)
            {
                location.X = Math.Max(screen.Left, screen.Right - popup.Width);
            }

            if (location.Y + popup.Height > screen.Bottom)
            {
                location.Y = Math.Max(screen.Top, PointToScreen(Point.Empty).Y - popup.Height - 1);
            }

            popup.Location = location;
            popup.TopMost = true;
            _calendarPopup = popup;

            popup.Show();
            popup.Activate();
        }

        private void CloseCalendar()
        {
            var popup = _calendarPopup;
            _calendarPopup = null;
            if (popup is null || popup.IsDisposed)
            {
                return;
            }

            popup.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseCalendar();
            }

            base.Dispose(disposing);
        }

        private void UpdateText()
        {
            _textBox.Text = _value.ToString("dd/MM/yyyy");
        }
    }

    private sealed class CalendarPopup : Form
    {
        private static readonly string[] WeekDays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
        private readonly Action<DateTime> _selectDate;
        private readonly Label _titleLabel = new()
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 48, 54)
        };
        private readonly TableLayoutPanel _calendarGrid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 7,
            BackColor = Color.White,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        private readonly DateTime _selectedDate;
        private DateTime _displayMonth;

        public CalendarPopup(DateTime selectedDate, Action<DateTime> selectDate)
        {
            _selectedDate = selectedDate.Date;
            _displayMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
            _selectDate = selectDate;

            AutoScaleMode = AutoScaleMode.None;
            BackColor = FormInputBorderColor;
            ClientSize = new Size(280, 238);
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(1);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8, 6, 8, 8)
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                Margin = new Padding(0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            header.Controls.Add(CalendarNavButton("<", (_, _) => MoveMonth(-1)), 0, 0);
            header.Controls.Add(_titleLabel, 1, 0);
            header.Controls.Add(CalendarNavButton(">", (_, _) => MoveMonth(1)), 2, 0);

            for (var i = 0; i < 7; i++)
            {
                _calendarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7f));
            }

            _calendarGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            for (var i = 1; i < 7; i++)
            {
                _calendarGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6f));
            }

            body.Controls.Add(header, 0, 0);
            body.Controls.Add(_calendarGrid, 0, 1);
            Controls.Add(body);
            RenderCalendar();
        }

        private static Button CalendarNavButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(28, 48, 54),
                TabStop = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += onClick;
            return button;
        }

        private void MoveMonth(int offset)
        {
            _displayMonth = _displayMonth.AddMonths(offset);
            RenderCalendar();
        }

        private void RenderCalendar()
        {
            _calendarGrid.SuspendLayout();
            _calendarGrid.Controls.Clear();
            _titleLabel.Text = _displayMonth.ToString("M yyyy");

            for (var column = 0; column < WeekDays.Length; column++)
            {
                _calendarGrid.Controls.Add(new Label
                {
                    Text = WeekDays[column],
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = MutedTextColor,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
                }, column, 0);
            }

            var start = _displayMonth.AddDays(-(int)_displayMonth.DayOfWeek);
            for (var index = 0; index < 42; index++)
            {
                var date = start.AddDays(index);
                var isCurrentMonth = date.Month == _displayMonth.Month;
                var isSelected = date.Date == _selectedDate;
                var button = new Button
                {
                    Text = date.Day.ToString(),
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(1),
                    BackColor = isSelected ? PrimaryBlue : Color.White,
                    ForeColor = isSelected
                        ? Color.White
                        : isCurrentMonth ? Color.FromArgb(28, 48, 54) : Color.FromArgb(150, 160, 164),
                    Font = new Font("Segoe UI", 8.5F),
                    TabStop = false
                };
                button.FlatAppearance.BorderColor = date.Date == DateTime.Today ? FormInputBorderColor : Color.White;
                button.FlatAppearance.BorderSize = date.Date == DateTime.Today && !isSelected ? 1 : 0;
                button.Click += (_, _) => _selectDate(date.Date);
                _calendarGrid.Controls.Add(button, index % 7, (index / 7) + 1);
            }

            _calendarGrid.ResumeLayout();
        }
    }

    private sealed class PendingLogRow
    {
        public int Id { get; init; }

        public string DeviceId { get; init; } = string.Empty;

        public string StoreCode { get; init; } = string.Empty;

        public string EmployeeCode { get; init; } = string.Empty;

        public string PunchTime { get; init; } = string.Empty;

        public string VerifyText { get; init; } = string.Empty;

        public string StateText { get; init; } = string.Empty;

        public int RetryCount { get; init; }

        public string LastError { get; init; } = string.Empty;

        public string UpdatedAt { get; init; } = string.Empty;

        public static PendingLogRow From(PendingLog log)
        {
            var parsed = ParsedVerifyType.Parse(log.VerifyType);
            return new PendingLogRow
            {
                Id = log.Id,
                DeviceId = log.DeviceId,
                StoreCode = log.StoreCode,
                EmployeeCode = log.EmployeeCode,
                PunchTime = FormatDateTimeText(log.PunchTime),
                VerifyText = parsed.VerifyText,
                StateText = parsed.StateText,
                RetryCount = log.RetryCount,
                LastError = log.LastError,
                UpdatedAt = FormatDateTimeText(log.UpdatedAt)
            };
        }
    }

    private sealed class SyncLogRow
    {
        public string Time { get; init; } = string.Empty;

        public string Location { get; init; } = string.Empty;

        public string IpAddress { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string FromDate { get; init; } = string.Empty;

        public string ToDate { get; init; } = string.Empty;

        public string Result { get; init; } = string.Empty;

        public string Data { get; init; } = string.Empty;

        public string Note { get; init; } = string.Empty;

        public static SyncLogRow From(SyncLog log, Device? device) => new()
        {
            Time = FormatDateTimeText(log.StartedAt),
            Location = string.IsNullOrWhiteSpace(device?.StoreCode) ? log.DeviceId : device.StoreCode,
            IpAddress = device?.IpAddress ?? string.Empty,
            Content = GetContent(log),
            FromDate = "",
            ToDate = "",
            Result = TranslateStatus(log.Status),
            Data = GetData(log),
            Note = log.ErrorMessage
        };

        private static string GetContent(SyncLog log)
        {
            if (log.TotalRead > 0 && log.TotalSent > 0)
            {
                return "Lấy và đẩy log";
            }

            if (log.TotalSent > 0)
            {
                return "Đẩy log";
            }

            return log.TotalRead > 0 ? "Lấy log" : "Kiểm tra";
        }

        private static string GetData(SyncLog log)
        {
            var parts = new List<string>();
            if (log.TotalRead > 0)
            {
                parts.Add($"{log.TotalRead} đọc");
            }

            if (log.TotalSent > 0)
            {
                parts.Add($"{log.TotalSent} gửi");
            }

            if (log.TotalFailed > 0)
            {
                parts.Add($"{log.TotalFailed} lỗi");
            }

            return parts.Count == 0 ? "0 log" : string.Join(", ", parts);
        }
    }

    private sealed class DeviceHomeRow
    {
        public int No { get; init; }

        public string Location { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public int LogCount { get; init; }

        public string UserCount { get; init; } = string.Empty;

        public string LastLoadedAt { get; init; } = string.Empty;

        public string LoadFrom { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public string IpAddress { get; init; } = string.Empty;

        public int Port { get; init; }

        public string Serial { get; init; } = string.Empty;

        public string DeviceType { get; init; } = string.Empty;

        public static DeviceHomeRow From(Device device, int no, int pendingLogs)
        {
            return new DeviceHomeRow
            {
                No = no,
                Location = device.StoreCode,
                Status = string.IsNullOrWhiteSpace(device.LastError) ? "San sang" : "Co loi",
                LogCount = pendingLogs,
                UserCount = "",
                LastLoadedAt = FormatDateTimeText(device.LastSuccessSyncAt ?? string.Empty),
                LoadFrom = "",
                Code = device.DeviceId,
                IpAddress = device.IpAddress,
                Port = device.Port,
                Serial = "",
                DeviceType = device.DeviceTypeName
            };
        }
    }

    private sealed class AppErrorRow
    {
        public int Id { get; init; }

        public string ErrorType { get; init; } = string.Empty;

        public string DeviceId { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public string Detail { get; init; } = string.Empty;

        public string CreatedAt { get; init; } = string.Empty;

        public static AppErrorRow From(AppError error) => new()
        {
            Id = error.Id,
            ErrorType = error.ErrorType,
            DeviceId = error.DeviceId,
            Message = error.Message,
            Detail = error.Detail,
            CreatedAt = FormatDateTimeText(error.CreatedAt)
        };
    }

    private sealed record ParsedVerifyType(string VerifyText, string StateText)
    {
        public static ParsedVerifyType Parse(string raw)
        {
            var values = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Split(':', 2))
                .Where(parts => parts.Length == 2 && int.TryParse(parts[1], out _))
                .ToDictionary(parts => parts[0], parts => int.Parse(parts[1]), StringComparer.OrdinalIgnoreCase);

            var verify = values.GetValueOrDefault("verify", -1);
            var state = values.GetValueOrDefault("state", -1);
            var work = values.GetValueOrDefault("work", 0);

            var verifyText = verify switch
            {
                0 => "Mật khẩu",
                1 => "Vân tay",
                2 => "Thẻ",
                3 => "Mật khẩu",
                4 => "Thẻ",
                15 => "Khuôn mặt",
                _ when string.IsNullOrWhiteSpace(raw) => "",
                _ => $"Mã xác minh {verify}"
            };

            var stateText = state switch
            {
                0 => "Vào/ra mặc định",
                1 => "Vào",
                2 => "Ra",
                3 => "Tăng ca vào",
                4 => "Tăng ca ra",
                5 => "Ra ngoài",
                _ => state < 0 ? "" : $"Trạng thái {state}"
            };

            if (work > 0)
            {
                stateText = string.IsNullOrWhiteSpace(stateText)
                    ? $"Work {work}"
                    : $"{stateText} | Work {work}";
            }

            return new ParsedVerifyType(verifyText, stateText);
        }
    }

    private static string FormatDateTimeText(string value)
    {
        return DateTime.TryParse(value, out var dateTime)
            ? dateTime.ToString("yyyy-MM-dd HH:mm:ss")
            : value;
    }

    private static string TranslateStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "SUCCESS" => "Thành công",
            "PARTIAL" => "Có lỗi",
            "FAILED" => "Thất bại",
            _ => status
        };
    }
}
