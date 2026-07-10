using System.Globalization;
using Helios.Attendance.Core.Models;
using Microsoft.Data.Sqlite;

namespace Helios.Attendance.Core.Data;

public sealed class AttendanceSyncStore
{
    private readonly string _databasePath;

    public AttendanceSyncStore(string? databasePath = null)
    {
        _databasePath = databasePath ?? AppPaths.DatabasePath;
    }

    public void Initialize()
    {
        AppPaths.EnsureDirectories();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS devices (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT NOT NULL UNIQUE,
                device_name TEXT,
                store_code TEXT,
                device_type TEXT DEFAULT 'zk',
                ip_address TEXT NOT NULL,
                port INTEGER DEFAULT 4370,
                password INTEGER DEFAULT 0,
                is_active INTEGER DEFAULT 1,
                last_success_sync_at TEXT,
                last_error TEXT,
                created_at TEXT,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS sync_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT,
                started_at TEXT,
                finished_at TEXT,
                status TEXT,
                total_read INTEGER DEFAULT 0,
                total_sent INTEGER DEFAULT 0,
                total_inserted INTEGER DEFAULT 0,
                total_duplicated INTEGER DEFAULT 0,
                total_failed INTEGER DEFAULT 0,
                error_message TEXT
            );

            CREATE TABLE IF NOT EXISTS pending_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT NOT NULL,
                store_code TEXT,
                employee_code TEXT NOT NULL,
                punch_time TEXT NOT NULL,
                verify_type TEXT,
                raw_payload TEXT,
                retry_count INTEGER DEFAULT 0,
                last_error TEXT,
                created_at TEXT,
                updated_at TEXT,
                UNIQUE(device_id, employee_code, punch_time)
            );

            CREATE TABLE IF NOT EXISTS app_errors (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                error_type TEXT,
                device_id TEXT,
                message TEXT,
                detail TEXT,
                created_at TEXT
            );
            """;
        command.ExecuteNonQuery();

        EnsureDeviceColumns(connection);

        SeedSetting(connection, "api_url", string.Empty);
        SeedSetting(connection, "api_token", string.Empty);
        SeedSetting(connection, "api_timeout_seconds", "30");
        SeedSetting(connection, "sync_interval_minutes", "5");
        SeedSetting(connection, "poll_interval_minutes", "5");
        SeedSetting(connection, "push_interval_minutes", "1");
        SeedSetting(connection, "push_batch_size", "200");
        SeedSetting(connection, "read_back_days", "1");
        SeedSetting(connection, "auto_push_enabled", "false");
    }

    private static void EnsureDeviceColumns(SqliteConnection connection)
    {
        if (ColumnExists(connection, "devices", "device_type"))
        {
            return;
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "ALTER TABLE devices ADD COLUMN device_type TEXT DEFAULT 'zk'";
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE devices
                SET device_type = 'zk'
                WHERE device_type IS NULL OR TRIM(device_type) = '';
                """;
            command.ExecuteNonQuery();
        }
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public ApiSettings GetApiSettings()
    {
        return new ApiSettings
        {
            ApiUrl = GetSetting("api_url"),
            ApiToken = GetSetting("api_token"),
            TimeoutSeconds = GetSettingInt("api_timeout_seconds", 30)
        };
    }

    public void SaveApiSettings(ApiSettings settings)
    {
        SaveSetting("api_url", settings.ApiUrl.Trim());
        SaveSetting("api_token", settings.ApiToken.Trim());
        SaveSetting("api_timeout_seconds", Math.Max(1, settings.TimeoutSeconds).ToString(CultureInfo.InvariantCulture));
    }

    public int GetSyncIntervalMinutes() => GetPollIntervalMinutes();

    public int GetPollIntervalMinutes() => Math.Clamp(GetSettingInt("poll_interval_minutes", GetSettingInt("sync_interval_minutes", 5)), 1, 1440);

    public int GetPushIntervalMinutes() => Math.Clamp(GetSettingInt("push_interval_minutes", 1), 1, 1440);

    public int GetPushBatchSize() => Math.Clamp(GetSettingInt("push_batch_size", 200), 1, 5000);

    public int GetReadBackDays() => Math.Clamp(GetSettingInt("read_back_days", 1), 0, 365);

    public bool GetAutoPushEnabled() => GetSettingBool("auto_push_enabled", fallback: false);

    public void SaveSyncSettings(int intervalMinutes, int readBackDays)
    {
        SaveSyncSettings(intervalMinutes, GetPushIntervalMinutes(), readBackDays, GetPushBatchSize(), GetAutoPushEnabled());
    }

    public void SaveSyncSettings(int intervalMinutes, int readBackDays, bool autoPushEnabled)
    {
        SaveSyncSettings(intervalMinutes, GetPushIntervalMinutes(), readBackDays, GetPushBatchSize(), autoPushEnabled);
    }

    public void SaveSyncSettings(
        int pollIntervalMinutes,
        int pushIntervalMinutes,
        int readBackDays,
        int pushBatchSize,
        bool autoPushEnabled)
    {
        var pollInterval = Math.Clamp(pollIntervalMinutes, 1, 1440);
        SaveSetting("sync_interval_minutes", pollInterval.ToString(CultureInfo.InvariantCulture));
        SaveSetting("poll_interval_minutes", pollInterval.ToString(CultureInfo.InvariantCulture));
        SaveSetting("push_interval_minutes", Math.Clamp(pushIntervalMinutes, 1, 1440).ToString(CultureInfo.InvariantCulture));
        SaveSetting("push_batch_size", Math.Clamp(pushBatchSize, 1, 5000).ToString(CultureInfo.InvariantCulture));
        SaveSetting("read_back_days", Math.Clamp(readBackDays, 0, 365).ToString(CultureInfo.InvariantCulture));
        SaveSetting("auto_push_enabled", autoPushEnabled ? "true" : "false");
    }

    public IReadOnlyList<Device> GetDevices(bool activeOnly = false)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = activeOnly
            ? "SELECT * FROM devices WHERE is_active = 1 ORDER BY device_id"
            : "SELECT * FROM devices ORDER BY device_id";

        using var reader = command.ExecuteReader();
        var items = new List<Device>();
        while (reader.Read())
        {
            items.Add(ReadDevice(reader));
        }

        return items;
    }

    public Device? GetDeviceByDeviceId(string deviceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM devices WHERE device_id = $device_id LIMIT 1";
        command.Parameters.AddWithValue("$device_id", deviceId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadDevice(reader) : null;
    }

    public void SaveDevice(Device device)
    {
        var now = DateTimeText.Now();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO devices (
                device_id,
                device_name,
                store_code,
                device_type,
                ip_address,
                port,
                password,
                is_active,
                created_at,
                updated_at
            )
            VALUES (
                $device_id,
                $device_name,
                $store_code,
                $device_type,
                $ip_address,
                $port,
                $password,
                $is_active,
                $now,
                $now
            )
            ON CONFLICT(device_id) DO UPDATE SET
                device_name = excluded.device_name,
                store_code = excluded.store_code,
                device_type = excluded.device_type,
                ip_address = excluded.ip_address,
                port = excluded.port,
                password = excluded.password,
                is_active = excluded.is_active,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$device_id", device.DeviceId.Trim());
        command.Parameters.AddWithValue("$device_name", device.DeviceName.Trim());
        command.Parameters.AddWithValue("$store_code", device.StoreCode.Trim());
        command.Parameters.AddWithValue("$device_type", AttendanceDeviceTypes.Normalize(device.DeviceType));
        command.Parameters.AddWithValue("$ip_address", device.IpAddress.Trim());
        command.Parameters.AddWithValue("$port", device.Port);
        command.Parameters.AddWithValue("$password", device.Password);
        command.Parameters.AddWithValue("$is_active", device.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$now", now);
        command.ExecuteNonQuery();
    }

    public void DeleteDevice(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM devices WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void MarkDeviceSuccess(string deviceId, string syncedAt)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE devices
            SET last_success_sync_at = $synced_at,
                last_error = '',
                updated_at = $synced_at
            WHERE device_id = $device_id;
            """;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$synced_at", syncedAt);
        command.ExecuteNonQuery();
    }

    public void MarkDeviceError(string deviceId, string message)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE devices
            SET last_error = $message,
                updated_at = $now
            WHERE device_id = $device_id;
            """;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$now", DateTimeText.Now());
        command.ExecuteNonQuery();
    }

    public DashboardStats GetDashboardStats()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new DashboardStats
        {
            TotalDevices = ScalarInt("SELECT COUNT(*) FROM devices"),
            ActiveDevices = ScalarInt("SELECT COUNT(*) FROM devices WHERE is_active = 1"),
            DeviceErrors = ScalarInt("SELECT COUNT(*) FROM devices WHERE is_active = 1 AND COALESCE(last_error, '') <> ''"),
            PendingLogs = ScalarInt("SELECT COUNT(*) FROM pending_logs"),
            MappingErrorsToday = ScalarInt("SELECT COUNT(*) FROM app_errors WHERE error_type LIKE 'MAPPING%' AND substr(created_at, 1, 10) = $today", ("$today", today)),
            SentToday = ScalarInt("SELECT COALESCE(SUM(total_sent), 0) FROM sync_logs WHERE substr(started_at, 1, 10) = $today", ("$today", today)),
            LastSyncAt = ScalarString("SELECT COALESCE(MAX(finished_at), '') FROM sync_logs")
        };
    }

    public int GetPendingLogCount() => ScalarInt("SELECT COUNT(*) FROM pending_logs");

    public IReadOnlyDictionary<string, int> GetPendingLogCountsByDevice()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT device_id, COUNT(*) AS pending_count
            FROM pending_logs
            GROUP BY device_id;
            """;

        using var reader = command.ExecuteReader();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            counts[GetString(reader, "device_id")] = GetInt(reader, "pending_count");
        }

        return counts;
    }

    public IReadOnlyList<SyncLog> GetRecentSyncLogs(int limit = 100)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM sync_logs
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var items = new List<SyncLog>();
        while (reader.Read())
        {
            items.Add(new SyncLog
            {
                Id = GetInt(reader, "id"),
                DeviceId = GetString(reader, "device_id"),
                StartedAt = GetString(reader, "started_at"),
                FinishedAt = GetString(reader, "finished_at"),
                Status = GetString(reader, "status"),
                TotalRead = GetInt(reader, "total_read"),
                TotalSent = GetInt(reader, "total_sent"),
                TotalInserted = GetInt(reader, "total_inserted"),
                TotalDuplicated = GetInt(reader, "total_duplicated"),
                TotalFailed = GetInt(reader, "total_failed"),
                ErrorMessage = GetString(reader, "error_message")
            });
        }

        return items;
    }

    public void InsertSyncLog(SyncLog log)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sync_logs (
                device_id,
                started_at,
                finished_at,
                status,
                total_read,
                total_sent,
                total_inserted,
                total_duplicated,
                total_failed,
                error_message
            )
            VALUES (
                $device_id,
                $started_at,
                $finished_at,
                $status,
                $total_read,
                $total_sent,
                $total_inserted,
                $total_duplicated,
                $total_failed,
                $error_message
            );
            """;
        command.Parameters.AddWithValue("$device_id", log.DeviceId);
        command.Parameters.AddWithValue("$started_at", log.StartedAt);
        command.Parameters.AddWithValue("$finished_at", log.FinishedAt);
        command.Parameters.AddWithValue("$status", log.Status);
        command.Parameters.AddWithValue("$total_read", log.TotalRead);
        command.Parameters.AddWithValue("$total_sent", log.TotalSent);
        command.Parameters.AddWithValue("$total_inserted", log.TotalInserted);
        command.Parameters.AddWithValue("$total_duplicated", log.TotalDuplicated);
        command.Parameters.AddWithValue("$total_failed", log.TotalFailed);
        command.Parameters.AddWithValue("$error_message", log.ErrorMessage);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<PendingLog> GetPendingLogs(int limit = 500)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM pending_logs
            ORDER BY id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        return ReadPendingLogs(command);
    }

    public IReadOnlyList<PendingLog> GetPendingLogsByDevice(string deviceId, int limit = 500)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM pending_logs
            WHERE device_id = $device_id
            ORDER BY id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$limit", limit);
        return ReadPendingLogs(command);
    }

    public void UpsertPendingLogs(IEnumerable<AttendancePunch> logs, string error)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var log in logs)
        {
            UpsertPendingLog(connection, transaction, log, error);
        }

        transaction.Commit();
    }

    public void MarkPendingLogsFailed(IEnumerable<int> ids, string error)
    {
        var idList = ids.ToArray();
        if (idList.Length == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var id in idList)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE pending_logs
                SET retry_count = retry_count + 1,
                    last_error = $error,
                    updated_at = $now
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$error", error);
            command.Parameters.AddWithValue("$now", DateTimeText.Now());
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void DeletePendingLogs(IEnumerable<int> ids)
    {
        var idList = ids.ToArray();
        if (idList.Length == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var id in idList)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM pending_logs WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void ClearAllPendingLogs()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM pending_logs";
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AppError> GetRecentErrors(int limit = 200)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM app_errors
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var items = new List<AppError>();
        while (reader.Read())
        {
            items.Add(new AppError
            {
                Id = GetInt(reader, "id"),
                ErrorType = GetString(reader, "error_type"),
                DeviceId = GetString(reader, "device_id"),
                Message = GetString(reader, "message"),
                Detail = GetString(reader, "detail"),
                CreatedAt = GetString(reader, "created_at")
            });
        }

        return items;
    }

    public void InsertAppError(string errorType, string deviceId, string message, string detail = "")
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_errors (
                error_type,
                device_id,
                message,
                detail,
                created_at
            )
            VALUES (
                $error_type,
                $device_id,
                $message,
                $detail,
                $created_at
            );
            """;
        command.Parameters.AddWithValue("$error_type", errorType);
        command.Parameters.AddWithValue("$device_id", deviceId);
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$detail", detail);
        command.Parameters.AddWithValue("$created_at", DateTimeText.Now());
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        AppPaths.EnsureDirectories();
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();

        return connection;
    }

    private static void SeedSetting(SqliteConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (key, value, updated_at)
            VALUES ($key, $value, $updated_at)
            ON CONFLICT(key) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updated_at", DateTimeText.Now());
        command.ExecuteNonQuery();
    }

    private string GetSetting(string key)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(value, '') FROM settings WHERE key = $key LIMIT 1";
        command.Parameters.AddWithValue("$key", key);
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private int GetSettingInt(string key, int fallback)
    {
        var value = GetSetting(key);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private bool GetSettingBool(string key, bool fallback)
    {
        var value = GetSetting(key);
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number != 0
            : fallback;
    }

    private void SaveSetting(string key, string value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (key, value, updated_at)
            VALUES ($key, $value, $updated_at)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updated_at", DateTimeText.Now());
        command.ExecuteNonQuery();
    }

    private int ScalarInt(string sql, params (string Name, object Value)[] parameters)
    {
        var value = Scalar(sql, parameters);
        return value is null || value == DBNull.Value
            ? 0
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private string ScalarString(string sql, params (string Name, object Value)[] parameters)
    {
        return Convert.ToString(Scalar(sql, parameters), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private object? Scalar(string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        return command.ExecuteScalar();
    }

    private static Device ReadDevice(SqliteDataReader reader) => new()
    {
        Id = GetInt(reader, "id"),
        DeviceId = GetString(reader, "device_id"),
        DeviceName = GetString(reader, "device_name"),
        StoreCode = GetString(reader, "store_code"),
        DeviceType = AttendanceDeviceTypes.Normalize(GetString(reader, "device_type")),
        IpAddress = GetString(reader, "ip_address"),
        Port = GetInt(reader, "port"),
        Password = GetInt(reader, "password"),
        IsActive = GetInt(reader, "is_active") == 1,
        LastSuccessSyncAt = GetString(reader, "last_success_sync_at"),
        LastError = GetString(reader, "last_error"),
        CreatedAt = GetString(reader, "created_at"),
        UpdatedAt = GetString(reader, "updated_at")
    };

    private static IReadOnlyList<PendingLog> ReadPendingLogs(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var items = new List<PendingLog>();
        while (reader.Read())
        {
            items.Add(new PendingLog
            {
                Id = GetInt(reader, "id"),
                DeviceId = GetString(reader, "device_id"),
                StoreCode = GetString(reader, "store_code"),
                EmployeeCode = GetString(reader, "employee_code"),
                PunchTime = GetString(reader, "punch_time"),
                VerifyType = GetString(reader, "verify_type"),
                RetryCount = GetInt(reader, "retry_count"),
                LastError = GetString(reader, "last_error"),
                CreatedAt = GetString(reader, "created_at"),
                UpdatedAt = GetString(reader, "updated_at")
            });
        }

        return items;
    }

    private static void UpsertPendingLog(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AttendancePunch log,
        string error)
    {
        var now = DateTimeText.Now();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO pending_logs (
                device_id,
                store_code,
                employee_code,
                punch_time,
                verify_type,
                raw_payload,
                retry_count,
                last_error,
                created_at,
                updated_at
            )
            VALUES (
                $device_id,
                $store_code,
                $employee_code,
                $punch_time,
                $verify_type,
                $raw_payload,
                0,
                $last_error,
                $now,
                $now
            )
            ON CONFLICT(device_id, employee_code, punch_time) DO UPDATE SET
                store_code = excluded.store_code,
                verify_type = excluded.verify_type,
                raw_payload = excluded.raw_payload,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$device_id", log.DeviceId);
        command.Parameters.AddWithValue("$store_code", log.StoreCode);
        command.Parameters.AddWithValue("$employee_code", log.EmployeeCode);
        command.Parameters.AddWithValue("$punch_time", DateTimeText.Format(log.PunchTime));
        command.Parameters.AddWithValue("$verify_type", log.VerifyType);
        command.Parameters.AddWithValue("$raw_payload", log.RawPayload);
        command.Parameters.AddWithValue("$last_error", error);
        command.Parameters.AddWithValue("$now", now);
        command.ExecuteNonQuery();
    }

    private static string GetString(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static int GetInt(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }
}
