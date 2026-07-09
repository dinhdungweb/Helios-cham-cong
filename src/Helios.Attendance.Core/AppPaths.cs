namespace Helios.Attendance.Core;

public static class AppPaths
{
    public const string AppName = "HELIOS Attendance Sync";
    public const string ServiceName = "HeliosAttendanceSyncService";

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppName);

    public static string DatabasePath => Path.Combine(DataDirectory, "attendance_sync.db");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    public static string SampleLogPath => Path.Combine(DataDirectory, "sample_logs.csv");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
