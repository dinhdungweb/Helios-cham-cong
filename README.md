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

- Tab Thiết bị có trường `Loại máy`: `ZK / Ronald Jack`, `Soyal`, `Morpho Sigma`, `Somac`, `CSV / Excel`, `API HTTP`.
- Bản hiện tại đọc thật cho `ZK / Ronald Jack`; các loại còn lại đã có chỗ chọn nhưng sẽ báo `chưa hỗ trợ` cho tới khi bổ sung SDK/giao thức tương ứng.
- `Test kết nối` dùng SDK ZK thật, không chỉ mở port TCP.
- `Đồng bộ ngay` đọc log bằng `ReadGeneralLogData` và `SSR_GetGeneralLogData`.
- Publish mặc định là `win-x86` để tương thích với driver ZK 32-bit thường đi kèm các phần mềm chấm công.
- Tab Thiết bị có nút `Cài driver`: với `ZK / Ronald Jack`, app tự tìm `zkemkeeper.dll`, bật UAC và đăng ký SDK; nếu không tìm thấy thì mở hộp thoại để chọn file DLL. Các loại máy khác sẽ báo rõ là cần bổ sung SDK/giao thức của hãng.

Máy cài app cần có driver ZK SDK/`zkemkeeper.dll` đã được đăng ký COM. Nếu app báo thiếu `zkemkeeper.CZKEM`, bấm `Cài driver`. Để triển khai dễ hơn, có thể đặt `zkemkeeper.dll` cùng thư mục với `HeliosAttendanceSync.exe` hoặc trong thư mục `drivers` cạnh file exe.

## Đóng gói driver sẵn

Project có sẵn thư mục `src\Helios.Attendance.App\Drivers` và khi publish sẽ copy thành thư mục `drivers` cạnh `HeliosAttendanceSync.exe`.

Nếu có quyền phân phối SDK của nhà cung cấp, copy cả thư mục driver vào một trong các vị trí:

```text
src\Helios.Attendance.App\Drivers\zk
src\Helios.Attendance.App\Drivers\dtc
src\Helios.Attendance.App\Drivers\zkteco
src\Helios.Attendance.App\Drivers\ronald-jack
```

Không chỉ copy riêng `zkemkeeper.dll`; hãy copy cả các DLL đi kèm trong cùng thư mục SDK. Các file DLL/OCX proprietary đang được `.gitignore` để tránh push nhầm lên GitHub.

Fallback dữ liệu mẫu vẫn còn cho môi trường test: copy `samples\sample_logs.csv` vào `C:\ProgramData\HELIOS Attendance Sync\sample_logs.csv`.
