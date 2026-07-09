namespace Helios.Attendance.Service;

using Helios.Attendance.Core.Data;
using Helios.Attendance.Core.Devices;
using Helios.Attendance.Core.Sync;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AttendanceSyncStore _store;
    private readonly IAttendanceDeviceClient _deviceClient;

    public Worker(
        ILogger<Worker> logger,
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var engine = new SyncEngine(_store, _deviceClient);
                var result = await engine.RunOnceAsync(
                    stoppingToken,
                    message => _logger.LogInformation("{Message}", message));

                _logger.LogInformation(
                    "Sync finished. Success={Success}, Read={Read}, Sent={Sent}, Failed={Failed}, Pending={Pending}",
                    result.Success,
                    result.TotalRead,
                    result.TotalSent,
                    result.TotalFailed,
                    result.PendingCreated);
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

            var interval = TimeSpan.FromMinutes(_store.GetSyncIntervalMinutes());
            await Task.Delay(interval, stoppingToken);
        }
    }
}
