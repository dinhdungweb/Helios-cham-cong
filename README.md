# HELIOS Attendance Sync

App Windows MVP theo `PLAN_Tich_Hop_May_Cham_Cong_Web.md`.

Project dùng một executable duy nhất:

```text
HeliosAttendanceSync.exe
```

- Mở bình thường: chạy giao diện quản lý.
- Windows Service gọi với `--service`: chạy nền tự động theo chu kỳ đã cấu hình trên UI.

## Chạy app giao diện

Nếu máy chưa có .NET SDK, cài local trong thư mục project:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-dotnet.ps1
```

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-app.ps1
```

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

## Publish app hợp nhất

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-app.ps1
```

## Cài Windows Service

Mở PowerShell bằng quyền Administrator:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-service.ps1
```

Service name: `HeliosAttendanceSyncService`.

Script sẽ đăng ký:

```text
publish\app\HeliosAttendanceSync.exe --service
```

## Dữ liệu local

App và service dùng chung thư mục:

```text
C:\ProgramData\HELIOS Attendance Sync
```

Database SQLite:

```text
C:\ProgramData\HELIOS Attendance Sync\attendance_sync.db
```

## Adapter thiết bị hiện tại

MVP hiện có:

- Test kết nối TCP tới IP/port máy chấm công.
- Đọc log tạm từ `C:\ProgramData\HELIOS Attendance Sync\sample_logs.csv` nếu file tồn tại.

Để thử luồng sync bằng dữ liệu mẫu, copy `samples\sample_logs.csv` vào thư mục data ở trên và tạo thiết bị `MCC_HN_01` trong app. Khi có hãng/model/SDK máy thật, thay phần đọc log trong `TcpAttendanceDeviceClient`.
