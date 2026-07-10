using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

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
    private const string SdkZipFileName = "sdk.zip";
    private const int MaxVisitedDirectories = 700;

    private static readonly string[] SearchKeywords =
    [
        "dtc",
        "zk",
        "zksoftware",
        "zkaccess",
        "zkteco",
        "ronald",
        "jack",
        "attendance",
        "cham",
        "chấm"
    ];

    public static bool IsRegistered() => Type.GetTypeFromProgID(ProgId) is not null;

    public static string? FindSdkSource() => FindSdkSource(TimeSpan.FromSeconds(3));

    public static string? FindSdkSource(TimeSpan maxDuration)
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

    public static string? FindSdkDll() => FindSdkSource();

    public static string? FindSdkDll(TimeSpan maxDuration) => FindSdkSource(maxDuration);

    public static ZkSdkInstallResult RegisterSdk(string sdkPath)
    {
        if (IsRegistered())
        {
            return Success("SDK ZK đã được cài sẵn.");
        }

        if (string.IsNullOrWhiteSpace(sdkPath))
        {
            return Fail("Không tìm thấy file SDK ZK.");
        }

        try
        {
            if (Directory.Exists(sdkPath))
            {
                return RegisterSdkFromDirectory(sdkPath, sdkPath);
            }

            if (!File.Exists(sdkPath))
            {
                return Fail("Không tìm thấy file SDK ZK.");
            }

            if (string.Equals(Path.GetFileName(sdkPath), SdkZipFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(sdkPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                return RegisterSdkFromZip(sdkPath);
            }

            var siblingZip = FindSiblingSdkZip(sdkPath);
            if (!string.IsNullOrWhiteSpace(siblingZip))
            {
                return RegisterSdkFromZip(siblingZip);
            }

            var sdkDirectory = Path.GetDirectoryName(sdkPath);
            return string.IsNullOrWhiteSpace(sdkDirectory)
                ? Fail("Không xác định được thư mục chứa SDK ZK.")
                : RegisterSdkFromDirectory(sdkDirectory, sdkPath);
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

    private static ZkSdkInstallResult RegisterSingleDllSdk(string dllPath)
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
            var workingDirectory = Path.GetDirectoryName(dllPath);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = GetRegsvr32Path(),
                Arguments = $"/s \"{dllPath}\"",
                UseShellExecute = true,
                Verb = ServiceInstaller.IsAdministrator() ? string.Empty : "runas",
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? AppContext.BaseDirectory
                    : workingDirectory,
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

    private static ZkSdkInstallResult RegisterSdkFromZip(string zipPath)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"hoffice-zk-sdk-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var archive = ZipFile.OpenRead(zipPath);
            var prefix = FindZkSdk32BitPrefix(archive);
            if (prefix is null)
            {
                return Fail("File SDK zip không có bộ ZK 32-bit chứa zkemkeeper.dll.");
            }

            foreach (var entry in archive.Entries.Where(entry =>
                entry.FullName.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var fileName = Path.GetFileName(entry.FullName);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                entry.ExtractToFile(Path.Combine(tempDirectory, fileName), overwrite: true);
            }

            if (!File.Exists(Path.Combine(tempDirectory, DllFileName)))
            {
                return Fail("Giải nén SDK zip xong nhưng không thấy zkemkeeper.dll 32-bit.");
            }

            return RegisterSdkFromDirectory(tempDirectory, zipPath);
        }
        catch (InvalidDataException)
        {
            return Fail("File SDK zip không hợp lệ hoặc bị hỏng.");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static ZkSdkInstallResult RegisterSdkFromDirectory(string sourceDirectory, string displaySource)
    {
        if (!File.Exists(Path.Combine(sourceDirectory, DllFileName)))
        {
            return Fail("Thư mục SDK không có zkemkeeper.dll.");
        }

        var targetDirectory = GetSdkInstallDirectory();
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            return Fail("Không xác định được thư mục Windows để cài SDK ZK.");
        }

        var result = RunSdkInstallScript(sourceDirectory, targetDirectory);
        if (!result.Success)
        {
            return result;
        }

        return IsRegistered()
            ? Success("Đã cài SDK ZK thành công. Hãy bấm Test kết nối lại.")
            : Fail($"Đã copy và đăng ký SDK từ {displaySource} nhưng Windows vẫn chưa nhận {ProgId}.");
    }

    private static ZkSdkInstallResult RunSdkInstallScript(string sourceDirectory, string targetDirectory)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"hoffice-zk-sdk-install-{Guid.NewGuid():N}.cmd");
        var logPath = Path.Combine(Path.GetTempPath(), $"hoffice-zk-sdk-install-{Guid.NewGuid():N}.log");
        var targetDll = Path.Combine(targetDirectory, DllFileName);
        var script = string.Join(Environment.NewLine, new[]
        {
            "@echo off",
            "setlocal",
            $"copy /Y \"{Path.Combine(sourceDirectory, "*.dll")}\" \"{targetDirectory}\\\" > \"{logPath}\" 2>&1",
            "if errorlevel 1 exit /b 10",
            $"\"{GetRegsvr32Path()}\" /s \"{targetDll}\"",
            "exit /b %errorlevel%"
        });

        try
        {
            File.WriteAllText(scriptPath, script, Encoding.ASCII);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = true,
                Verb = ServiceInstaller.IsAdministrator() ? string.Empty : "runas",
                WorkingDirectory = sourceDirectory,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                return Fail("Không mở được trình cài SDK ZK.");
            }

            process.WaitForExit();
            if (process.ExitCode == 10)
            {
                return Fail("Không copy được bộ SDK ZK vào thư mục Windows. Hãy chạy app bằng quyền Administrator.");
            }

            if (process.ExitCode != 0)
            {
                return Fail(GetRegsvr32ErrorMessage(process.ExitCode, targetDll));
            }

            return Success("Đã copy bộ DLL phụ và đăng ký zkemkeeper.dll.");
        }
        finally
        {
            TryDeleteFile(scriptPath);
            TryDeleteFile(logPath);
        }
    }

    private static string GetSdkInstallDirectory()
    {
        return Environment.Is64BitOperatingSystem
            ? Environment.GetFolderPath(Environment.SpecialFolder.SystemX86)
            : Environment.GetFolderPath(Environment.SpecialFolder.System);
    }

    private static string? FindZkSdk32BitPrefix(ZipArchive archive)
    {
        var candidates = archive.Entries
            .Where(entry => string.Equals(Path.GetFileName(entry.FullName), DllFileName, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .ToList();

        var selected = candidates.FirstOrDefault(LooksLike32BitSdkPath) ??
            candidates.FirstOrDefault(entry => !entry.StartsWith("64", StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return null;
        }

        var index = selected.LastIndexOf('/');
        return index < 0 ? string.Empty : selected[..(index + 1)];
    }

    private static bool LooksLike32BitSdkPath(string path) =>
        path.Contains("32", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("x86", StringComparison.OrdinalIgnoreCase);

    private static string? FindSiblingSdkZip(string sdkPath)
    {
        var directory = Path.GetDirectoryName(sdkPath);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var zip = Path.Combine(directory, SdkZipFileName);
            if (File.Exists(zip))
            {
                return zip;
            }

            var parent = Directory.GetParent(directory);
            if (parent is null)
            {
                return null;
            }

            directory = parent.FullName;
        }

        return null;
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
            3 => "Cài SDK ZK không thành công: Windows không load được zkemkeeper.dll. Hãy chọn sdk.zip hoặc thư mục SDK có đủ các DLL phụ như plcommpro.dll, plcomms.dll, zkemsdk.dll. Nếu vẫn lỗi, bộ driver có thể thiếu file phụ thuộc hoặc không phải zkemkeeper COM.",
            4 => "Cài SDK ZK không thành công: file DLL không có hàm đăng ký COM DllRegisterServer. Có thể chọn nhầm file DLL.",
            5 => "Cài SDK ZK không thành công: DllRegisterServer trả lỗi. Hãy chạy app bằng quyền Administrator hoặc dùng bộ SDK khác đúng phiên bản.",
            _ => $"Cài SDK ZK không thành công. Mã lỗi: {exitCode}. File: {dllPath}"
        };
    }

    private static IEnumerable<string> GetDirectCandidatePaths()
    {
        var baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, SdkZipFileName);
        yield return Path.Combine(baseDirectory, DllFileName);
        yield return Path.Combine(baseDirectory, "drivers", SdkZipFileName);
        yield return Path.Combine(baseDirectory, "drivers", DllFileName);
        yield return Path.Combine(baseDirectory, "drivers", "zk", SdkZipFileName);
        yield return Path.Combine(baseDirectory, "drivers", "zk", DllFileName);
        yield return Path.Combine(baseDirectory, "drivers", "zk", "32bit", "32bit", DllFileName);
        yield return Path.Combine(baseDirectory, "drivers", "zkteco", SdkZipFileName);
        yield return Path.Combine(baseDirectory, "drivers", "zkteco", DllFileName);
        yield return Path.Combine(baseDirectory, "drivers", "dtc", SdkZipFileName);
        yield return Path.Combine(baseDirectory, "drivers", "dtc", DllFileName);
        yield return Path.Combine(baseDirectory, "drivers", "ronald-jack", SdkZipFileName);
        yield return Path.Combine(baseDirectory, "drivers", "ronald-jack", DllFileName);
        yield return Path.Combine(baseDirectory, "sdk", SdkZipFileName);
        yield return Path.Combine(baseDirectory, "sdk", DllFileName);
        yield return Path.Combine(baseDirectory, "sdk", "zk", SdkZipFileName);
        yield return Path.Combine(baseDirectory, "sdk", "zk", DllFileName);
        yield return Path.Combine(baseDirectory, "sdk", "dtc", SdkZipFileName);
        yield return Path.Combine(baseDirectory, "sdk", "dtc", DllFileName);

        var currentDirectory = Environment.CurrentDirectory;
        yield return Path.Combine(currentDirectory, SdkZipFileName);
        yield return Path.Combine(currentDirectory, DllFileName);
        yield return Path.Combine(currentDirectory, "drivers", SdkZipFileName);
        yield return Path.Combine(currentDirectory, "drivers", DllFileName);
        yield return Path.Combine(currentDirectory, "drivers", "zk", SdkZipFileName);
        yield return Path.Combine(currentDirectory, "drivers", "zk", DllFileName);
        yield return Path.Combine(currentDirectory, "drivers", "zk", "32bit", "32bit", DllFileName);
        yield return Path.Combine(currentDirectory, "drivers", "zkteco", SdkZipFileName);
        yield return Path.Combine(currentDirectory, "drivers", "zkteco", DllFileName);
        yield return Path.Combine(currentDirectory, "drivers", "dtc", SdkZipFileName);
        yield return Path.Combine(currentDirectory, "drivers", "dtc", DllFileName);
        yield return Path.Combine(currentDirectory, "drivers", "ronald-jack", SdkZipFileName);
        yield return Path.Combine(currentDirectory, "drivers", "ronald-jack", DllFileName);
        yield return Path.Combine(currentDirectory, "sdk", SdkZipFileName);
        yield return Path.Combine(currentDirectory, "sdk", DllFileName);
        yield return Path.Combine(currentDirectory, "sdk", "zk", SdkZipFileName);
        yield return Path.Combine(currentDirectory, "sdk", "zk", DllFileName);
        yield return Path.Combine(currentDirectory, "sdk", "dtc", SdkZipFileName);
        yield return Path.Combine(currentDirectory, "sdk", "dtc", DllFileName);
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
                if (depth == 0 && IsBroadSearchRoot(directory) && !LooksRelevant(child))
                {
                    continue;
                }

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
            return Directory.EnumerateFiles(directory, SdkZipFileName, SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(directory, DllFileName, SearchOption.TopDirectoryOnly))
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

    private static bool IsBroadSearchRoot(string path)
    {
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
            var folder = Environment.GetFolderPath(specialFolder);
            if (!string.IsNullOrWhiteSpace(folder) &&
                string.Equals(
                    Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

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
