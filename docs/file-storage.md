# Lưu file báo cáo trên S3 thay Firebase Storage (MB-53)

## Vì sao dùng S3

Đề Lab 3 §6 yêu cầu Cloud Storage. Tạo bucket Firebase Storage có thể cần gói Blaze, nhóm đang dùng gói Spark. Ngày 05/11/2026 giảng viên cho dùng S3 hoặc Cloudinary thay thế. Nhóm chọn S3 vì Collaboration Service đã có sẵn kho S3 (SeaweedFS trong Docker) cho file dự án, không cần thêm dịch vụ hay tài khoản ngoài.

## Đường đi của một báo cáo

1. App tạo PDF trên máy từ dữ liệu C01 (`report_pdf.dart`, gói `pdf`).
2. App gửi multipart `POST /api/reports/{userId}` qua Gateway, hiện tiến độ theo byte đã gửi.
3. Collaboration Service kiểm tra quyền, kiểm tra file là PDF, lưu vào `reports/{userId}/{thời gian}-{mã}.pdf`.
4. Server trả link ký sẵn, hết hạn sau `ObjectStorage:PresignMinutes` phút (mặc định 10).

## Quy tắc phân quyền

Quy tắc trên Firebase Storage sẽ là:

```
match /reports/{uid}/{file} {
  allow read, write: if request.auth != null && request.auth.uid == uid;
}
```

Backend làm tương đương:

| Firebase Storage | Ở đây |
|---|---|
| `request.auth != null` | JWT hợp lệ ở Gateway và `[Authorize]` ở Collaboration |
| `request.auth.uid == uid` | `ownerId` trên đường dẫn phải trùng claim `sub`, sai thì 403 `not-report-owner`. Admin cũng không ngoại lệ |
| Client không tự truy cập bucket | S3 không mở công khai; file chỉ tải được qua link ký sẵn do server cấp cho chủ file |
| Hạn đọc | Link ký sẵn tự hết hạn |

Không có bảng riêng: đường dẫn đã cho biết chủ báo cáo, nên không có dữ liệu nào phải giữ đồng bộ với S3.

Ai có link ký sẵn còn hạn thì tải được, giống URL tải của Firebase Storage. Vì vậy link chỉ sống 10 phút và app xin link mới mỗi lần mở.

## Chạy trên điện thoại cắm cáp

Link ký sẵn trỏ tới `ObjectStorage:PublicUrl` (dev: `http://localhost:9000`), nên ngoài cổng Gateway phải chuyển thêm cổng S3:

```bash
adb reverse tcp:8080 tcp:8080
adb reverse tcp:9000 tcp:9000
```

## Kết quả thử

Xem mục "MB-53" trong [test-report.md](test-report.md).
