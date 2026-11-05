# Thay đổi database 05/11/2026 (BE-87)

> Ghi cho Thiên cập nhật `docs/EXE_Database_Design.pdf` mục 4 (`identity_db`). Script: [`deploy/db/01_identity_db.sql`](../deploy/db/01_identity_db.sql). Database đang có dữ liệu chạy [`deploy/db/migrations/BE-87_client_error_reports.sql`](../deploy/db/migrations/BE-87_client_error_reports.sql), không cần reset.

## Bảng mới `identity.client_error_reports`

Báo cáo lỗi app gửi về để Bộ môn xem ở W30. Bản chính vẫn ở Firebase Crashlytics, vì Firebase không có API đọc Crashlytics miễn phí (BigQuery export cần gói Blaze).

| Cột | Kiểu | Null | Ghi chú |
|---|---|---|---|
| `id` | uuid | không | PK, `gen_random_uuid()` |
| `kind` | varchar(20) | không | `fatal` hoặc `non_fatal` (`ck_client_error_reports_kind`) |
| `message` | varchar(2000) | không | |
| `exception_type` | varchar(200) | không | Client không gửi thì lưu `Unknown` |
| `stack_trace` | text | có | Cắt còn 8192 ký tự |
| `first_stack_line` | varchar(300) | không | Dòng đầu của `stack_trace`, tính khi nhận, mặc định chuỗi rỗng |
| `platform` | varchar(20) | không | Ví dụ `android` |
| `app_version` | varchar(30) | không | |
| `device_model` | varchar(100) | có | |
| `os_version` | varchar(100) | có | |
| `user_id` | uuid | có | Không có khóa ngoại; null khi lỗi xảy ra trước đăng nhập |
| `role` | varchar(20) | có | |
| `screen` | varchar(200) | có | Route lúc xảy ra lỗi |
| `occurred_at` | timestamptz | không | Thời điểm trên máy, không cho vượt quá lúc server nhận |
| `received_at` | timestamptz | không | Mặc định `now()` |

Index: `idx_client_error_reports_occurred_at (occurred_at DESC)`, `idx_client_error_reports_kind_occurred_at (kind, occurred_at DESC)`.

## Khác với yêu cầu ban đầu

- **Thêm cột `first_stack_line`.** Top 5 lỗi gom theo `exception_type` và dòng đầu của stack; lưu sẵn dòng đầu thì gom bằng `GROUP BY`, không phải tách chuỗi trên từng dòng mỗi lần đọc.
- **`user_id` không có khóa ngoại.** Endpoint nhận cả khi chưa đăng nhập, `user_id` trong body chưa được kiểm tra; JWT hợp lệ thì lấy theo token.
- **Không lưu IP.** IP chỉ dùng để giới hạn tần suất trong bộ nhớ của Identity.

## Chưa làm

Chưa có job xóa báo cáo cũ. Bảng tăng dần theo số lỗi; nếu cần, thêm job ở Jobs.Host.
