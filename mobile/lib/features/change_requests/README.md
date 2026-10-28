# MB-29 / MB-30 — trạng thái triển khai cục bộ

Có màn S26–S29 và API client theo `docs/contracts/openapi/team.yaml`.
`routes.dart` xuất route module; chưa được đăng ký vào router chung.

Đã kiểm tra: 15 widget tests cho lọc S26, chặn tạo yêu cầu khi đợt đóng ở 360/412 dp, chọn thay đổi, tính số thành viên và chuyên ngành theo mục đã chọn, gửi thành công và lỗi API giữ nội dung.
APK debug thất bại ở cache Kotlin của plugin `url_launcher_android`.

Chưa nghiệm thu task:
- Tích hợp API BE-45, nhóm của tôi và thông tin học kỳ thực.
- Nối route trong shell sinh viên bởi chủ mảng router.
- Đối chiếu tỷ lệ thay đổi với backend khi chạy API thật.
- Kiểm tra toàn bộ luồng gửi thành công, lỗi mạng và quay lại với API mock/real; kiểm tra bố cục form dài và Figma.

Chỉ giữ nhánh cục bộ; chưa cập nhật Sheet thành Xong.
