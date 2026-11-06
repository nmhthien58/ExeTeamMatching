# Seed dữ liệu demo (BE-57)

`seed-demo.mjs` tạo dữ liệu demo ẩn danh **qua API thật** (Gateway), chạy một lần sau DB script. Đi qua API để sự kiện Kafka tới đủ Identity, Notification, Matching và Collaboration; chèn thẳng SQL sẽ làm các read model lệch nhau.

## Chạy

Stack phải chạy ở `ASPNETCORE_ENVIRONMENT=Development`, vì script lấy token qua `POST /api/dev/token`.

```bash
docker compose -f deploy/docker-compose.yml up -d --build
docker compose -f deploy/docker-compose.yml --profile seed run --rm seed
```

Mất khoảng 1–2 phút. Chạy lại trên DB đã seed thì script dừng ngay, không tạo trùng (nhận biết bằng học kỳ `SP90`).

### Seed lại từ đầu

Seed chạy trên **DB trống**. Nếu lần trước dừng giữa chừng, script báo lỗi "chưa xong" thay vì chạy tiếp. Lệnh này **xóa toàn bộ dữ liệu** (DB, Kafka, file):

```bash
docker compose -f deploy/docker-compose.yml down -v
```

Rồi chạy lại hai lệnh ở trên.

**Volume cũ:** DB script chỉ chạy khi volume trống, nên volume tạo trước khi script đổi sẽ thiếu cột và API trả 500 (`42703`). Trường hợp này cần `down -v`, hoặc thêm cột theo mục "Volume cũ cần thêm cột" trong [`deploy/db/README.md`](../db/README.md) trước khi seed.

Không có Docker cho riêng bước seed thì chạy bằng Node 22: `node deploy/seed/seed-demo.mjs` (mặc định gọi `http://localhost:8080`, đổi bằng `SEED_GATEWAY_URL`).

## Lưu ý

- Script **đổi học kỳ hiện hành** sang `SP90`. Ngày học kỳ tính theo lúc chạy: đang ở tuần 2, hạn chọn slot hết tuần 3, tuần bắt đầu mentor là tuần 8. Để lâu sau khi seed thì các mốc này sẽ qua.
- Email là `@example.com` nên không đăng nhập Google được; lấy token bằng `POST /api/dev/token` với `role`, `email` và id tương ứng (`studentId`, `lecturerId`, `mentorId`). Cuối lượt chạy script in sẵn id các tài khoản chính.
- Toàn bộ dữ liệu ẩn danh: tên ghép ngẫu nhiên, không có SĐT.
- Muốn đăng nhập Google bằng tài khoản của mình với role Sinh viên: đặt `SEED_STUDENT_EMAIL` trong `deploy/.env` (không commit) trước khi seed. Email này phải khác email Bộ môn: trùng `ADMIN_EMAIL` hoặc `SEED_ADMIN_EMAIL` thì script dừng ngay. Mỗi email chỉ nên có một vai trò; script không kiểm tra được Admin cấp tay trong DB. Script tạo thêm sinh viên EXE101 `DS189999` với email đó: chưa có nhóm, đủ hồ sơ, đang tìm nhóm, có 1 lời mời từ nhóm Khởi Hành. Để trống thì bỏ qua.

## Dữ liệu tạo ra

| Phần | Nội dung | Màn hình dùng |
|---|---|---|
| Cố vấn | 4 giảng viên (`gv1`–`gv4.seed@example.com`), 2 mentor ngoài trường, `gv4` kiêm mentor | W11, W27, L06, M01 |
| Sinh viên | 30 EXE101, 12 EXE201, đủ hồ sơ 5 kỹ năng | W09, W10, S02, S04 |
| Nhóm EXE101 (đợt lập nhóm đang mở) | Bình Minh 5 người, Sao Mai 4, Hải Đăng 6, Khởi Hành 2 (chưa đủ, có bài tuyển) | S05–S08, S15 |
| Đang chờ | 1 lời mời, 1 yêu cầu tham gia kèm ý tưởng, 2 bài tìm nhóm, sinh viên chưa có nhóm | S10, S12, S18, S20 |
| Nhóm EXE201 (đã chốt) | Ánh Dương, Tiên Phong kế thừa từ mốc; 3 sinh viên chưa có nhóm | S15, W20–W22 |
| Ghép cố vấn | Giảng viên cho cả 6 nhóm (lý do `MUTUAL`, `DOMAIN`), mentor cho 2 nhóm EXE201 (`MENTOR_INTEREST`, `DOMAIN`) | W19, W28, S25, L04 |
| Dự án Bình Minh, Sao Mai | Tin nhắn, câu hỏi cho cố vấn đã trả lời, việc, file, bài nộp chờ sửa, họp chờ chốt, họp đã có biên bản; Bình Minh có Checkpoint 1 | C01–C08, L05 |
| Dự án Ánh Dương | Lịch họp hằng tuần 8 slot đã đăng, nhóm đã chọn slot (Tiên Phong chưa chọn), buổi mentor chờ chốt | C07, C09 |

File `sinh-vien-exe101-seed.xlsx`, `sinh-vien-exe201-seed.xlsx` theo mẫu import W07; `bao-cao-demo.pdf` là file nhỏ để thử tải lên.
