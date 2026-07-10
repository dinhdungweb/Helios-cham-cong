using System.ComponentModel;
using System.Diagnostics;

namespace Helios.Attendance.App;

public sealed class ZkSdkInstallResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}

public static class ZkSdkInstaller
{
    public const string ProgId = "zkemkeeper.CZKEM";
    private const string DllFileName = "zkemkeeper.dll";
    private const int MaxVisitedDirectories = 700;

    private static readonly string[] SearchKeywords =
    [
        "zk",
        "zkteco",
        "ronald",
        "jack",
        "attendance",
        "cham",
        "chấm",
        "1office",
        "10ffice",
        "hoffice"
    ];

    public static bool IsRegistered() => Type.GetTypeFromProgID(ProgId) is not null;

    public static string? FindSdkDll() => FindSdkDll(TimeSpan.FromSeconds(3));

    public static string? FindSdkDll(TimeSpan maxDuration)
    {
        var stopwatch = Stopwatch.StartNew();
        foreach (var path in GetDirectCandidatePaths())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        foreach (var root in GetSearchRoots().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (stopwatch.Elapsed >= maxDuration)
            {
                return null;
            }

            var found = FindSdkDll(root, stopwatch, maxDuration);
            if (!string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }

        return null;
    }

    public static ZkSdkInstallResult RegisterSdk(string dllPath)
    {
        if (IsRegistered())
        {
            return Success("SDK ZK đã được cài sẵn.");
        }

        if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
        {
            return Fail("Không tìm thấy file zkemkeeper.dll.");
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = GetRegsvr32Path(),
                Arguments = $"/s \"{dllPath}\"",
                UseShellExecute = true,
                Verb = ServiceInstaller.IsAdministrator() ? string.Empty : "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                return Fail("Không mở được trình cài SDK ZK.");
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return Fail(GetRegsvr32ErrorMessage(process.ExitCode, dllPath));
            }

            return IsRegistered()
                ? Success("Đã cài SDK ZK thành công. Hãy bấm Test kết nối lại.")
                : Fail("Đã chạy cài SDK nhưng Windows vẫn chưa nhận zkemkeeper.CZKEM. Có thể file DLL thiếu dependency hoặc sai kiến trúc.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Fail("Bạn đã hủy yêu cầu cấp quyền Administrator.");
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static string GetRegsvr32Path()
    {
        var systemX86 = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        var regsvr32 = Path.Combine(systemX86, "regsvr32.exe");
        if (File.Exists(regsvr32))
        {
            return regsvr32;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "regsvr32.exe");
    }

    private static string GetRegsvr32ErrorMessage(int exitCode, string dllPath)
    {
        return exitCode switch
        {
            1 => "Cài SDK ZK không thành công: tham số cài driver không hợp lệ.",
            2 => "Cài SDK ZK không thành công: Windows không khởi tạo được COM/OLE.",
            3 => "Cài SDK ZK không thành công: Windows không load được zkemkeeper.dll. Thường là do thiếu các file DLL đi kèm trong bộ SDK, chọn nhầm DLL 32/64-bit, hoặc file driver bị chặn. Hãy copy cả thư mục SDK/driver chứ không chỉ mỗi zkemkeeper.dll, rồi chọn lại file trong thư mục đó.",
            4 => "Cài SDK ZK không thành công: file DLL không có hàm đăng ký COM DllRegisterServer. Có thể chọn nhầm file DLL.",
            5 => "Cài SDK ZK không thành công: DllRegisterServer trả lỗi. Hãy chạy app bằng quyền Administrator hoặc dùng bộ SDK khác đúng phiên bản.",
            _ => $"Cài SDK ZK không thành công. Mã lỗi: {exitCode}. File: {dllPath}"
        };
    }

    private static IEnumerable<string> GetDirectCandidatePaths()
    {
        var baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, DllFileName);
        yield return Path.Combine(baseDirectory, "drivers", DllFileName);
        yield return Path.Combine(baseDirectory, "sdk", DllFileName);

        var currentDirectory = Environment.CurrentDirectory;
        yield return Path.Combine(currentDirectory, DllFileName);
        yield return Path.Combine(currentDirectory, "drivers", DllFileName);
        yield return Path.Combine(currentDirectory, "sdk", DllFileName);
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        yield return AppContext.BaseDirectory;

        foreach (var specialFolder in new[]
        {
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.CommonProgramFilesX86,
            Environment.SpecialFolder.CommonProgramFiles,
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.ApplicationData
        })
        {
            var path = Environment.GetFolderPath(specialFolder);
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                yield return path;
            }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var child in new[] { "Downloads", "Desktop" })
        {
            var path = Path.Combine(userProfile, child);
            if (Directory.Exists(path))
            {
                yield return path;
            }
        }
    }

    private static string? FindSdkDll(string root, Stopwatch stopwatch, TimeSpan maxDuration)
    {
        if (!Directory.Exists(root))
        {
            return null;
        }

        var queue = new Queue<(string Directory, int Depth)>();
        queue.Enqueue((root, 0));
        var visited = 0;

        while (queue.Count > 0 &&
            visited < MaxVisitedDirectories &&
            stopwatch.Elapsed < maxDuration)
        {
            var (directory, depth) = queue.Dequeue();
            visited++;

            var found = TryFindInDirectory(directory);
            if (!string.IsNullOrWhiteSpace(found))
            {
                return found;
            }

            if (depth >= 5)
            {
                continue;
            }

            foreach (var child in EnumerateDirectoriesSafe(directory))
            {
                var nextDepth = depth + 1;
                if (depth > 0 && nextDepth >= 3 && !LooksRelevant(child))
                {
                    continue;
                }

                queue.Enqueue((child, nextDepth));
            }
        }

        return null;
    }

    private static string? TryFindInDirectory(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, DllFileName, SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory);
        }
        catch
        {
            return [];
        }
    }

    private static bool LooksRelevant(string path) =>
        SearchKeywords.Any(keyword => path.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static ZkSdkInstallResult Success(string message) => new()
    {
        Success = true,
        Message = message
    };

    private static ZkSdkInstallResult Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
