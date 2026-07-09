# PLAN.md — Triển khai App Windows đồng bộ máy chấm công lên Web

## 1. Mục tiêu

Xây dựng một ứng dụng Windows nội bộ giúp tự động lấy dữ liệu từ máy chấm công trong mạng LAN và cập nhật lên hệ thống web qua API.

Mục tiêu chính:

- Tự động đọc dữ liệu chấm công từ máy qua LAN/Wi-Fi.
- Đẩy dữ liệu lên web theo chu kỳ, ví dụ mỗi 5 phút.
- Có app Windows dễ thao tác cho nhân sự nội bộ.
- Có Windows Service chạy ngầm để đảm bảo tự động đồng bộ kể cả khi người dùng không mở app.
- Lưu dữ liệu gốc từ máy chấm công để đối soát.
- Không mở trực tiếp máy chấm công ra internet.
- Chống trùng dữ liệu.
- Có cơ chế lưu tạm và gửi lại khi mất mạng.
- Có màn hình kiểm tra lỗi đồng bộ, lỗi mã nhân viên, lỗi API.

---

## 2. Mô hình tổng thể

```text
Máy chấm công
        ↓ LAN/Wi-Fi nội bộ
Windows Sync App / Windows Service
        ↓ HTTPS API
Website Backend
        ↓
Database
        ↓
Web quản lý công
```

Mô hình chi tiết:

```text
Nhân viên chấm công
        ↓
Máy chấm công tại cửa hàng/văn phòng
        ↓
App Windows nội bộ đọc log
        ↓
App gửi log lên API website
        ↓
Website lưu vào bảng log gốc
        ↓
Website xử lý theo ca làm
        ↓
Hiển thị bảng công ngày/tháng
```

---

## 3. Phạm vi triển khai

### 3.1. Trong phạm vi

- App Windows để cấu hình và theo dõi đồng bộ.
- Windows Service chạy nền để tự động đồng bộ.
- Kết nối máy chấm công qua IP nội bộ.
- Gửi dữ liệu lên API website.
- Lưu log đồng bộ local.
- Lưu log chưa gửi khi mất mạng.
- Gửi lại dữ liệu lỗi.
- Web API nhận dữ liệu chấm công.
- Database lưu dữ liệu gốc.
- Màn hình web xem log chấm công và lỗi mapping.

### 3.2. Ngoài phạm vi giai đoạn đầu

- Tính lương hoàn chỉnh.
- Chấm công nâng cao theo nhiều loại ca phức tạp.
- Tự động đồng bộ vân tay/khuôn mặt từ web xuống máy.
- Quản lý phân quyền nhân sự phức tạp.
- Auto update app.
- Kết nối nhiều loại máy khác nhau nếu chưa test SDK.

---

## 4. Kiến trúc đề xuất

### 4.1. Thành phần 1 — Máy chấm công

Máy chấm công cần kết nối mạng LAN hoặc Wi-Fi nội bộ.

Thông tin cần cấu hình:

```text
Device ID: MCC_HN_01
Tên máy: Máy chấm công Nguyễn Trãi
Chi nhánh: HN_NGUYENTRAI
IP: 192.168.1.201
Port: 4370
Password thiết bị: 0
```

Yêu cầu:

- Đặt IP tĩnh cho máy chấm công.
- Đồng bộ giờ máy chấm công đúng giờ Việt Nam.
- Không xóa log trên máy trong giai đoạn đầu.
- Mã nhân viên trên máy phải trùng hoặc mapping được với mã nhân viên trên web.

---

### 4.2. Thành phần 2 — Windows Service

Windows Service là phần chạy ngầm, chịu trách nhiệm đồng bộ tự động.

Nhiệm vụ:

- Tự chạy khi máy tính bật.
- Đọc cấu hình từ local database/config.
- Kết nối máy chấm công theo chu kỳ.
- Đọc log chấm công.
- Lọc dữ liệu cần gửi.
- Gửi lên API website.
- Lưu lịch sử đồng bộ.
- Lưu dữ liệu chưa gửi nếu lỗi mạng.
- Tự gửi lại dữ liệu pending.
- Ghi log lỗi để app giao diện đọc và hiển thị.

Chu kỳ mặc định:

```text
5 phút/lần
```

Có thể cấu hình:

```text
1 phút, 5 phút, 10 phút, 15 phút
```

Khuyến nghị dùng 5 phút để cân bằng giữa realtime và ổn định.

---

### 4.3. Thành phần 3 — Windows App giao diện

App giao diện dùng để nhân sự dễ thao tác.

Nhiệm vụ:

- Xem trạng thái service.
- Cấu hình máy chấm công.
- Cấu hình API website.
- Test kết nối máy chấm công.
- Test kết nối API.
- Bấm đồng bộ thủ công.
- Xem lần đồng bộ gần nhất.
- Xem log lỗi.
- Xem dữ liệu đang chờ gửi lại.
- Khởi động lại service nếu cần.

Giao diện nên đơn giản, ưu tiên dễ dùng.

---

### 4.4. Thành phần 4 — API Website

Website cần có API nhận dữ liệu từ app Windows.

Endpoint đề xuất:

```http
POST /api/attendance/sync
```

API cần xử lý:

- Xác thực token.
- Kiểm tra device_id.
- Kiểm tra employee_code.
- Lưu dữ liệu vào bảng raw logs.
- Chống trùng dữ liệu.
- Ghi lỗi mapping nếu mã nhân viên chưa tồn tại.
- Trả kết quả về cho app.

---

### 4.5. Thành phần 5 — Database Website

Database website cần lưu ít nhất 3 nhóm dữ liệu:

```text
1. Danh sách nhân sự
2. Log chấm công gốc
3. Lỗi đồng bộ/mapping
```

Sau đó mới xử lý ra bảng công ngày/tháng.

---

## 5. Công nghệ đề xuất

### 5.1. App Windows

Khuyến nghị:

```text
C# / .NET
WinForms hoặc WPF
Windows Service
SQLite local
```

Lý do:

- Phù hợp môi trường Windows.
- Dễ đóng gói cài đặt.
- Dễ chạy service nền.
- Dễ làm giao diện quản lý.
- Tương thích tốt với nhiều SDK máy chấm công dạng DLL/COM.

---

### 5.2. Local Database

Khuyến nghị dùng:

```text
SQLite
```

Dùng để lưu:

- Cấu hình app.
- Danh sách máy chấm công.
- Lịch sử đồng bộ.
- Log lỗi.
- Dữ liệu pending chưa gửi lên web.

---

### 5.3. Website Backend

Có thể dùng stack hiện tại của website:

```text
Laravel / Node.js / NestJS / Django / ASP.NET
```

Không bắt buộc đổi công nghệ. Chỉ cần website có API nhận dữ liệu và database lưu log.

---

## 6. Thiết kế dữ liệu local trong app Windows

File database local:

```text
attendance_sync.db
```

### 6.1. Bảng settings

Lưu cấu hình chung.

```sql
CREATE TABLE settings (
    key TEXT PRIMARY KEY,
    value TEXT,
    updated_at TEXT
);
```

Ví dụ dữ liệu:

```text
api_url = https://your-domain.com/api/attendance/sync
api_token = ********
sync_interval_minutes = 5
read_back_days = 1
```

---

### 6.2. Bảng devices

Lưu danh sách máy chấm công.

```sql
CREATE TABLE devices (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    device_id TEXT NOT NULL UNIQUE,
    device_name TEXT,
    store_code TEXT,
    ip_address TEXT NOT NULL,
    port INTEGER DEFAULT 4370,
    password INTEGER DEFAULT 0,
    is_active INTEGER DEFAULT 1,
    last_success_sync_at TEXT,
    last_error TEXT,
    created_at TEXT,
    updated_at TEXT
);
```

---

### 6.3. Bảng sync_logs

Lưu lịch sử các lần đồng bộ.

```sql
CREATE TABLE sync_logs (
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
```

---

### 6.4. Bảng pending_logs

Lưu log chưa gửi được lên web.

```sql
CREATE TABLE pending_logs (
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
```

---

### 6.5. Bảng app_errors

Lưu lỗi hệ thống app.

```sql
CREATE TABLE app_errors (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    error_type TEXT,
    device_id TEXT,
    message TEXT,
    detail TEXT,
    created_at TEXT
);
```

---

## 7. Thiết kế database trên website

### 7.1. Bảng employees

```sql
CREATE TABLE employees (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    employee_code VARCHAR(50) UNIQUE NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    department VARCHAR(100),
    store_code VARCHAR(100),
    status VARCHAR(50) DEFAULT 'active',
    created_at DATETIME,
    updated_at DATETIME
);
```

---

### 7.2. Bảng attendance_devices

```sql
CREATE TABLE attendance_devices (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    device_id VARCHAR(100) UNIQUE NOT NULL,
    device_name VARCHAR(255),
    store_code VARCHAR(100),
    ip_address VARCHAR(100),
    port INT DEFAULT 4370,
    api_token_hash VARCHAR(255),
    status VARCHAR(50) DEFAULT 'active',
    last_sync_at DATETIME,
    created_at DATETIME,
    updated_at DATETIME
);
```

---

### 7.3. Bảng attendance_raw_logs

Đây là bảng quan trọng nhất, lưu dữ liệu gốc từ máy.

```sql
CREATE TABLE attendance_raw_logs (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    device_id VARCHAR(100) NOT NULL,
    store_code VARCHAR(100),
    employee_code VARCHAR(50) NOT NULL,
    punch_time DATETIME NOT NULL,
    verify_type VARCHAR(50),
    raw_payload JSON,
    synced_at DATETIME,
    created_at DATETIME,

    UNIQUE KEY unique_punch (
        device_id,
        employee_code,
        punch_time
    )
);
```

Nguyên tắc chống trùng:

```text
device_id + employee_code + punch_time
```

Nếu app gửi lại cùng một log nhiều lần, database vẫn không bị nhân đôi.

---

### 7.4. Bảng attendance_sync_errors

Lưu lỗi mapping hoặc lỗi dữ liệu.

```sql
CREATE TABLE attendance_sync_errors (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    device_id VARCHAR(100),
    store_code VARCHAR(100),
    employee_code VARCHAR(50),
    punch_time DATETIME,
    error_type VARCHAR(100),
    error_message TEXT,
    raw_payload JSON,
    created_at DATETIME
);
```

Ví dụ lỗi:

```text
EMPLOYEE_NOT_FOUND
DEVICE_NOT_FOUND
INVALID_PUNCH_TIME
INACTIVE_EMPLOYEE
```

---

### 7.5. Bảng attendance_daily

Bảng công sau khi xử lý theo ca.

```sql
CREATE TABLE attendance_daily (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    employee_code VARCHAR(50) NOT NULL,
    work_date DATE NOT NULL,
    first_checkin DATETIME,
    last_checkout DATETIME,
    late_minutes INT DEFAULT 0,
    early_leave_minutes INT DEFAULT 0,
    overtime_minutes INT DEFAULT 0,
    work_status VARCHAR(50),
    created_at DATETIME,
    updated_at DATETIME,

    UNIQUE KEY unique_employee_date (
        employee_code,
        work_date
    )
);
```

Lưu ý:

```text
attendance_raw_logs là dữ liệu gốc.
attendance_daily là dữ liệu đã xử lý.
Không được bỏ qua attendance_raw_logs.
```

---

## 8. Thiết kế API đồng bộ

### 8.1. Endpoint

```http
POST /api/attendance/sync
```

---

### 8.2. Header

```http
Authorization: Bearer {DEVICE_API_TOKEN}
Content-Type: application/json
```

---

### 8.3. Payload

```json
{
  "device_id": "MCC_HN_01",
  "store_code": "HN_NGUYENTRAI",
  "logs": [
    {
      "employee_code": "NV001",
      "punch_time": "2026-07-09 08:27:15",
      "verify_type": "fingerprint"
    },
    {
      "employee_code": "NV002",
      "punch_time": "2026-07-09 08:31:40",
      "verify_type": "face"
    }
  ]
}
```

---

### 8.4. Response thành công

```json
{
  "success": true,
  "inserted": 2,
  "duplicated": 0,
  "failed": 0,
  "errors": []
}
```

---

### 8.5. Response có lỗi mapping

```json
{
  "success": true,
  "inserted": 1,
  "duplicated": 0,
  "failed": 1,
  "errors": [
    {
      "employee_code": "123",
      "punch_time": "2026-07-09 08:30:00",
      "error_type": "EMPLOYEE_NOT_FOUND",
      "message": "Mã nhân viên chưa tồn tại trên hệ thống"
    }
  ]
}
```

---

### 8.6. Response lỗi xác thực

```json
{
  "success": false,
  "error": "UNAUTHORIZED",
  "message": "Token không hợp lệ"
}
```

---

## 9. Logic xử lý API website

Khi nhận request từ app Windows:

```text
1. Kiểm tra Authorization token.
2. Xác định device_id theo token.
3. Kiểm tra device_id có tồn tại không.
4. Kiểm tra logs có đúng định dạng không.
5. Với từng log:
   5.1. Kiểm tra employee_code.
   5.2. Kiểm tra punch_time.
   5.3. Nếu mã nhân viên chưa tồn tại, lưu vào attendance_sync_errors.
   5.4. Nếu log đã tồn tại, tính là duplicated.
   5.5. Nếu hợp lệ và chưa tồn tại, insert vào attendance_raw_logs.
6. Cập nhật last_sync_at cho thiết bị.
7. Trả kết quả inserted / duplicated / failed cho app.
```

Pseudo logic:

```text
for log in logs:
    if employee_code không tồn tại:
        lưu lỗi EMPLOYEE_NOT_FOUND
        failed += 1
        continue

    if device_id + employee_code + punch_time đã tồn tại:
        duplicated += 1
        continue

    insert attendance_raw_logs
    inserted += 1
```

---

## 10. Logic đồng bộ trong Windows Service

### 10.1. Chu kỳ chạy

```text
Cứ 5 phút chạy 1 lần
```

Mỗi lần chạy:

```text
1. Đọc danh sách devices đang active.
2. Với từng device:
   2.1. Test kết nối máy chấm công.
   2.2. Đọc attendance logs.
   2.3. Lọc log từ last_success_sync_at - read_back_days.
   2.4. Gửi pending logs trước.
   2.5. Gửi logs mới lên API.
   2.6. Nếu gửi thành công, cập nhật last_success_sync_at.
   2.7. Nếu gửi lỗi, lưu vào pending_logs.
   2.8. Ghi sync_logs.
```

---

### 10.2. Đọc lùi để tránh mất dữ liệu

Không chỉ đọc từ đúng thời điểm lần cuối.

Nên đọc lùi:

```text
last_success_sync_at - 1 ngày
```

Ví dụ:

```text
Lần đồng bộ gần nhất: 2026-07-09 10:00:00
Lần sau đọc từ: 2026-07-08 10:00:00
```

Lý do:

- Tránh mất log nếu mạng lỗi.
- Tránh mất log nếu service bị tắt.
- Tránh mất log nếu máy chấm công sai giờ nhẹ.
- Web đã có chống trùng nên gửi lại dữ liệu cũ không sao.

---

### 10.3. Gửi batch

Không gửi từng log một.

Nên gửi theo batch:

```text
100 - 500 log/lần
```

Nếu ít dữ liệu, gửi toàn bộ một batch.

---

### 10.4. Xử lý mất mạng

Nếu gọi API lỗi:

```text
1. Không xóa dữ liệu.
2. Lưu log vào pending_logs.
3. Tăng retry_count.
4. Lần sau gửi lại.
```

Quy tắc gửi lại:

```text
Ưu tiên gửi pending_logs trước.
Sau đó mới gửi logs mới.
```

---

## 11. Giao diện app Windows đề xuất

### 11.1. Màn hình Tổng quan

Hiển thị:

```text
Trạng thái Service: Đang chạy / Đã dừng
Trạng thái API: Kết nối thành công / Lỗi
Tổng số máy chấm công: 1
Máy đang kết nối: 1
Máy lỗi: 0
Lần đồng bộ gần nhất: 09/07/2026 10:05
Số log đồng bộ hôm nay: 128
Số log đang chờ gửi lại: 0
Số lỗi mapping: 3
```

Nút thao tác:

```text
[Đồng bộ ngay]
[Test API]
[Khởi động lại Service]
[Mở thư mục log]
```

Màu trạng thái:

```text
Xanh: Hoạt động bình thường
Vàng: Có cảnh báo
Đỏ: Có lỗi cần xử lý
```

---

### 11.2. Màn hình Thiết bị

Danh sách máy:

```text
Device ID    Tên máy             Chi nhánh        IP              Trạng thái
MCC_HN_01    Nguyễn Trãi         HN_NGUYENTRAI    192.168.1.201   Đang kết nối
MCC_HCM_01   Bà Hạt              HCM_BAHAT        192.168.2.201   Mất kết nối
```

Form cấu hình:

```text
Device ID
Tên máy
Chi nhánh
IP
Port
Password
Trạng thái active/inactive
```

Nút:

```text
[Test kết nối]
[Lưu]
[Xóa]
```

---

### 11.3. Màn hình API

Cấu hình:

```text
API URL
API Token
Timeout
```

Nút:

```text
[Test API]
[Lưu cấu hình]
```

Lưu ý:

- Token phải được mã hóa hoặc che trên giao diện.
- Không hiển thị token đầy đủ sau khi lưu.
- Chỉ hiển thị dạng `************`.

---

### 11.4. Màn hình Lịch sử đồng bộ

Bảng lịch sử:

```text
Thời gian bắt đầu     Máy          Kết quả       Đọc được   Gửi mới   Trùng   Lỗi
09/07/2026 10:05      MCC_HN_01    Thành công    120        12        108     0
09/07/2026 10:10      MCC_HN_01    Thành công    122        0         122     0
09/07/2026 10:15      MCC_HN_01    Lỗi API       130        0         0       130
```

Có filter:

```text
- Theo ngày
- Theo máy
- Theo trạng thái
```

---

### 11.5. Màn hình Pending Logs

Hiển thị dữ liệu chưa gửi được:

```text
Máy          Mã NV    Thời gian chấm       Số lần gửi lại    Lỗi gần nhất
MCC_HN_01    NV001    09/07/2026 08:30     2                 API timeout
```

Nút:

```text
[Gửi lại ngay]
[Xóa pending đã xử lý]
```

---

### 11.6. Màn hình Lỗi Mapping

Hiển thị lỗi do web trả về:

```text
Máy          Mã trên máy    Thời gian chấm       Lỗi
MCC_HN_01    123            09/07/2026 08:31     Mã nhân viên chưa tồn tại
MCC_HN_01    456            09/07/2026 08:45     Nhân viên đã nghỉ việc
```

Mục tiêu là giúp nhân sự biết cần tạo/mapping mã nhân viên nào trên web.

---

## 12. Quy tắc bảo mật

### 12.1. Không public máy chấm công

Không mở port máy chấm công trực tiếp ra internet.

Sai:

```text
Internet → IP public → Máy chấm công
```

Đúng:

```text
Máy chấm công LAN → App Windows → HTTPS → Website
```

---

### 12.2. Dùng token riêng

Mỗi thiết bị hoặc mỗi chi nhánh nên có token riêng.

Ví dụ:

```text
MCC_HN_01  → token A
MCC_HCM_01 → token B
```

Nếu lộ token, chỉ khóa token đó.

---

### 12.3. API bắt buộc dùng HTTPS

Không gửi dữ liệu qua HTTP thường.

Bắt buộc:

```text
https://your-domain.com/api/attendance/sync
```

---

### 12.4. Che token trong app

App không hiển thị token đầy đủ.

Hiển thị:

```text
***************
```

---

### 12.5. Log không lưu thông tin nhạy cảm quá mức

Log app chỉ nên lưu:

```text
device_id
employee_code
punch_time
status
error
```

Không cần lưu dữ liệu sinh trắc học.

---

## 13. Quy trình tính công trên web

App chỉ đồng bộ log gốc, không tính công.

Web xử lý công theo quy trình riêng:

```text
attendance_raw_logs
        ↓
Ghép nhân viên
        ↓
Ghép lịch làm / ca làm
        ↓
Xác định check-in đầu tiên
        ↓
Xác định check-out cuối cùng
        ↓
Tính đi muộn / về sớm / tăng ca
        ↓
attendance_daily
```

Ví dụ:

```text
Ca làm: 08:30 - 17:30
Log trong ngày:
- 08:42
- 12:01
- 13:28
- 17:45

Kết quả:
- Check-in: 08:42
- Check-out: 17:45
- Đi muộn: 12 phút
- Về sớm: 0 phút
- Trạng thái: Có công
```

---

## 14. Các trạng thái công nên có

```text
Đủ dữ liệu
Thiếu check-in
Thiếu check-out
Chưa gán ca
Mã nhân viên chưa map
Nhân viên đã nghỉ việc
Nghỉ phép
Nghỉ không phép
Có công
Đi muộn
Về sớm
Tăng ca
```

---

## 15. Quy trình triển khai theo giai đoạn

### Giai đoạn 1 — Khảo sát thiết bị

#### Việc cần làm

- Xác định hãng máy chấm công.
- Xác định model máy.
- Kiểm tra máy có LAN/Wi-Fi không.
- Kiểm tra IP/port/password.
- Kiểm tra có đọc được log qua SDK không.
- Kiểm tra mã nhân viên trên máy đang dùng trường nào.

#### Kết quả cần đạt

```text
Kết nối được máy chấm công từ máy tính nội bộ.
Đọc được danh sách user.
Đọc được log chấm công.
Xác định được employee_code.
```

---

### Giai đoạn 2 — Làm API website

#### Việc cần làm

- Tạo bảng attendance_devices.
- Tạo bảng attendance_raw_logs.
- Tạo bảng attendance_sync_errors.
- Tạo API `/api/attendance/sync`.
- Tạo xác thực bằng Bearer token.
- Tạo logic chống trùng.
- Tạo logic ghi lỗi mapping.

#### Kết quả cần đạt

- Gửi payload test lên API thành công.
- API lưu được log mới.
- API bỏ qua log trùng.
- API ghi nhận lỗi mã nhân viên chưa tồn tại.

---

### Giai đoạn 3 — Làm bản POC app đồng bộ

#### Việc cần làm

- Tạo app đọc cấu hình thiết bị.
- Kết nối máy chấm công.
- Đọc log từ máy.
- Gửi log lên API.
- Ghi log local.
- Cho phép bấm đồng bộ thủ công.

#### Kết quả cần đạt

```text
Bấm "Đồng bộ ngay" trên app
→ App đọc được log máy chấm công
→ App gửi lên web
→ Web hiển thị log vừa đồng bộ
```

---

### Giai đoạn 4 — Làm Windows Service

#### Việc cần làm

- Tạo Windows Service chạy nền.
- Tự chạy khi Windows khởi động.
- Đồng bộ theo chu kỳ.
- Gửi pending logs trước.
- Retry khi lỗi.
- Ghi sync_logs local.
- Giao diện app đọc trạng thái service.

#### Kết quả cần đạt

```text
Không cần mở app giao diện
Service vẫn tự đồng bộ mỗi 5 phút
```

---

### Giai đoạn 5 — Hoàn thiện app giao diện

#### Việc cần làm

- Màn hình Tổng quan.
- Màn hình Thiết bị.
- Màn hình API.
- Màn hình Lịch sử đồng bộ.
- Màn hình Pending Logs.
- Màn hình Lỗi Mapping.
- Nút Test kết nối máy.
- Nút Test API.
- Nút Đồng bộ ngay.
- Nút Restart Service.

#### Kết quả cần đạt

Nhân sự không cần mở code vẫn kiểm tra được:

```text
App có chạy không?
Máy chấm công có kết nối không?
API có lỗi không?
Dữ liệu đã đồng bộ lúc nào?
Có log nào chưa gửi không?
Có mã nhân viên nào lỗi không?
```

---

### Giai đoạn 6 — Kiểm thử thực tế

#### Test case bắt buộc

##### Test 1 — Kết nối máy thành công

```text
Input: IP máy đúng
Expected: App báo kết nối thành công
```

##### Test 2 — Sai IP máy

```text
Input: IP sai
Expected: App báo mất kết nối, không crash
```

##### Test 3 — Gửi API thành công

```text
Input: Log hợp lệ
Expected: Web lưu vào attendance_raw_logs
```

##### Test 4 — Gửi trùng log

```text
Input: Gửi cùng log 2 lần
Expected: Web chỉ lưu 1 lần, lần sau trả duplicated
```

##### Test 5 — Mã nhân viên chưa tồn tại

```text
Input: employee_code không có trên web
Expected: Web lưu vào attendance_sync_errors
```

##### Test 6 — Mất internet

```text
Input: Tắt mạng internet máy tính
Expected: App lưu log vào pending_logs
```

##### Test 7 — Có mạng lại

```text
Input: Bật mạng lại
Expected: App tự gửi pending_logs lên web
```

##### Test 8 — Service tự chạy

```text
Input: Restart máy tính
Expected: Windows Service tự chạy lại
```

##### Test 9 — Không mở app giao diện

```text
Input: Đóng app giao diện
Expected: Service vẫn đồng bộ
```

##### Test 10 — Máy chấm công sai giờ nhẹ

```text
Input: Log cũ hơn last_sync_at
Expected: App vẫn đọc lùi 1 ngày và gửi lại, web chống trùng
```

---

### Giai đoạn 7 — Triển khai production

#### Việc cần làm

- Cài app trên máy nội bộ tại điểm có máy chấm công.
- Cấu hình IP máy chấm công.
- Cấu hình API URL.
- Cấu hình API token.
- Test kết nối.
- Test đồng bộ thủ công.
- Bật Windows Service.
- Theo dõi log trong 3–7 ngày đầu.
- Đối soát log máy với log trên web.

#### Kết quả cần đạt

```text
Dữ liệu chấm công tự động cập nhật lên web ổn định.
Nhân sự có thể kiểm tra trạng thái qua app.
Không cần xuất Excel thủ công từ phần mềm chấm công.
```

---

## 16. Checklist trước khi nghiệm thu

### 16.1. Checklist app Windows

- [ ] Cài được app trên Windows.
- [ ] App mở được giao diện.
- [ ] Lưu được cấu hình máy chấm công.
- [ ] Test kết nối máy thành công.
- [ ] Lưu được API URL.
- [ ] Lưu được API token.
- [ ] Test API thành công.
- [ ] Bấm đồng bộ thủ công thành công.
- [ ] Service tự chạy nền.
- [ ] Service tự chạy lại khi restart máy.
- [ ] Có log lịch sử đồng bộ.
- [ ] Có pending logs khi mất mạng.
- [ ] Có retry khi mạng hoạt động lại.
- [ ] Có hiển thị lỗi mapping.

---

### 16.2. Checklist website

- [ ] Có bảng attendance_devices.
- [ ] Có bảng attendance_raw_logs.
- [ ] Có bảng attendance_sync_errors.
- [ ] API xác thực bằng token.
- [ ] API chống trùng log.
- [ ] API trả kết quả inserted / duplicated / failed.
- [ ] Web hiển thị log chấm công gốc.
- [ ] Web hiển thị lỗi mapping.
- [ ] Web cập nhật last_sync_at cho thiết bị.
- [ ] Web có màn hình bảng công sau xử lý.

---

### 16.3. Checklist bảo mật

- [ ] Không mở máy chấm công ra internet.
- [ ] API dùng HTTPS.
- [ ] Token được che trong app.
- [ ] Mỗi máy/chi nhánh có token riêng.
- [ ] Có thể khóa token nếu cần.
- [ ] Không lưu dữ liệu sinh trắc học.
- [ ] Log không chứa thông tin nhạy cảm không cần thiết.

---

## 17. Tiêu chí nghiệm thu

Dự án được nghiệm thu khi đạt các tiêu chí sau:

```text
1. App Windows kết nối được máy chấm công trong LAN.
2. App đọc được log chấm công thực tế.
3. App gửi dữ liệu lên API website thành công.
4. Website lưu được dữ liệu vào attendance_raw_logs.
5. Gửi trùng không bị nhân đôi dữ liệu.
6. Mất mạng không mất log, app lưu pending và gửi lại sau.
7. Windows Service tự chạy khi bật máy.
8. Nhân sự có thể xem trạng thái đồng bộ trên app.
9. Web có thể xem log gốc và lỗi mapping.
10. Không cần thao tác xuất Excel thủ công.
```

---

## 18. Rủi ro và cách xử lý

### Rủi ro 1 — Không đọc được máy chấm công

Nguyên nhân có thể:

```text
- Sai IP
- Sai port
- Máy không cùng mạng LAN
- Máy không hỗ trợ SDK đang dùng
- Firewall chặn kết nối
```

Cách xử lý:

```text
- Ping IP máy
- Kiểm tra port
- Test bằng phần mềm chính hãng
- Kiểm tra model máy
- Đổi SDK/thư viện phù hợp
```

---

### Rủi ro 2 — Mã nhân viên trên máy không trùng web

Cách xử lý:

```text
- Chuẩn hóa employee_code
- Tạo bảng mapping nếu cần
- Không match bằng tên nhân viên
```

---

### Rủi ro 3 — Mất mạng internet

Cách xử lý:

```text
- Lưu pending_logs local
- Gửi lại tự động khi có mạng
```

---

### Rủi ro 4 — Nhân sự tắt app

Cách xử lý:

```text
- Dùng Windows Service chạy nền
- App giao diện chỉ để quản lý
```

---

### Rủi ro 5 — Máy tính nội bộ bị tắt

Cách xử lý:

```text
- Chọn máy tính thường xuyên bật
- Hoặc dùng mini PC riêng
- Web cảnh báo nếu thiết bị không đồng bộ quá lâu
```

---

### Rủi ro 6 — Log bị trùng

Cách xử lý:

```text
- Web tạo unique key theo device_id + employee_code + punch_time
```

---

## 19. Cảnh báo nên có trên web

Website nên có cảnh báo:

```text
- Máy chấm công chưa đồng bộ quá 3 giờ.
- Có mã nhân viên chưa mapping.
- Có log lỗi trong ngày.
- Có thiết bị mất kết nối.
- Số log hôm nay thấp bất thường.
```

Ví dụ:

```text
Cảnh báo: MCC_HN_01 chưa đồng bộ từ 08:00, vui lòng kiểm tra app tại máy nội bộ.
```

---

## 20. Bản MVP đề xuất

Bản MVP nên làm trước với phạm vi nhỏ:

```text
1 máy chấm công
1 app Windows
1 Windows Service
1 API nhận dữ liệu
1 bảng log gốc
1 màn hình web xem log
```

Tính năng MVP:

- Cấu hình IP máy chấm công.
- Cấu hình API URL/token.
- Test kết nối máy.
- Test API.
- Đồng bộ thủ công.
- Đồng bộ tự động mỗi 5 phút.
- Lưu log local.
- Lưu pending khi lỗi.
- Gửi lại pending.
- Chống trùng trên web.

Chưa cần làm ở MVP:

- Nhiều loại máy.
- Auto update app.
- Tính lương phức tạp.
- Đồng bộ nhân viên từ web xuống máy.
- Đồng bộ vân tay/khuôn mặt.

---

## 21. Roadmap mở rộng

Sau khi MVP ổn định, có thể mở rộng:

### Phase 2

- Hỗ trợ nhiều máy chấm công.
- Hỗ trợ nhiều chi nhánh.
- Dashboard trạng thái thiết bị trên web.
- Cảnh báo qua email/Zalo/Telegram khi lỗi đồng bộ.
- Export log lỗi ra Excel.

### Phase 3

- Tính công theo nhiều ca.
- Xử lý ca gãy.
- Xử lý tăng ca.
- Xử lý nghỉ phép.
- Duyệt chỉnh công.
- Phân quyền quản lý theo chi nhánh.

### Phase 4

- Auto update app Windows.
- Mã hóa cấu hình local.
- Gửi health check định kỳ.
- Kết nối cloud nếu đổi loại máy.
- Đồng bộ nhân viên từ web xuống app.

---

## 22. Quy trình vận hành hằng ngày

Nhân sự không cần thao tác nếu hệ thống chạy bình thường.

Chỉ cần kiểm tra khi có cảnh báo.

Quy trình kiểm tra nhanh:

```text
1. Mở app HELIOS Attendance Sync.
2. Xem trạng thái tổng quan.
3. Nếu màu xanh: không cần làm gì.
4. Nếu màu vàng: kiểm tra pending logs hoặc lỗi mapping.
5. Nếu màu đỏ: test kết nối máy và test API.
6. Nếu cần, bấm Đồng bộ ngay.
7. Nếu vẫn lỗi, gửi log lỗi cho kỹ thuật.
```

---

## 23. Quy trình xử lý lỗi thường gặp

### 23.1. App báo mất kết nối máy chấm công

Kiểm tra:

```text
- Máy chấm công có bật không?
- Máy tính có cùng mạng LAN không?
- IP máy chấm công có đổi không?
- Có ping được IP máy không?
- Port có đúng không?
```

---

### 23.2. App báo lỗi API

Kiểm tra:

```text
- Máy tính có internet không?
- API URL có đúng không?
- Token có đúng không?
- Website có đang hoạt động không?
```

---

### 23.3. Web báo mã nhân viên chưa tồn tại

Xử lý:

```text
- Kiểm tra mã nhân viên trên máy chấm công.
- Tạo nhân viên trên web nếu chưa có.
- Hoặc tạo mapping mã máy sang mã nhân viên web.
```

---

### 23.4. Dữ liệu bị thiếu

Kiểm tra:

```text
- Log có tồn tại trên máy chấm công không?
- App có đọc được log đó không?
- Log có nằm trong pending_logs không?
- API có trả lỗi không?
- Web có lưu vào attendance_sync_errors không?
```

---

## 24. Nguyên tắc quan trọng

```text
1. Không mở máy chấm công ra internet.
2. Không xóa log trên máy trong giai đoạn đầu.
3. Không match nhân viên bằng tên.
4. Không tính công trực tiếp trong app Windows.
5. App chỉ đồng bộ dữ liệu gốc.
6. Web mới xử lý công theo ca.
7. Luôn lưu raw logs để đối soát.
8. Luôn chống trùng dữ liệu.
9. Luôn có pending logs khi mất mạng.
10. Luôn có màn hình lỗi để nhân sự tự kiểm tra.
```

---

## 25. Kết luận

Phương án triển khai tối ưu là:

```text
Máy chấm công trong LAN
→ Windows Service tự động đọc log
→ App Windows để cấu hình và kiểm tra
→ API website nhận dữ liệu
→ Database lưu raw logs
→ Web xử lý bảng công
```

Đây là mô hình phù hợp để triển khai thực tế vì:

- An toàn hơn so với mở máy chấm công ra internet.
- Dễ thao tác cho nhân sự.
- Ổn định vì có Windows Service chạy nền.
- Không mất dữ liệu khi lỗi mạng.
- Dễ mở rộng nhiều chi nhánh.
- Dễ đối soát vì luôn lưu dữ liệu gốc.
