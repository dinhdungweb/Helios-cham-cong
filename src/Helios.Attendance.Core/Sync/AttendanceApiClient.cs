using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Helios.Attendance.Core.Models;

namespace Helios.Attendance.Core.Sync;

public sealed class AttendanceApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ApiSettings _settings;

    public AttendanceApiClient(ApiSettings settings)
    {
        _settings = settings;
    }

    public Task<ApiSyncResult> TestAsync(CancellationToken cancellationToken)
    {
        var testDevice = new Device
        {
            DeviceId = "HELIOS_TEST",
            StoreCode = "TEST"
        };

        return SendPayloadAsync(testDevice, [], cancellationToken, sendEmptyBatch: true);
    }

    public Task<ApiSyncResult> SendAsync(
        Device device,
        IReadOnlyList<AttendancePunch> logs,
        CancellationToken cancellationToken)
    {
        return logs.Count == 0
            ? Task.FromResult(ApiSyncResult.EmptySuccess())
            : SendPayloadAsync(device, logs, cancellationToken, sendEmptyBatch: false);
    }

    private async Task<ApiSyncResult> SendPayloadAsync(
        Device device,
        IReadOnlyList<AttendancePunch> logs,
        CancellationToken cancellationToken,
        bool sendEmptyBatch)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiUrl))
        {
            return ApiSyncResult.Fail("Chưa cấu hình API URL.");
        }

        if (!Uri.TryCreate(_settings.ApiUrl, UriKind.Absolute, out var uri))
        {
            return ApiSyncResult.Fail("API URL không hợp lệ.");
        }

        if (!sendEmptyBatch && logs.Count == 0)
        {
            return ApiSyncResult.EmptySuccess();
        }

        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Math.Clamp(_settings.TimeoutSeconds, 1, 300))
            };

            if (!string.IsNullOrWhiteSpace(_settings.ApiToken))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
            }

            var payload = new
            {
                device_id = device.DeviceId,
                store_code = device.StoreCode,
                logs = logs.Select(log => new
                {
                    employee_code = log.EmployeeCode,
                    punch_time = DateTimeText.Format(log.PunchTime),
                    verify_type = log.VerifyType
                }).ToArray()
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(uri, content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiSyncResult.Fail($"HTTP {(int)response.StatusCode}: {responseText}");
            }

            var result = JsonSerializer.Deserialize<ApiSyncResult>(responseText, JsonOptions);
            return result ?? ApiSyncResult.Fail("API trả response rỗng hoặc không đúng JSON.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiSyncResult.Fail("API timeout.");
        }
        catch (Exception ex)
        {
            return ApiSyncResult.Fail(ex.Message);
        }
    }
}
