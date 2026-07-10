using Helios.Attendance.Core.Data;
using Helios.Attendance.Core.Devices;
using Helios.Attendance.Core.Sync;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Helios.Attendance.App;

public sealed class ServiceWorker : BackgroundService
{
    private readonly ILogger<ServiceWorker> _logger;
    private readonly AttendanceSyncStore _store;
    private readonly IAttendanceDeviceClient _deviceClient;

    public ServiceWorker(
        ILogger<ServiceWorker> logger,
        AttendanceSyncStore store,
        IAttendanceDeviceClient deviceClient)
    {
        _logger = logger;
        _store = store;
        _deviceClient = deviceClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _store.Initialize();
        var nextPollAt = DateTimeOffset.MinValue;
        var nextPushAt = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.Now;
                var engine = new SyncEngine(_store, _deviceClient);

                if (now >= nextPollAt)
                {
                    var pollResult = await engine.PollDevicesAsync(
                        stoppingToken,
                        message => _logger.LogInformation("{Message}", message));

                    _logger.LogInformation(
                        "Poll finished. Success={Success}, Read={Read}, Failed={Failed}, Pending={Pending}",
                        pollResult.Success,
                        pollResult.TotalRead,
                        pollResult.TotalFailed,
                        pollResult.PendingCreated);

                    nextPollAt = DateTimeOffset.Now.AddMinutes(_store.GetPollIntervalMinutes());
                }

                var autoPushEnabled = _store.GetAutoPushEnabled();
                if (autoPushEnabled && now >= nextPushAt)
                {
                    var pushResult = await engine.PushPendingAsync(
                        stoppingToken,
                        message => _logger.LogInformation("{Message}", message));

                    _logger.LogInformation(
                        "Auto push finished. Success={Success}, Sent={Sent}, Failed={Failed}, Pending={Pending}",
                        pushResult.Success,
                        pushResult.TotalSent,
                        pushResult.TotalFailed,
                        pushResult.PendingCreated);

                    nextPushAt = DateTimeOffset.Now.AddMinutes(_store.GetPushIntervalMinutes());
                }
                else if (!autoPushEnabled)
                {
                    nextPushAt = DateTimeOffset.MinValue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled service sync error");
                _store.InsertAppError("SERVICE_ERROR", string.Empty, ex.Message, ex.ToString());
            }

            var nextDueAt = _store.GetAutoPushEnabled()
                ? Min(nextPollAt, nextPushAt)
                : nextPollAt;
            var delay = nextDueAt - DateTimeOffset.Now;
            if (delay < TimeSpan.FromSeconds(5))
            {
                delay = TimeSpan.FromSeconds(5);
            }

            if (delay > TimeSpan.FromSeconds(30))
            {
                delay = TimeSpan.FromSeconds(30);
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left <= right ? left : right;
}
