# Báo cáo kiểm thử

## Checklist web admin cho Final (FE-48)

Đối chiếu 5 màn đề yêu cầu với màn đã có trên web. Lần đầu kiểm tra ngày 04/11/2026 trên Docker Compose local. Sau FE-49 (giao diện mới), **chạy lại ngày 05/11/2026 trên bản Hosting** `https://exe-team-matching.web.app`, backend Docker trên máy, theo `docs/setup/deploy.md`; mọi ảnh dưới đây là ảnh của lần chạy lại.

**Chưa xong:** khi BE-85 lên Railway phải chạy lại bảng này trên bản đó.

### Cách kiểm tra

- Dữ liệu: seed BE-57 (học kỳ `SP90`) trên DB local đã có dữ liệu cũ của collection demo.
- Đăng nhập: lấy phiên bằng `POST /api/dev/token`, vì email seed không đăng nhập Google được.
- Thao tác chính gọi API thật, trừ W17 và W18 ở dưới. Riêng trạng thái **trống** và **lỗi tải** là giả lập: trình duyệt headless chặn request của màn đó và trả dữ liệu rỗng hoặc lỗi 500.
- **W17, W18 lần chạy lại là giả lập:** seed hiện không còn yêu cầu `PENDING`, nên lấy một yêu cầu thật, đổi trạng thái thành chờ duyệt ở tầng trình duyệt, và giả phản hồi của `approve`, `reject`. Lần này chỉ xác nhận giao diện mới; duyệt, từ chối qua API thật đã kiểm tra ngày 04/11 và FE-49 không đổi logic.

### Kết quả

| Yêu cầu | Màn | Trường hợp | Kết quả | Ảnh |
|---|---|---|---|---|
| Đăng nhập, từ chối sai vai trò | W01 | Mở trang đăng nhập | Đạt | [w01-dang-nhap](../evidence/web/w01-dang-nhap.png) |
| | | Chưa đăng nhập mở `/admin` thì về `/dang-nhap` | Đạt | [w01-chua-dang-nhap](../evidence/web/w01-chua-dang-nhap.png) |
| | | Sinh viên mở `/admin` thấy "Bạn không có quyền xem trang này" | Đạt | [w01-sai-vai-tro](../evidence/web/w01-sai-vai-tro.png) |
| | | Đăng nhập Google thật, email không có trong danh sách | **Chưa kiểm tra** | – |
| Quản lý một thực thể | W11 | Danh sách giảng viên | Đạt | [w11-danh-sach](../evidence/web/w11-danh-sach.png) |
| | | Thêm giảng viên, đọc lại qua API thấy bản ghi mới | Đạt | [w11-them-form](../evidence/web/w11-them-form.png), [w11-them](../evidence/web/w11-them.png) |
| | | Trống | Đạt | [w11-trong](../evidence/web/w11-trong.png) |
| | | Lỗi tải | Đạt | [w11-loi](../evidence/web/w11-loi.png) |
| Hàng chờ duyệt | W17 | Danh sách chờ duyệt | Đạt | [w17-cho-duyet](../evidence/web/w17-cho-duyet.png) |
| | | Trống | Đạt | [w17-trong](../evidence/web/w17-trong.png) |
| | | Lỗi tải | Đạt | [w17-loi](../evidence/web/w17-loi.png) |
| | W18 | Chi tiết: so sánh mốc và thành viên sau thay đổi | Đạt | [w18-chi-tiet](../evidence/web/w18-chi-tiet.png) |
| | | Duyệt, yêu cầu chuyển sang tab Đã duyệt (giả lập, xem trên) | Đạt | [w18-duyet-dialog](../evidence/web/w18-duyet-dialog.png), [w18-da-duyet](../evidence/web/w18-da-duyet.png) |
| | | Từ chối bỏ trống ghi chú bị chặn | Đạt | [w18-tu-choi-dialog](../evidence/web/w18-tu-choi-dialog.png), [w18-tu-choi-thieu-ghi-chu](../evidence/web/w18-tu-choi-thieu-ghi-chu.png) |
| | | Từ chối có ghi chú, chuyển sang tab Đã từ chối (giả lập, xem trên) | Đạt | [w18-da-tu-choi](../evidence/web/w18-da-tu-choi.png) |
| | | Lỗi tải | Đạt | [w18-loi](../evidence/web/w18-loi.png) |
| Dashboard ≥ 3 số liệu, 1 biểu đồ | W02 | 4 số liệu, 3 biểu đồ thanh | Đạt | [w02-dashboard](../evidence/web/w02-dashboard.png) |
| | | Trống | Đạt | [w02-trong](../evidence/web/w02-trong.png) |
| | | Lỗi tải | Đạt | [w02-loi](../evidence/web/w02-loi.png) |
| Form thông báo | W23 | Mở form | Đạt sau sửa lỗi 1 | [w23-form](../evidence/web/w23-form.png) |
| | | Thiếu tiêu đề bị chặn | Đạt | [w23-thieu-tieu-de](../evidence/web/w23-thieu-tieu-de.png) |
| | | Gửi cho mọi người, hiện số người nhận | Đạt sau sửa lỗi 2 | [w23-da-gui](../evidence/web/w23-da-gui.png) |
| | | Gửi lỗi | Đạt | [w23-loi](../evidence/web/w23-loi.png) |

W23 là form nên không có trạng thái trống.

### Lỗi tìm thấy

| # | Màn | Lỗi | Trạng thái |
|---|---|---|---|
| 1 | W23 | Có màn hình nhưng chưa gắn route trong `featureRoutes.tsx`, web chỉ hiện "Màn hình đang được làm" | Đã sửa |
| 2 | W23 | Gửi "Tất cả mọi người" trả 500 khi có giảng viên kiêm mentor: 2 contact cùng email vi phạm `uq_email_logs_event_to` | Đã sửa, có test |
| 3 | W23 | "Sinh viên chưa có nhóm" và "Nhóm chưa có mentor" luôn trả 0 người nhận, vì chưa có API nội bộ từ Team Service | Chưa sửa, cần Thành xem |
| 4 | W23 | Chọn "Khóa học / môn" không có tác dụng: `SendAnnouncementAsync` không lọc theo `courseCode` | Chưa sửa, cần Thành xem |

## Xuất báo cáo PDF (MB-53)

Thử ngày 05/11/2026 trên Docker Compose local, SeaweedFS thật, gọi thẳng Collaboration Service (cổng 5007) bằng JWT dev. Quy tắc phân quyền: [file-storage.md](file-storage.md).

| Trường hợp | Kết quả mong đợi | Thực tế |
|---|---|---|
| Chủ báo cáo tải lên PDF | 201, có link ký sẵn | Đạt |
| Mở link ký sẵn | 200, đúng nội dung, tên file tải về | Đạt |
| Chủ báo cáo xem danh sách, xin link mới | 200 | Đạt |
| Người khác xem danh sách, xin link, ghi vào `reports/{uid}/` | 403 `not-report-owner` | Đạt |
| Không có JWT | 401 | Đạt |
| Gọi qua Gateway: chủ báo cáo, người khác | 200, 403 | Đạt |
| File không phải PDF | 415 | Đạt |
| Báo cáo không tồn tại | 404 | Đạt |
| Mở URL S3 không có chữ ký | 403 | Đạt |
| Sửa chữ ký trên link | 403 | Đạt |
| Đổi đường dẫn sang người khác, giữ chữ ký | 403 | Đạt |
| Mất mạng khi tải lên | App báo "Mất kết nối…", có nút Thử lại | Đạt ở test ViewModel và widget, **chưa thử trên máy thật** |

**Chưa kiểm tra:** chưa bấm nút trên điện thoại thật, chưa mở PDF đã tạo bằng trình đọc PDF trên máy.
