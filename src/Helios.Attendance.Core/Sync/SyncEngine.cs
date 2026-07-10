using Helios.Attendance.Core.Data;
using Helios.Attendance.Core.Devices;
using Helios.Attendance.Core.Models;

namespace Helios.Attendance.Core.Sync;

public sealed class SyncEngine
{
    private const int BatchSize = 200;

    private readonly AttendanceSyncStore _store;
    private readonly IAttendanceDeviceClient _deviceClient;

    public SyncEngine(AttendanceSyncStore store, IAttendanceDeviceClient deviceClient)
    {
        _store = store;
        _deviceClient = deviceClient;
    }

    public async Task<SyncRunResult> RunOnceAsync(
        CancellationToken cancellationToken,
        Action<string>? progress = null)
    {
        var pollResult = await PollDevicesAsync(cancellationToken, progress);
        var pushResult = await PushPendingAsync(cancellationToken, progress);

        return new SyncRunResult
        {
            DeviceCount = Math.Max(pollResult.DeviceCount, pushResult.DeviceCount),
            TotalRead = pollResult.TotalRead,
            TotalSent = pushResult.TotalSent,
            TotalInserted = pushResult.TotalInserted,
            TotalDuplicated = pushResult.TotalDuplicated,
            TotalFailed = pollResult.TotalFailed + pushResult.TotalFailed,
            PendingCreated = pushResult.PendingCreated,
            Success = pollResult.Success && pushResult.Success,
            Message = pollResult.Success && pushResult.Success
                ? "Da lay log va day du lieu xong."
                : "Da chay xong nhung co loi can kiem tra."
        };
    }

    public async Task<SyncRunResult> PollDevicesAsync(
        CancellationToken cancellationToken,
        Action<string>? progress = null)
    {
        _store.Initialize();

        var devices = _store.GetDevices(activeOnly: true);
        if (devices.Count == 0)
        {
            progress?.Invoke("Chua co thiet bi active.");
            return new SyncRunResult
            {
                Success = true,
                Message = "Chua co thiet bi active."
            };
        }

        var readBackDays = _store.GetReadBackDays();
        var pendingBefore = _store.GetPendingLogCount();
        var totalRead = 0;
        var totalFailed = 0;
        var allSucceeded = true;

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke($"Lay log {device.DeviceId}...");

            var startedAt = DateTimeText.Now();
            var deviceRead = 0;
            var deviceFailed = 0;
            var status = "success";
            var errorMessage = string.Empty;

            try
            {
                var fromTime = GetReadFromTime(device, readBackDays);
                var logs = await _deviceClient.ReadLogsAsync(device, fromTime, cancellationToken);
                deviceRead = logs.Count;
                progress?.Invoke($"{device.DeviceId}: doc duoc {deviceRead} log tu {DateTimeText.Format(fromTime)}.");

                if (logs.Count > 0)
                {
                    _store.UpsertPendingLogs(logs, "Cho day len server.");
                    progress?.Invoke($"{device.DeviceId}: da luu {logs.Count} log vao danh sach cho day.");
                }

                _store.MarkDeviceSuccess(device.DeviceId, DateTimeText.Now());
            }
            catch (Exception ex)
            {
                allSucceeded = false;
                status = "error";
                errorMessage = ex.Message;
                deviceFailed = 1;
                _store.MarkDeviceError(device.DeviceId, ex.Message);
                _store.InsertAppError("POLL_ERROR", device.DeviceId, ex.Message, ex.ToString());
                progress?.Invoke($"{device.DeviceId}: loi lay log {ex.Message}");
            }
            finally
            {
                _store.InsertSyncLog(new SyncLog
                {
                    DeviceId = device.DeviceId,
                    StartedAt = startedAt,
                    FinishedAt = DateTimeText.Now(),
                    Status = status,
                    TotalRead = deviceRead,
                    TotalSent = 0,
                    TotalInserted = 0,
                    TotalDuplicated = 0,
                    TotalFailed = deviceFailed,
                    ErrorMessage = errorMessage
                });

                totalRead += deviceRead;
                totalFailed += deviceFailed;
            }
        }

        var pendingAfter = _store.GetPendingLogCount();

        return new SyncRunResult
        {
            DeviceCount = devices.Count,
            TotalRead = totalRead,
            TotalSent = 0,
            TotalInserted = 0,
            TotalDuplicated = 0,
            TotalFailed = totalFailed,
            PendingCreated = Math.Max(0, pendingAfter - pendingBefore),
            Success = allSucceeded,
            Message = allSucceeded ? "Lay log hoan tat." : "Lay log xong nhung co loi can kiem tra."
        };
    }

    public async Task<SyncRunResult> PushPendingAsync(
        CancellationToken cancellationToken,
        Action<string>? progress = null)
    {
        _store.Initialize();

        var devices = _store.GetDevices(activeOnly: true);
        if (devices.Count == 0)
        {
            progress?.Invoke("Chua co thiet bi active.");
            return new SyncRunResult
            {
                Success = true,
                Message = "Chua co thiet bi active."
            };
        }

        var apiClient = new AttendanceApiClient(_store.GetApiSettings());
        var totalSent = 0;
        var totalInserted = 0;
        var totalDuplicated = 0;
        var totalFailed = 0;
        var allSucceeded = true;

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startedAt = DateTimeText.Now();
            var status = "success";
            var errorMessage = string.Empty;
            var deviceSent = 0;
            var deviceInserted = 0;
            var deviceDuplicated = 0;
            var deviceFailed = 0;

            try
            {
                var attemptedAny = false;

                while (true)
                {
                    var pendingResult = await SendPendingAsync(device, apiClient, cancellationToken, progress);
                    if (pendingResult.Attempted == 0)
                    {
                        if (!attemptedAny)
                        {
                            progress?.Invoke($"{device.DeviceId}: khong co log cho day.");
                        }

                        break;
                    }

                    attemptedAny = true;
                    deviceSent += pendingResult.Sent;
                    deviceInserted += pendingResult.Inserted;
                    deviceDuplicated += pendingResult.Duplicated;
                    deviceFailed += pendingResult.Failed;

                    if (!pendingResult.Success)
                    {
                        allSucceeded = false;
                        status = deviceSent > 0 ? "warning" : "error";
                        errorMessage = pendingResult.Message;
                        _store.MarkDeviceError(device.DeviceId, pendingResult.Message);
                        break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (status == "success" && attemptedAny)
                {
                    _store.MarkDeviceSuccess(device.DeviceId, DateTimeText.Now());
                }
            }
            catch (Exception ex)
            {
                allSucceeded = false;
                status = "error";
                errorMessage = ex.Message;
                deviceFailed = Math.Max(1, deviceFailed);
                _store.MarkDeviceError(device.DeviceId, ex.Message);
                _store.InsertAppError("PUSH_ERROR", device.DeviceId, ex.Message, ex.ToString());
                progress?.Invoke($"{device.DeviceId}: loi day du lieu {ex.Message}");
            }
            finally
            {
                _store.InsertSyncLog(new SyncLog
                {
                    DeviceId = device.DeviceId,
                    StartedAt = startedAt,
                    FinishedAt = DateTimeText.Now(),
                    Status = status,
                    TotalRead = 0,
                    TotalSent = deviceSent,
                    TotalInserted = deviceInserted,
                    TotalDuplicated = deviceDuplicated,
                    TotalFailed = deviceFailed,
                    ErrorMessage = errorMessage
                });

                totalSent += deviceSent;
                totalInserted += deviceInserted;
                totalDuplicated += deviceDuplicated;
                totalFailed += deviceFailed;
            }
        }

        var remainingPending = _store.GetPendingLogCount();

        return new SyncRunResult
        {
            DeviceCount = devices.Count,
            TotalRead = 0,
            TotalSent = totalSent,
            TotalInserted = totalInserted,
            TotalDuplicated = totalDuplicated,
            TotalFailed = totalFailed,
            PendingCreated = remainingPending,
            Success = allSucceeded,
            Message = allSucceeded ? "Day du lieu hoan tat." : "Day du lieu xong nhung co loi can kiem tra."
        };
    }

    private async Task<PendingSendResult> SendPendingAsync(
        Device device,
        AttendanceApiClient apiClient,
        CancellationToken cancellationToken,
        Action<string>? progress)
    {
        var pending = _store.GetPendingLogsByDevice(device.DeviceId, BatchSize);
        if (pending.Count == 0)
        {
            return new PendingSendResult();
        }

        progress?.Invoke($"{device.DeviceId}: day {pending.Count} log cho len server.");
        var punches = pending.Select(item => item.ToPunch()).ToArray();
        var result = await apiClient.SendAsync(device, punches, cancellationToken);
        ApplyApiErrors(device, punches, result);

        if (!result.Success)
        {
            _store.MarkPendingLogsFailed(pending.Select(item => item.Id), result.Message);
            return new PendingSendResult
            {
                Attempted = pending.Count,
                Success = false,
                Message = result.Message,
                Failed = pending.Count,
                PendingCreated = pending.Count
            };
        }

        var failedIds = pending
            .Where(item => result.Errors.Any(error => MatchesError(item.ToPunch(), error)))
            .Select(item => item.Id)
            .ToArray();

        var sentIds = pending
            .Select(item => item.Id)
            .Except(failedIds)
            .ToArray();

        _store.DeletePendingLogs(sentIds);
        _store.MarkPendingLogsFailed(failedIds, "API tra loi mapping/du lieu.");

        return new PendingSendResult
        {
            Attempted = pending.Count,
            Success = failedIds.Length == 0,
            Message = failedIds.Length == 0 ? string.Empty : "API tra loi mapping/du lieu.",
            Sent = result.Inserted + result.Duplicated,
            Inserted = result.Inserted,
            Duplicated = result.Duplicated,
            Failed = result.Failed,
            PendingCreated = failedIds.Length
        };
    }

    private void ApplyApiErrors(Device device, IReadOnlyList<AttendancePunch> batch, ApiSyncResult result)
    {
        foreach (var error in result.Errors)
        {
            var message = string.IsNullOrWhiteSpace(error.Message)
                ? error.ErrorType
                : error.Message;

            _store.InsertAppError(
                "MAPPING_" + (string.IsNullOrWhiteSpace(error.ErrorType) ? "ERROR" : error.ErrorType),
                device.DeviceId,
                message,
                $"{error.EmployeeCode} | {error.PunchTime}");
        }

        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
        {
            _store.InsertAppError("API_ERROR", device.DeviceId, result.Message);
        }
    }

    private static DateTime GetReadFromTime(Device device, int readBackDays)
    {
        if (string.IsNullOrWhiteSpace(device.LastSuccessSyncAt))
        {
            return DateTime.Now.AddDays(-Math.Max(1, readBackDays));
        }

        var lastSync = DateTimeText.ParseOrDefault(device.LastSuccessSyncAt, DateTime.Now);
        return lastSync.AddDays(-readBackDays);
    }

    private static bool MatchesError(AttendancePunch log, ApiLogError error)
    {
        if (!string.Equals(log.EmployeeCode, error.EmployeeCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var errorPunchTime = DateTimeText.ParseOrDefault(error.PunchTime, DateTime.MinValue);
        return Math.Abs((log.PunchTime - errorPunchTime).TotalSeconds) < 1;
    }

    private sealed class PendingSendResult
    {
        public int Attempted { get; init; }

        public bool Success { get; init; } = true;

        public string Message { get; init; } = string.Empty;

        public int Sent { get; init; }

        public int Inserted { get; init; }

        public int Duplicated { get; init; }

        public int Failed { get; init; }

        public int PendingCreated { get; init; }
    }
}
