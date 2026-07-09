using System.Globalization;
using System.Net.Sockets;
using Helios.Attendance.Core.Models;

namespace Helios.Attendance.Core.Devices;

public sealed class TcpAttendanceDeviceClient : IAttendanceDeviceClient
{
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

    public async Task<IReadOnlyList<AttendancePunch>> ReadLogsAsync(
        Device device,
        DateTime fromTime,
        CancellationToken cancellationToken)
    {
        var connection = await TestConnectionAsync(device, cancellationToken);
        if (!connection.Success)
        {
            throw new InvalidOperationException(connection.Message);
        }

        return ReadSampleCsv(device, fromTime);
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
}
