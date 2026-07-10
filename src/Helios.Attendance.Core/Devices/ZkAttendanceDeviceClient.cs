using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Helios.Attendance.Core.Models;

namespace Helios.Attendance.Core.Devices;

public sealed class ZkAttendanceDeviceClient : IAttendanceDeviceClient
{
    private const string ComProgId = "zkemkeeper.CZKEM";
    private const int MachineNumber = 1;

    private static readonly string[] DateFormats =
    [
        DateTimeText.StorageFormat,
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff"
    ];

    public async Task<DeviceConnectionResult> TestConnectionAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(device.IpAddress))
        {
            return DeviceConnectionResult.Fail("Chưa nhập IP thiết bị.");
        }

        var sdkType = GetSdkType();
        if (sdkType is null)
        {
            var tcpResult = await TestTcpPortAsync(device, cancellationToken);
            var prefix = tcpResult.Success
                ? $"Port {device.IpAddress}:{device.Port} đang mở, nhưng "
                : string.Empty;

            return DeviceConnectionResult.Fail(
                prefix + "máy này chưa có driver ZK SDK (zkemkeeper.CZKEM) đúng kiến trúc để đọc log. Hãy bấm Cài SDK ZK trong tab Thiết bị.");
        }

        try
        {
            await RunInStaAsync(
                () =>
                {
                    using var session = ZkSession.Connect(sdkType, device);
                    return true;
                },
                cancellationToken);

            return DeviceConnectionResult.Ok($"Kết nối SDK ZK thành công {device.IpAddress}:{device.Port}.");
        }
        catch (Exception ex)
        {
            return DeviceConnectionResult.Fail(ex.Message);
        }
    }

    public async Task<IReadOnlyList<AttendancePunch>> ReadLogsAsync(
        Device device,
        DateTime fromTime,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(device.IpAddress))
        {
            throw new InvalidOperationException("Chưa nhập IP thiết bị.");
        }

        var sdkType = GetSdkType();
        if (sdkType is null)
        {
            var sampleLogs = ReadSampleCsv(device, fromTime);
            if (sampleLogs.Count > 0)
            {
                return sampleLogs;
            }

            throw new InvalidOperationException(
                "Chưa cài hoặc chưa đăng ký driver ZK SDK (zkemkeeper.CZKEM). " +
                "App có thể mở được IP/port nhưng không thể đọc log nếu thiếu driver này. " +
                "Hãy bấm Cài SDK ZK trong tab Thiết bị.");
        }

        return await RunInStaAsync(
            () =>
            {
                using var session = ZkSession.Connect(sdkType, device);
                return session.ReadLogs(device, fromTime);
            },
            cancellationToken);
    }

    private static Type? GetSdkType()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return Type.GetTypeFromProgID(ComProgId);
    }

    private static async Task<DeviceConnectionResult> TestTcpPortAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            using var client = new TcpClient();
            await client.ConnectAsync(device.IpAddress, device.Port, timeout.Token);
            return DeviceConnectionResult.Ok($"Kết nối được {device.IpAddress}:{device.Port}.");
        }
        catch (OperationCanceledException)
        {
            return DeviceConnectionResult.Fail($"Timeout khi kết nối {device.IpAddress}:{device.Port}.");
        }
        catch (Exception ex)
        {
            return DeviceConnectionResult.Fail(ex.Message);
        }
    }

    private static async Task<T> RunInStaAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "HOFFICE ZK SDK"
        };

        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }

        thread.Start();

        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return await completion.Task.ConfigureAwait(false);
    }

    private static IReadOnlyList<AttendancePunch> ReadSampleCsv(Device device, DateTime fromTime)
    {
        if (!File.Exists(AppPaths.SampleLogPath))
        {
            return [];
        }

        var punches = new List<AttendancePunch>();
        foreach (var line in File.ReadLines(AppPaths.SampleLogPath).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 5)
            {
                continue;
            }

            var deviceId = parts[0].Trim();
            if (!string.Equals(deviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!DateTime.TryParseExact(
                parts[3].Trim(),
                DateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var punchTime))
            {
                continue;
            }

            if (punchTime < fromTime)
            {
                continue;
            }

            punches.Add(new AttendancePunch
            {
                DeviceId = device.DeviceId,
                StoreCode = string.IsNullOrWhiteSpace(parts[1]) ? device.StoreCode : parts[1].Trim(),
                EmployeeCode = parts[2].Trim(),
                PunchTime = punchTime,
                VerifyType = parts[4].Trim(),
                RawPayload = line
            });
        }

        return punches;
    }

    private sealed class ZkSession : IDisposable
    {
        private readonly dynamic _sdk;
        private bool _connected;
        private bool _deviceDisabled;

        private ZkSession(dynamic sdk)
        {
            _sdk = sdk;
        }

        public static ZkSession Connect(Type sdkType, Device device)
        {
            dynamic sdk = Activator.CreateInstance(sdkType)
                ?? throw new InvalidOperationException("Không khởi tạo được ZK SDK.");

            var session = new ZkSession(sdk);
            try
            {
                if (device.Password > 0)
                {
                    sdk.SetCommPassword(device.Password);
                }

                if (!sdk.Connect_Net(device.IpAddress, device.Port))
                {
                    throw new InvalidOperationException(
                        $"Không đăng nhập được máy {device.IpAddress}:{device.Port}. {session.GetLastErrorText()}");
                }

                session._connected = true;
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        public IReadOnlyList<AttendancePunch> ReadLogs(Device device, DateTime fromTime)
        {
            DisableDevice();

            if (!_sdk.ReadGeneralLogData(MachineNumber))
            {
                var errorCode = GetLastErrorCode();
                if (errorCode is 0 or -100)
                {
                    return [];
                }

                throw new InvalidOperationException($"SDK ZK không đọc được log. {GetLastErrorText(errorCode)}");
            }

            try
            {
                return ReadSsrLogs(device, fromTime);
            }
            catch (Exception ex) when (IsMissingComMember(ex))
            {
                return ReadClassicLogs(device, fromTime);
            }
        }

        private IReadOnlyList<AttendancePunch> ReadSsrLogs(Device device, DateTime fromTime)
        {
            var punches = new List<AttendancePunch>();

            while (true)
            {
                string enrollNumber = string.Empty;
                var verifyMode = 0;
                var inOutMode = 0;
                var year = 0;
                var month = 0;
                var day = 0;
                var hour = 0;
                var minute = 0;
                var second = 0;
                var workCode = 0;

                if (!_sdk.SSR_GetGeneralLogData(
                    MachineNumber,
                    out enrollNumber,
                    out verifyMode,
                    out inOutMode,
                    out year,
                    out month,
                    out day,
                    out hour,
                    out minute,
                    out second,
                    ref workCode))
                {
                    break;
                }

                AddPunch(
                    punches,
                    device,
                    enrollNumber,
                    verifyMode,
                    inOutMode,
                    workCode,
                    year,
                    month,
                    day,
                    hour,
                    minute,
                    second,
                    fromTime);
            }

            return punches
                .OrderBy(item => item.PunchTime)
                .ThenBy(item => item.EmployeeCode, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private IReadOnlyList<AttendancePunch> ReadClassicLogs(Device device, DateTime fromTime)
        {
            var punches = new List<AttendancePunch>();

            while (true)
            {
                var terminalMachineNumber = 0;
                var enrollNumber = 0;
                var enrollMachineNumber = 0;
                var verifyMode = 0;
                var inOutMode = 0;
                var year = 0;
                var month = 0;
                var day = 0;
                var hour = 0;
                var minute = 0;

                if (!_sdk.GetGeneralLogData(
                    MachineNumber,
                    ref terminalMachineNumber,
                    ref enrollNumber,
                    ref enrollMachineNumber,
                    ref verifyMode,
                    ref inOutMode,
                    ref year,
                    ref month,
                    ref day,
                    ref hour,
                    ref minute))
                {
                    break;
                }

                AddPunch(
                    punches,
                    device,
                    enrollNumber.ToString(CultureInfo.InvariantCulture),
                    verifyMode,
                    inOutMode,
                    0,
                    year,
                    month,
                    day,
                    hour,
                    minute,
                    0,
                    fromTime);
            }

            return punches
                .OrderBy(item => item.PunchTime)
                .ThenBy(item => item.EmployeeCode, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void AddPunch(
            List<AttendancePunch> punches,
            Device device,
            string employeeCode,
            int verifyMode,
            int inOutMode,
            int workCode,
            int year,
            int month,
            int day,
            int hour,
            int minute,
            int second,
            DateTime fromTime)
        {
            if (string.IsNullOrWhiteSpace(employeeCode) ||
                !TryCreateDateTime(year, month, day, hour, minute, second, out var punchTime) ||
                punchTime < fromTime)
            {
                return;
            }

            punches.Add(new AttendancePunch
            {
                DeviceId = device.DeviceId,
                StoreCode = device.StoreCode,
                EmployeeCode = employeeCode.Trim(),
                PunchTime = punchTime,
                VerifyType = FormatVerifyType(verifyMode, inOutMode, workCode),
                RawPayload = string.Join(
                    "|",
                    employeeCode.Trim(),
                    DateTimeText.Format(punchTime),
                    verifyMode.ToString(CultureInfo.InvariantCulture),
                    inOutMode.ToString(CultureInfo.InvariantCulture),
                    workCode.ToString(CultureInfo.InvariantCulture))
            });
        }

        private void DisableDevice()
        {
            try
            {
                _sdk.EnableDevice(MachineNumber, false);
                _deviceDisabled = true;
            }
            catch
            {
                _deviceDisabled = false;
            }
        }

        private void EnableDevice()
        {
            if (!_deviceDisabled)
            {
                return;
            }

            try
            {
                _sdk.EnableDevice(MachineNumber, true);
            }
            catch
            {
                // The connection may already be gone. Disconnect below still releases the COM object.
            }
        }

        private int GetLastErrorCode()
        {
            try
            {
                var errorCode = 0;
                _sdk.GetLastError(ref errorCode);
                return errorCode;
            }
            catch
            {
                return 0;
            }
        }

        private string GetLastErrorText(int? errorCode = null)
        {
            var code = errorCode ?? GetLastErrorCode();
            return code == 0
                ? "Không có mã lỗi SDK."
                : $"Mã lỗi SDK: {code}.";
        }

        public void Dispose()
        {
            EnableDevice();

            if (_connected)
            {
                try
                {
                    _sdk.Disconnect();
                }
                catch
                {
                    // Ignore disconnect errors while disposing the SDK session.
                }

                _connected = false;
            }

            try
            {
                if (Marshal.IsComObject(_sdk))
                {
                    Marshal.FinalReleaseComObject(_sdk);
                }
            }
            catch
            {
                // Nothing actionable here.
            }
        }
    }

    private static bool TryCreateDateTime(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        out DateTime value)
    {
        try
        {
            value = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private static string FormatVerifyType(int verifyMode, int inOutMode, int workCode) =>
        $"verify:{verifyMode};state:{inOutMode};work:{workCode}";

    private static bool IsMissingComMember(Exception ex)
    {
        if (ex is COMException comException)
        {
            return unchecked((uint)comException.ErrorCode) == 0x80020003;
        }

        return ex.GetType().FullName == "Microsoft.CSharp.RuntimeBinder.RuntimeBinderException" &&
            ex.Message.Contains("definition", StringComparison.OrdinalIgnoreCase);
    }
}
