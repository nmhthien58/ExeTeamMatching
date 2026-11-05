# Kế hoạch kiểm thử E2E (MB-58)

> PRM393 Lab 3 §7, Phụ lục B; Final §9. Công cụ: [Google ARTEMIS](https://github.com/google/artemis), không dùng Patrol. Mỗi ca là một hàm pytest trong `e2e_test/`, gửi một câu lệnh qua `ArtemisClient` và kiểm tra giá trị agent báo về.
>
> **Trạng thái:** kế hoạch viết trước khi có kết quả. Cột Kết quả ở mục 5 còn trống cho tới khi chạy trên máy thật. Bản này chưa đối chiếu trực tiếp với file đề PDF; nội dung lấy theo `docs/task-changes-2026-11-04.md`.

## 1. Môi trường

| Mục | Giá trị |
|---|---|
| Thiết bị | Điện thoại Android thật, cắm cáp, bật USB debugging |
| Backend | Docker Compose trên laptop, dữ liệu seed `SP90` (`deploy/seed`) |
| Kết nối | `adb reverse tcp:8080 tcp:8080` |
| Bản app | `flutter build apk --debug --dart-define=API_BASE_URL=http://localhost:8080 --dart-define=FIREBASE_DEMO=true --dart-define=CRASHLYTICS_DEBUG=true` |
| Tài khoản | Sinh viên chưa có nhóm theo `SEED_STUDENT_EMAIL`; trưởng nhóm EXE201 và giảng viên theo seed. Đăng nhập Google làm tay một lần trước khi chạy, ARTEMIS không nhập mật khẩu |
| Model | Profile Flash cho hồi quy, Pro cho phiên khám phá. Key Gemini đặt trong `e2e_test/.env` (đã gitignore) |

## 2. Luồng quan trọng và màn hình

| Luồng | Màn hình | Ca |
|---|---|---|
| Đăng nhập, phiên | A01, A03, A04 | TC-01, TC-02 |
| F1 – Sinh viên tham gia nhóm | S01, S05, S07, S08, S09, S12 | TC-03, TC-04, TC-05 |
| F2 – Trưởng nhóm EXE201 đổi thành viên | S15, S26, S27, S28, S29 | TC-06, TC-07 |
| F3 – Giảng viên | L01, L04, L05, L07 | TC-08, TC-09 |
| Các tab của từng khung | S01, S05 / C01, S15, A06, S02 | TC-10 |
| Màn chi tiết | S08, S23, S28, L05 | TC-04, TC-11 |
| Xuất và tải file | C01, C06 | TC-12, TC-13 |
| Remote Config | S07, A07 | TC-14 |
| Android gốc | Nút Back hệ thống, hộp xin quyền thông báo | TC-15, TC-16 |
| Đăng xuất | A07 → A03 | TC-17 |
| Xuyên vai trò, bước mobile | S28, A06 | TC-18 |
| Tính năng AI | S09 | TC-19, TC-20 |

## 3. Danh sách ca

Câu lệnh chạy nằm trong `e2e_test/test_*.py`. Cột Phải thấy là giá trị agent phải báo về, không chỉ trạng thái đạt.

| Ca | Loại | Điểm bắt đầu | Các bước | Phải thấy ở cuối | Chờ |
|---|---|---|---|---|---|
| TC-01 | Chính | App đã đăng nhập, mở lại từ đầu | Đợi màn khởi động | Tab Trang chủ, không quay lại màn đăng nhập | – |
| TC-02 | Lỗi | Màn đăng nhập, tài khoản ngoài danh sách | Chọn tài khoản Google đó | Màn A04 "Tài khoản chưa có trong danh sách" | Cần tài khoản Google thứ hai |
| TC-03 | Chính | S01, sinh viên chưa có nhóm | Mở Khám phá, tìm một nhóm theo tên | Tên nhóm đã tìm trong danh sách và số kết quả | – |
| TC-04 | Chính | S05 | Mở nhóm đầu tiên | S08 hiện tên nhóm, số thành viên, đề tài | – |
| TC-05 | Lỗi | S09 | Để trống ý tưởng và không tích "Chưa có ý tưởng", bấm Gửi | Thông báo lỗi tại chỗ, yêu cầu chưa gửi | – |
| TC-06 | Chính | S15, trưởng nhóm EXE201 | Tạo yêu cầu đổi thành viên, lý do đủ 10 ký tự | S28 có trạng thái chờ duyệt | Cần đợt đổi nhóm đang mở |
| TC-07 | Lỗi | S27 | Nhập lý do dưới 10 ký tự, bấm Gửi | Lỗi tại ô lý do, nội dung còn nguyên | – |
| TC-08 | Chính | L01, giảng viên | Mở nhóm đầu tiên ở L04 | L05 hiện tên nhóm và mục Checkpoint | – |
| TC-09 | Lỗi | L05 | Đánh dấu Checkpoint với ngày tương lai | Bị chặn, kèm thông báo | – |
| TC-10 | Chính | S01 | Bấm lần lượt mọi tab | Tên từng tab và tiêu đề màn tương ứng | – |
| TC-11 | Chính | S23 | Mở một giảng viên trong danh sách | S24 hiện tên giảng viên và chuyên môn | – |
| TC-12 | Chính | C01 của nhóm có việc | Bấm "Xuất báo cáo PDF" | Tiến độ, rồi "Đã lưu báo cáo" và nút mở file | MB-53 |
| TC-13 | Lỗi | C01, tắt mạng trước khi xuất | Bấm "Xuất báo cáo PDF" | Thông báo lỗi tiếng Việt và nút thử lại, app không đóng | MB-53 |
| TC-14 | Chính | A07, bản `FIREBASE_DEMO` | Đọc mục Remote Config đang dùng | Số nhóm gợi ý tối đa khớp giá trị trên Console | – |
| TC-15 | Android gốc | S08 mở từ S05 | Bấm nút Back hệ thống | Quay về S05, danh sách vẫn giữ vị trí | – |
| TC-16 | Android gốc | Cài mới, Android 13+ | Mở app, chạm Cho phép ở hộp xin quyền thông báo | Hộp đóng, app vào màn chính | MB-52 |
| TC-17 | Chính | A07 | Bấm Đăng xuất, xác nhận | Màn đăng nhập A03 | – |
| TC-18 | Chính | Bộ môn vừa duyệt yêu cầu trên web | Mở A06 trên mobile | Thông báo duyệt, chạm vào mở S28 trạng thái Đã duyệt | MB-52, FE-47 |
| TC-19 | Chính | S09, cờ `ai_idea_enabled` bật | Bấm "Gợi ý bằng AI", nhập mô tả, dùng gợi ý | Ý tưởng điền sẵn có nhãn "Gợi ý của AI" | MB-55, MB-56 |
| TC-20 | Lỗi | S09, cờ `ai_idea_enabled` tắt | Mở phần ý tưởng | Không có nút AI, vẫn nhập tay được | MB-56 |

Tổng 20 ca, gồm 6 ca lỗi (TC-02, 05, 07, 09, 13, 20) và 2 ca Android gốc (TC-15, 16). Đề yêu cầu tối thiểu 10 ca, 1 ca lỗi, 1 tương tác Android gốc.

## 4. Cách chạy

```bash
cd e2e_test
cp .env.example .env
uv sync
uv run pytest --runs 3
```

Mỗi ca chạy 3 lần. Trace lưu ở `evidence/artemis/<ca>/<lần chạy>/`.

## 5. Kết quả

| Ca | Lần 1 | Lần 2 | Lần 3 | Tỷ lệ đạt | Thời gian trung bình | Nguyên nhân nếu dưới 3/3 và cách sửa |
|---|---|---|---|---|---|---|
| TC-01…TC-20 | chưa chạy | | | | | |

Nguyên nhân ghi theo một trong ba nhóm: app, timing, câu lệnh mơ hồ.

## 6. Phiên khám phá

Một phiên 15–30 phút, profile Pro, bắt đầu từ S01 với tài khoản sinh viên. Vấn đề tìm được ghi ở bảng dưới.

| # | Màn | Vấn đề | Mức độ | Xử lý |
|---|---|---|---|---|
| | | chưa chạy | | |
