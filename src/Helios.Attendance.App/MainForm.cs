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
    private static readonly Color PrimaryBlue = Color.FromArgb(45, 124, 190);
    private static readonly Color MenuBorderColor = Color.FromArgb(205, 205, 205);

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
    private readonly NumericUpDown _readBackDaysInput = new()
    {
        Minimum = 0,
        Maximum = 365,
        Value = 1
    };

    private readonly DataGridView _historyGrid = Grid();
    private readonly DataGridView _pendingGrid = Grid();
    private readonly DataGridView _errorsGrid = Grid();
    private readonly TextBox _searchEmployeeText = new();
    private readonly DateTimePicker _searchFromDate = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy"
    };
    private readonly DateTimePicker _searchToDate = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy"
    };

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

    private void InitializeLayout()
    {
        SetStatus("Sẵn sàng", StatusReadyColor);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        ConfigurePageHost();

        _tabs.TabPages.Add(BuildOverviewTab());
        _tabs.TabPages.Add(BuildDevicesTab());
        _tabs.TabPages.Add(BuildPendingTab());
        _tabs.TabPages.Add(BuildHistoryTab());
        _tabs.TabPages.Add(BuildApiTab());
        _tabs.TabPages.Add(BuildErrorsTab());
        _tabs.SelectedIndexChanged += (_, _) => UpdateMenuState();

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_statusText);

        root.Controls.Add(BuildTopNavigation(), 0, 0);
        root.Controls.Add(_tabs, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        root.Controls.Add(statusStrip, 0, 3);
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

    private Control BuildTopNavigation()
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14, 8, 10, 8),
            BackColor = Color.White
        };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "HOFFICE.VN",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(70, 70, 70),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            WrapContents = false,
            Margin = new Padding(0)
        };

        menu.Controls.Add(MenuButton("⌂", 0, 38));
        menu.Controls.Add(MenuButton("Thêm máy", 1));
        menu.Controls.Add(MenuButton("Tìm kiếm", 2));
        menu.Controls.Add(MenuButton("Lịch sử", 3));
        menu.Controls.Add(MenuButton("Cài đặt", 4));
        menu.Controls.Add(MenuButton("Hướng dẫn", 5));
        menu.Controls.Add(MenuButton("Xuất/Nhập DS thiết bị", 1, 150));

        var syncButton = new Button
        {
            Text = "Tải/đẩy log tự động",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = PrimaryBlue,
            ForeColor = Color.White,
            Margin = new Padding(0, 2, 0, 2),
            Height = 34
        };
        syncButton.FlatAppearance.BorderSize = 0;
        syncButton.Click += async (_, _) => await SyncNowAsync();

        row.Controls.Add(menu, 0, 0);
        row.Controls.Add(syncButton, 1, 0);
        outer.Controls.Add(title, 0, 0);
        outer.Controls.Add(row, 0, 1);
        return outer;
    }

    private Button MenuButton(string text, int pageIndex, int width = 92)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            Margin = new Padding(0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.Black,
            Tag = pageIndex
        };
        button.FlatAppearance.BorderColor = MenuBorderColor;
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => ShowPage(pageIndex);
        _menuButtons.Add(button);
        return button;
    }

    private Control BuildFooter()
    {
        return new Label
        {
            Text = "Bản quyền thuộc HOFFICE © | Phiên bản 2.0",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 3, 12, 0),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
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
            button.BackColor = selected ? Color.FromArgb(245, 245, 245) : Color.White;
            button.FlatAppearance.BorderColor = selected ? PrimaryBlue : MenuBorderColor;
            button.ForeColor = selected ? PrimaryBlue : Color.Black;
        }
    }

    private TabPage BuildOverviewTab()
    {
        var tab = new TabPage("Tong quan");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var search = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 10, 8)
        };
        _homeSearchText.Width = 160;
        _homeSearchText.Margin = new Padding(8, 0, 0, 0);
        _homeSearchText.TextChanged += (_, _) => RefreshHome();
        search.Controls.Add(_homeSearchText);
        search.Controls.Add(new Label
        {
            Text = "Tim kiem",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 3, 0, 0)
        });

        layout.Controls.Add(search, 0, 0);
        layout.Controls.Add(_homeGrid, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }
    private TabPage BuildDevicesTab()
    {
        var tab = new TabPage("Thiết bị");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _devicesGrid.SelectionChanged += (_, _) => LoadSelectedDeviceIntoForm();
        var gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 12, 0)
        };
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
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2
        };
        sideLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sideLayout.Controls.Add(form, 0, 0);
        sideLayout.Controls.Add(buttons, 0, 1);

        var group = new GroupBox
        {
            Text = "Cấu hình máy",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        group.Controls.Add(sideLayout);
        layout.Controls.Add(group, 1, 0);

        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildApiTab()
    {
        var tab = new TabPage("API");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
            AutoSize = true
        };

        var form = FormGrid();
        AddRow(form, "API URL", _apiUrlText);
        AddRow(form, "API Token", _apiTokenText);
        AddRow(form, "Timeout giây", _apiTimeoutInput);
        AddRow(form, "Chu kỳ phút", _syncIntervalInput);
        AddRow(form, "Đọc lùi ngày", _readBackDaysInput);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        actions.Controls.Add(Button("Test API", async (_, _) => await TestApiAsync()));
        actions.Controls.Add(Button("Lưu cấu hình", (_, _) => SaveApiSettingsFromForm()));

        layout.Controls.Add(form, 0, 0);
        layout.Controls.Add(actions, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildHistoryTab()
    {
        var tab = new TabPage("Lịch sử");
        tab.Controls.Add(BuildGridPanel(_historyGrid, Button("Refresh", (_, _) => RefreshHistory())));
        return tab;
    }

    private TabPage BuildPendingTab()
    {
        var tab = new TabPage("Tim kiem");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 172));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(170, 28, 200, 0)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        AddSearchRow(form, 0, "Ma cham cong", _searchEmployeeText);
        AddSearchRow(form, 1, "Tu Ngay", _searchFromDate);
        AddSearchRow(form, 2, "Den ngay", _searchToDate);

        var searchButton = Button("Tim kiem", (_, _) => RefreshPending());
        searchButton.Width = 100;
        searchButton.Anchor = AnchorStyles.Top;
        form.Controls.Add(searchButton, 1, 3);

        var title = new Label
        {
            Text = "Danh sach log cham cong",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 0, 0, 0),
            TextAlign = ContentAlignment.BottomLeft
        };

        layout.Controls.Add(form, 0, 0);
        layout.Controls.Add(title, 0, 1);
        layout.Controls.Add(_pendingGrid, 0, 2);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildPendingTabOld()
    {
        var tab = new TabPage("Pending");
        var retryButton = Button("Gửi lại ngay", async (_, _) => await SyncNowAsync());
        var clearButton = Button("Xóa pending", (_, _) => ClearPendingLogs());
        tab.Controls.Add(BuildGridPanel(_pendingGrid, retryButton, clearButton, Button("Refresh", (_, _) => RefreshPending())));
        return tab;
    }

    private TabPage BuildErrorsTab()
    {
        var tab = new TabPage("Huong dan");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 8)
        };
        actions.Controls.Add(Button("Cai driver", async (_, _) => await InstallDeviceDriverAsync()));
        actions.Controls.Add(Button("Cai/Cap nhat Service", async (_, _) => await InstallServiceAsync()));
        actions.Controls.Add(Button("Restart Service", async (_, _) => await RestartServiceAsync()));
        actions.Controls.Add(Button("Mo thu muc log", (_, _) => OpenDataFolder()));
        actions.Controls.Add(Button("Refresh", (_, _) => RefreshDynamicData()));

        layout.Controls.Add(actions, 0, 0);
        layout.Controls.Add(_outputText, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildErrorsTabOld()
    {
        var tab = new TabPage("Lỗi");
        tab.Controls.Add(BuildGridPanel(_errorsGrid, Button("Refresh", (_, _) => RefreshErrors())));
        return tab;
    }

    private static Control BuildGridPanel(DataGridView grid, params Button[] buttons)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(12)
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

        layout.Controls.Add(actions, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        return layout;
    }

    private static GroupBox BuildSummaryGroup(string title, params (string Label, Control Value)[] rows)
    {
        var form = CompactFormGrid();
        foreach (var (label, value) in rows)
        {
            AddRow(form, label, value);
        }

        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 10, 0)
        };
        group.Controls.Add(form);
        return group;
    }

    private async Task SyncNowAsync()
    {
        SaveApiSettingsFromForm(showMessage: false);
        await RunBusyAsync("Đang đồng bộ...", async () =>
        {
            AppendOutput("Bắt đầu đồng bộ thủ công.");
            var result = await _syncEngine.RunOnceAsync(CancellationToken.None, AppendOutput);
            AppendOutput($"{result.Message} Đọc={result.TotalRead}, gửi={result.TotalSent}, lỗi={result.TotalFailed}, pending={result.PendingCreated}.");
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

            await Task.Run(() =>
            {
                using var service = new ServiceController(AppPaths.ServiceName);
                if (service.Status is not ServiceControllerStatus.Stopped and not ServiceControllerStatus.StopPending)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            });
            AppendOutput("Restart service thành công.");
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
            AppendOutput("Đã cài và khởi động service nền.");
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
        RefreshOverview();
    }

    private void LoadSettingsIntoForm()
    {
        var settings = _store.GetApiSettings();
        _apiUrlText.Text = settings.ApiUrl;
        _apiTokenText.Text = settings.ApiToken;
        _apiTimeoutInput.Value = Math.Clamp(settings.TimeoutSeconds, 1, 300);
        _syncIntervalInput.Value = Math.Clamp(_store.GetSyncIntervalMinutes(), 1, 1440);
        _readBackDaysInput.Value = Math.Clamp(_store.GetReadBackDays(), 0, 365);
        _searchFromDate.Value = DateTime.Today.AddDays(-Math.Max(1, (int)_readBackDaysInput.Value));
        _searchToDate.Value = DateTime.Today;
    }

    private void SaveApiSettingsFromForm(bool showMessage = true)
    {
        _store.SaveApiSettings(ReadApiSettingsFromForm());
        _store.SaveSyncSettings((int)_syncIntervalInput.Value, (int)_readBackDaysInput.Value);
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
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Location), "Dia diem", 120);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Status), "Trang thai", 130);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.LogCount), "So log", 80);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.UserCount), "Nguoi dung", 95);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.LastLoadedAt), "Lan tai cuoi", 145);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.LoadFrom), "Tai tu ngay", 120);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Code), "Ma", 80);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.IpAddress), "IP", 120);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Port), "Port", 70);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.Serial), "Serial", 110);
        SetGridColumn(_homeGrid, nameof(DeviceHomeRow.DeviceType), "Loai May", 130);
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
        var from = _searchFromDate.Value.Date;
        var toExclusive = _searchToDate.Value.Date.AddDays(1);

        _pendingGrid.DataSource = null;
        _pendingGrid.DataSource = _store.GetPendingLogs()
            .Where(log => string.IsNullOrWhiteSpace(employee) ||
                log.EmployeeCode.Contains(employee, StringComparison.OrdinalIgnoreCase))
            .Where(log => !DateTime.TryParse(log.PunchTime, out var punchTime) ||
                (punchTime >= from && punchTime < toExclusive))
            .Select(PendingLogRow.From)
            .ToList();
        FormatPendingGrid();
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
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Time), "Thoi gian", 145);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Location), "Dia diem", 130);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.IpAddress), "IP", 120);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Content), "Noi dung", 140);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.FromDate), "Tu ngay", 110);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.ToDate), "Den ngay", 110);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Result), "Ket qua", 120);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Data), "Du lieu", 140);
        SetGridColumn(_historyGrid, nameof(SyncLogRow.Note), "Ghi chu", 300);
    }

    private void FormatPendingGrid()
    {
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.Id), "#", 48);
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.DeviceId), "Máy", 80);
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.StoreCode), "Chi nhánh", 110);
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.EmployeeCode), "Mã nhân viên", 110);
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.PunchTime), "Thời gian chấm", 150);
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.VerifyText), "Kiểu chấm", 150);
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.StateText), "Vào/Ra", 120);
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.RetryCount), "Lần gửi", 70);
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.LastError), "Lỗi gửi", 260);
        SetGridColumn(_pendingGrid, nameof(PendingLogRow.UpdatedAt), "Cập nhật", 145);
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
            using var service = new ServiceController(AppPaths.ServiceName);
            return service.Status.ToString();
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
            Margin = new Padding(0, 0, 8, 8)
        };
        button.Click += onClick;
        return button;
    }

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static TableLayoutPanel FormGrid()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return panel;
    }

    private static TableLayoutPanel CompactFormGrid()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return panel;
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, row);

        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 3, 0, 3);
        panel.Controls.Add(control, 1, row);
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
            Content = log.TotalSent > 0 ? "Day log" : "Tai log",
            FromDate = "",
            ToDate = "",
            Result = TranslateStatus(log.Status),
            Data = $"{log.TotalRead} logs",
            Note = log.ErrorMessage
        };
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
