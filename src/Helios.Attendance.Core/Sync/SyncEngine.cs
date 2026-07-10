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
        _store.Initialize();

        var devices = _store.GetDevices(activeOnly: true);
        if (devices.Count == 0)
        {
            progress?.Invoke("Chưa có thiết bị active.");
            return new SyncRunResult
            {
                Success = true,
                Message = "Chưa có thiết bị active."
            };
        }

        var apiClient = new AttendanceApiClient(_store.GetApiSettings());
        var readBackDays = _store.GetReadBackDays();

        var totalRead = 0;
        var totalSent = 0;
        var totalInserted = 0;
        var totalDuplicated = 0;
        var totalFailed = 0;
        var totalPending = 0;
        var allSucceeded = true;

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke($"Đồng bộ {device.DeviceId}...");

            var startedAt = DateTimeText.Now();
            var deviceRead = 0;
            var deviceSent = 0;
            var deviceInserted = 0;
            var deviceDuplicated = 0;
            var deviceFailed = 0;
            var status = "success";
            var errorMessage = string.Empty;

            try
            {
                var pendingResult = await SendPendingAsync(device, apiClient, cancellationToken, progress);
                deviceSent += pendingResult.Sent;
                deviceInserted += pendingResult.Inserted;
                deviceDuplicated += pendingResult.Duplicated;
                deviceFailed += pendingResult.Failed;
                totalPending += pendingResult.PendingCreated;

                var fromTime = GetReadFromTime(device, readBackDays);
                var logs = await _deviceClient.ReadLogsAsync(device, fromTime, cancellationToken);
                deviceRead = logs.Count;
                progress?.Invoke($"{device.DeviceId}: đọc được {deviceRead} log từ {DateTimeText.Format(fromTime)}.");

                foreach (var batch in Batch(logs, BatchSize))
                {
                    var sendResult = await apiClient.SendAsync(device, batch, cancellationToken);
                    ApplyApiErrors(device, batch, sendResult);

                    deviceSent += sendResult.Inserted + sendResult.Duplicated;
                    deviceInserted += sendResult.Inserted;
                    deviceDuplicated += sendResult.Duplicated;
                    deviceFailed += sendResult.Failed;

                    if (!sendResult.Success)
                    {
                        allSucceeded = false;
                        status = "error";
                        errorMessage = sendResult.Message;
                        _store.UpsertPendingLogs(batch, sendResult.Message);
                        totalPending += batch.Count;
                        progress?.Invoke($"{device.DeviceId}: API lỗi, đã đưa {batch.Count} log vào pending.");
                    }
                    else
                    {
                        var failedLogs = FindFailedLogs(batch, sendResult.Errors).ToArray();
                        if (failedLogs.Length > 0)
                        {
                            status = status == "error" ? status : "warning";
                            _store.UpsertPendingLogs(failedLogs, "API trả lỗi mapping/dữ liệu.");
                            totalPending += failedLogs.Length;
                        }
                    }
                }

                if (status != "error")
                {
                    _store.MarkDeviceSuccess(device.DeviceId, DateTimeText.Now());
                }
                else
                {
                    _store.MarkDeviceError(device.DeviceId, errorMessage);
                }
            }
            catch (Exception ex)
            {
                allSucceeded = false;
                status = "error";
                errorMessage = ex.Message;
                _store.MarkDeviceError(device.DeviceId, ex.Message);
                _store.InsertAppError("SYNC_ERROR", device.DeviceId, ex.Message, ex.ToString());
                progress?.Invoke($"{device.DeviceId}: lỗi {ex.Message}");
            }
            finally
            {
                var finishedAt = DateTimeText.Now();
                _store.InsertSyncLog(new SyncLog
                {
                    DeviceId = device.DeviceId,
                    StartedAt = startedAt,
                    FinishedAt = finishedAt,
                    Status = status,
                    TotalRead = deviceRead,
                    TotalSent = deviceSent,
                    TotalInserted = deviceInserted,
                    TotalDuplicated = deviceDuplicated,
                    TotalFailed = deviceFailed,
                    ErrorMessage = errorMessage
                });

                totalRead += deviceRead;
                totalSent += deviceSent;
                totalInserted += deviceInserted;
                totalDuplicated += deviceDuplicated;
                totalFailed += deviceFailed;
            }
        }

        return new SyncRunResult
        {
            DeviceCount = devices.Count,
            TotalRead = totalRead,
            TotalSent = totalSent,
            TotalInserted = totalInserted,
            TotalDuplicated = totalDuplicated,
            TotalFailed = totalFailed,
            PendingCreated = totalPending,
            Success = allSucceeded,
            Message = allSucceeded ? "Đồng bộ hoàn tất." : "Đồng bộ xong nhưng có lỗi cần kiểm tra."
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

        progress?.Invoke($"{device.DeviceId}: gửi lại {pending.Count} pending log.");
        var punches = pending.Select(item => item.ToPunch()).ToArray();
        var result = await apiClient.SendAsync(device, punches, cancellationToken);
        ApplyApiErrors(device, punches, result);

        if (!result.Success)
        {
            _store.MarkPendingLogsFailed(pending.Select(item => item.Id), result.Message);
            return new PendingSendResult
            {
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
        _store.MarkPendingLogsFailed(failedIds, "API trả lỗi mapping/dữ liệu.");

        return new PendingSendResult
        {
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

    private static IEnumerable<AttendancePunch> FindFailedLogs(
        IReadOnlyList<AttendancePunch> logs,
        IReadOnlyList<ApiLogError> errors)
    {
        return logs.Where(log => errors.Any(error => MatchesError(log, error)));
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

    private static IEnumerable<IReadOnlyList<T>> Batch<T>(IReadOnlyList<T> items, int batchSize)
    {
        for (var index = 0; index < items.Count; index += batchSize)
        {
            yield return items.Skip(index).Take(batchSize).ToArray();
        }
    }

    private sealed class PendingSendResult
    {
        public int Sent { get; init; }

        public int Inserted { get; init; }

        public int Duplicated { get; init; }

        public int Failed { get; init; }

        public int PendingCreated { get; init; }
    }
}
