# HELIOS Attendance Sync

App Windows MVP theo `PLAN_Tich_Hop_May_Cham_Cong_Web.md`.

Project dùng một executable duy nhất:

```text
HeliosAttendanceSync.exe
```

- Mở bình thường: chạy giao diện quản lý.
- Windows Service gọi với `--service`: chạy nền tự động theo chu kỳ đã cấu hình trên UI.
- Nếu service chưa cài, app sẽ hỏi cài service và tự bật UAC quyền Administrator.

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

Trong app, bấm `Cài Service` ở tab Tổng quan. Windows sẽ hỏi quyền Administrator, chọn Yes.

Hoặc mở PowerShell bằng quyền Administrator:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-service.ps1
```

Service name: `HeliosAttendanceSyncService`.

Script sẽ đăng ký:

```text
publish\hoffice\HeliosAttendanceSync.exe --service
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

App đọc máy ZK/Ronald Jack qua ZK SDK COM `zkemkeeper.CZKEM`.

- `Test kết nối` dùng SDK ZK thật, không chỉ mở port TCP.
- `Đồng bộ ngay` đọc log bằng `ReadGeneralLogData` và `SSR_GetGeneralLogData`.
- Publish mặc định là `win-x86` để tương thích với driver ZK 32-bit thường đi kèm các phần mềm chấm công.

Máy cài app cần có driver ZK SDK/`zkemkeeper.dll` đã được đăng ký COM. Nếu app báo thiếu `zkemkeeper.CZKEM` trong khi phần mềm khác vẫn đọc được log, hãy dùng bản HOFFICE `win-x86` hoặc đăng ký lại `zkemkeeper.dll` đúng kiến trúc.

Fallback dữ liệu mẫu vẫn còn cho môi trường test: copy `samples\sample_logs.csv` vào `C:\ProgramData\HELIOS Attendance Sync\sample_logs.csv`.
