# Flutter Handoff – EXE Team Matching System (Mobile)

> Artifact cho **Slide 12 – Flutter Handoff** (PRM323 Lab 2). Lab 2 không yêu cầu viết code Flutter; file này là spec để một lập trình viên Flutter chưa biết dự án dựng được màn hình và các trạng thái.
> Nguồn: [DESIGN.md](DESIGN.md) (token, component, mục 12), [user-flow.md](user-flow.md) (flow, điều hướng), `EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx` (nội dung và trạng thái từng màn), `PROJECT_CONTEXT.md` mục 3 (quy tắc nghiệp vụ).
> Figma: page `03 Final UI` (frame 360 × 800, bản kiểm tra 412 × 915) và `06 Prototype`. Cập nhật: 19/10/2026.

**Phạm vi.** Spec đầy đủ 6 mục cho **18 màn hình và overlay nằm trong 3 flow** (mục 4). Các màn còn lại (S02–S04, S10–S14, S16, S19–S26, S30, S31, L02, L05, L06, A01, A02, A07) dùng cùng quy ước; nội dung và trạng thái của chúng nằm trong file Excel danh sách màn hình.

---

## 1. Bảng 1 – Token → Flutter

| Token (Figma variables) | Flutter |
|---|---|
| `color/primary` | `ColorScheme.primary` |
| Trắng trên Primary | `ColorScheme.onPrimary` |
| `color/primary-container` | `ColorScheme.primaryContainer` |
| `color/primary-dark` | `ColorScheme.onPrimaryContainer` |
| `color/surface` | `ColorScheme.surface` |
| `color/background` | `ThemeData.scaffoldBackgroundColor` |
| `color/text-primary` | `ColorScheme.onSurface` |
| `color/text-secondary` | `ColorScheme.onSurfaceVariant` |
| `color/border-strong` | `ColorScheme.outline` |
| `color/border` | `ColorScheme.outlineVariant` |
| `color/error`, `color/error-container` | `ColorScheme.error`, `ColorScheme.errorContainer` |
| `color/scrim` | `ColorScheme.scrim` |
| `color/sky`, `color/primary-border`, nhóm success / warning / neutral, `color/text-disabled` | `ThemeExtension<AppColors>` |
| `type/display` … `type/caption` | `TextTheme`: `headlineSmall`, `titleLarge`, `titleMedium`, `bodyLarge`, `bodyMedium`, `labelLarge`, `bodySmall`, `labelSmall` |
| `space/*`, `radius/*` | Hằng số trong `AppSpacing`, `AppRadius` |
| `elevation/0–2` | `elevation: 0`, `1`, `3`, `shadowColor` = Text Primary |
| `motion/*` | Hằng số `Duration` trong `AppMotion` |

`ThemeData(useMaterial3: true)` với `ColorScheme` khai báo tay theo bảng (không dùng `fromSeed`). Font `GoogleFonts.beVietnamProTextTheme()`. Không viết `Color(0x…)` hay `TextStyle(fontSize: …)` trong màn hình; luôn lấy qua `Theme.of(context)`.

## 2. Bảng 2 – Component → widget

| Component Figma | Flutter |
|---|---|
| `Button/Primary`, `Button/Secondary`, nút chữ | `FilledButton`, `OutlinedButton` (`OutlinedButton.icon` khi có icon), `TextButton` |
| Nút chỉ có icon | `IconButton` kèm `tooltip` |
| App bar, tab bar | `AppBar`, `NavigationBar` |
| Thẻ, thẻ nhóm, thẻ sinh viên, thẻ số liệu | `Card.outlined` |
| Dòng điều hướng, dòng thành viên | `ListTile` trong `Card.outlined` |
| Chip domain, kỹ năng, trạng thái | `Chip` |
| Chip lọc | `FilterChip` |
| Ô nhập | `TextField` + `OutlineInputBorder` bo 12, `errorText`, `helperText` |
| Bottom sheet | `showModalBottomSheet(isScrollControlled: true)`; nội dung trong `Flexible` cuộn được, thanh nút ngoài vùng cuộn, bọc `SafeArea` |
| Dialog xác nhận | `AlertDialog` |
| Thông báo nhanh (chỉ khi thành công) | `SnackBar` |
| Hàng tên nhóm + chip trạng thái | `Wrap(alignment: WrapAlignment.spaceBetween)` |
| Skeleton | `Container` nền Neutral Container, hoặc gói `skeletonizer` |
| Kéo để làm mới | `RefreshIndicator` |
| Màn trống, màn lỗi, banner hạn chót, thanh điều kiện nhóm | Widget riêng: `EmptyState`, `ErrorState`, `DeadlineBanner`, `TeamRequirementBar` |

## 3. Quy ước chung cho mọi màn hình

- **Frame:** thiết kế ở 360 × 800, kiểm tra ở 412 × 915. Lề ngang 16, lưới 4 px. Dùng `minHeight` thay `height` cho thẻ và nút; không cố định `textScaler`.
- **Accessibility:** vùng chạm ≥ 48 × 48 (`MaterialTapTargetSize.padded`); chữ nội dung ≥ 14 sp; trạng thái luôn có chữ + icon, không chỉ màu; widget tự dựng bấm được bọc `Semantics`; đọc `MediaQuery.disableAnimationsOf(context)` để giảm chuyển động.
- **Đang tải:** skeleton đúng hình nội dung; nút đang gửi thay chữ bằng vòng xoay 20 px, giữ chiều rộng, khóa bấm lần hai.
- **Lỗi:** lỗi tải cả màn → `ErrorState` có nút "Thử lại"; lỗi khi gửi → banner lỗi nền Error Container có "Thử lại", giữ nguyên nội dung đã nhập. Không dùng `SnackBar` cho lỗi.
- **Nút vô hiệu** luôn kèm một dòng nêu lý do.
- **Điều hướng:** màn cấp 1 (tab) không có Back; màn cấp 2 Back về đúng màn cha; sau khi gửi thành công, Back không quay lại form đã gửi.
- **Một nút Primary** mỗi màn hình.
- **Dữ liệu cá nhân:** SĐT chỉ hiện với đồng đội, giảng viên được phân công và Bộ môn.

---

## 4. Spec từng màn hình

### F1 – Sinh viên tham gia nhóm

#### A03 · Đăng nhập Google

| Mục | Nội dung |
|---|---|
| Layout | Logo và tên ứng dụng ở nửa trên; nút "Đăng nhập với Google" rộng hết chiều ngang ở nửa dưới; ghi chú "Dùng email đã đăng ký với bộ môn" bên dưới nút |
| Components | `FilledButton` có icon Google, `Text` (bodyMedium, onSurfaceVariant) |
| States | Mặc định · đang xử lý (nút đang gửi) · lỗi mạng (banner lỗi + Thử lại) · người dùng hủy đăng nhập (về mặc định, không báo lỗi) |
| Interactions | Bấm nút → Google Sign-In → gửi ID token lên Identity Service → nhận JWT của hệ thống |
| Navigation | Điểm vào. Email không có trong danh sách → A04. Sinh viên lần đầu → A05. Sinh viên đã có hồ sơ → S01. Giảng viên → L01 |
| UI constraints | Chỉ email trong danh sách học kỳ được vào (quy tắc 5). Không lưu mật khẩu trong ứng dụng |

#### A04 · Tài khoản chưa có trong danh sách (nhánh lỗi E1)

| Mục | Nội dung |
|---|---|
| Layout | `ErrorState`: icon, tiêu đề, mô tả lý do và cách liên hệ bộ môn; nút đăng nhập bằng tài khoản khác |
| Components | `ErrorState`, `FilledButton` |
| States | Mặc định |
| Interactions | Bấm nút → đăng xuất Google → về A03 |
| Navigation | Từ A03; nút → A03. Không có ngõ cụt |
| UI constraints | Hiện email vừa dùng để người dùng nhận ra nhầm tài khoản |

#### A05 · Hoàn thiện hồ sơ lần đầu

| Mục | Nội dung |
|---|---|
| Layout | Thanh bước "Bước 1/2", "Bước 2/2". Bước 1: thông tin đã import (họ tên, MSSV, ngành, chuyên ngành, mã lớp) chỉ đọc. Bước 2: SĐT/Zalo (không bắt buộc), kỹ năng dạng chip, giới thiệu ngắn; nút chính cuối trang |
| Components | `AppBar` cấp 2, `ListTile` chỉ đọc, `TextField` (SĐT, giới thiệu), `FilterChip` (kỹ năng chọn từ danh mục), `FilledButton` |
| States | Bước 1 · bước 2 · lỗi nhập SĐT (`errorText`, icon lỗi cuối ô) · đang lưu |
| Interactions | "Tiếp tục" → bước 2; "Hoàn tất" → kiểm tra SĐT → lưu hồ sơ |
| Navigation | Từ A03 (lần đầu). Lưu xong → S01, Back không quay lại A05 |
| UI constraints | SĐT lưu dạng chuỗi, giữ số 0 đầu (quy tắc 11). Chuyên ngành và kỹ năng chọn từ danh mục, không gõ tự do (quy tắc 12) |

#### S01 · Trang chủ sinh viên

| Mục | Nội dung |
|---|---|
| Layout | Lời chào (Display) + dòng môn · kỳ · lớp; `DeadlineBanner`; thẻ việc cần làm ngay hoặc thẻ trạng thái nhóm có `TeamRequirementBar`; thẻ lời mời (dòng điều hướng); "Nhóm phù hợp với bạn"; tab bar 5 mục |
| Components | `DeadlineBanner`, `Card.outlined`, `ListTile`, `TeamRequirementBar`, `FilledButton`, `NavigationBar` |
| States | Chưa có nhóm · là thành viên · là trưởng nhóm (thẻ việc gấp có nút "Mời người thay thế") · đợt đã khóa · được ghép tự động · đang tải. Banner hạn chót 4 mức: > 72 giờ, ≤ 72 giờ, ≤ 24 giờ, đã khóa |
| Interactions | Kéo để làm mới; chạm thẻ lời mời → S12; chạm nhóm gợi ý → S08; nút chính theo trạng thái |
| Navigation | Tab cấp 1. Đi tới S05, S07, S08, S12, S15, S17 |
| UI constraints | Banner hạn chót là thông tin tĩnh, không bấm được. Mức phù hợp chỉ là tham khảo, luôn kèm lý do |

#### S05 · Khám phá – Nhóm đang tuyển

| Mục | Nội dung |
|---|---|
| Layout | Tab trên: "Nhóm đang tuyển" / "Sinh viên tìm nhóm" (S10); ô tìm kiếm; hàng chip bộ lọc đang áp dụng; khu "Gợi ý cho bạn"; danh sách thẻ nhóm |
| Components | `TabBar`, `SearchBar`/`TextField`, `FilterChip`, `Card.outlined` (thẻ nhóm), `RefreshIndicator` |
| States | Có dữ liệu · không tìm thấy (`EmptyState` + "Xóa bộ lọc") · đang tải (skeleton) · lỗi tải (`ErrorState`) |
| Interactions | Gõ tìm kiếm; chạm "Bộ lọc" → S06; chạm thẻ → S08; kéo để làm mới; danh sách phân trang |
| Navigation | Tab cấp 1. → S06, S08, S10 |
| UI constraints | Thẻ người ngoài xem không có chip trạng thái, ghi "n/6 thành viên · còn k chỗ"; nhóm 6/6 không xuất hiện |

#### S06 · Bộ lọc và sắp xếp (bottom sheet)

| Mục | Nội dung |
|---|---|
| Layout | Thanh kéo; nhóm chip Domain, Chuyên ngành đang thiếu, Số chỗ trống, Mã lớp, Sắp xếp; thanh nút cố định cuối sheet: "Xóa tất cả" / "Áp dụng (n)" |
| Components | `showModalBottomSheet`, `FilterChip` (có dấu tích khi chọn), `DropdownMenu` (mã lớp), `TextButton`, `FilledButton` |
| States | Chưa chọn · đã chọn |
| Interactions | Chọn chip; "Áp dụng" → đóng sheet, lọc S05; "Xóa tất cả" → bỏ hết chip; kéo xuống, chạm lớp phủ hoặc nút đóng để thoát |
| Navigation | Overlay trên S05; đóng → S05 |
| UI constraints | Sheet cao tối đa màn hình trừ 48 px; thanh nút luôn nhìn thấy, phía trên vùng điều hướng hệ thống |

#### S08 · Chi tiết nhóm (xem từ bên ngoài)

| Mục | Nội dung |
|---|---|
| Layout | Tên nhóm + chip trạng thái cùng hàng; chip domain; mô tả ý tưởng; `TeamRequirementBar` dạng đầy đủ; danh sách thành viên kèm chuyên ngành; bài tuyển; nút chính "Xin tham gia" cuối trang |
| Components | `AppBar` cấp 2, `Wrap`, `Chip`, `ListTile`, `TeamRequirementBar`, `FilledButton` |
| States | Mặc định · đã gửi yêu cầu (còn 72 giờ) · nhóm đã đủ (nút khóa + lý do) · đợt đã khóa · bạn đã có nhóm · xem từ lời mời (Chấp nhận / Từ chối) |
| Interactions | "Xin tham gia" → mở S09 |
| Navigation | Từ S05, S07, S01; Back về màn cha |
| UI constraints | Không hiện SĐT thành viên với người ngoài nhóm. Mỗi sinh viên chỉ thuộc 1 nhóm (quy tắc 4) |

#### S09 · Gửi yêu cầu tham gia (bottom sheet)

| Mục | Nội dung |
|---|---|
| Layout | Tiêu đề, ô lời nhắn cho trưởng nhóm, dòng nhắc hiệu lực 72 giờ, thanh nút cố định "Hủy" / "Xin tham gia" |
| Components | `showModalBottomSheet`, `TextField` nhiều dòng, `TextButton`, `FilledButton` |
| States | Mặc định · đang gửi · gửi thành công · lỗi gửi (banner lỗi, giữ lời nhắn, Thử lại) |
| Interactions | "Xin tham gia" → nút đang gửi → thành công thì đóng sheet, S08 chuyển sang "đã gửi yêu cầu" và hiện `SnackBar` "Đã gửi yêu cầu" |
| Navigation | Overlay trên S08; đóng → S08 |
| UI constraints | Yêu cầu hết hạn sau 72 giờ (quy tắc 8). Lỗi không đóng sheet và không xóa lời nhắn |

#### A06 · Thông báo

| Mục | Nội dung |
|---|---|
| Layout | Danh sách theo ngày, mục chưa đọc có chấm; nút "Đánh dấu đã đọc tất cả" trên app bar |
| Components | `ListTile`, `Badge`, `RefreshIndicator`, `IconButton` |
| States | Có dữ liệu · trống · đang tải · lỗi tải · đã đọc tất cả |
| Interactions | Chạm thông báo → đánh dấu đã đọc, mở màn liên quan; kéo để làm mới (gọi `GET /api/notifications`) |
| Navigation | Tab cấp 1 (cả sinh viên và giảng viên). → S15, S29, L03…; Back về A06 |
| UI constraints | Không có push realtime; dữ liệu cập nhật khi mở tab hoặc kéo để làm mới. Trong prototype, chạm thông báo mô phỏng phản hồi của bên thứ hai |

#### S15 · Nhóm của tôi – Tổng quan

| Mục | Nội dung |
|---|---|
| Layout | Tên nhóm + chip trạng thái (Nháp / Hoạt động / Cần xem lại / Đã chốt); `TeamRequirementBar` dạng đầy đủ; giảng viên; khối "Thay đổi đang chờ duyệt" (EXE201); lối tắt Thành viên, Yêu cầu, Bài tuyển |
| Components | `Wrap`, `Chip`, `TeamRequirementBar`, `Card.outlined`, `ListTile` |
| States | Trưởng nhóm · thành viên · đủ điều kiện · chưa đủ điều kiện · có thay đổi chờ duyệt · cần xem lại (sau hạn) · đã chốt · chưa có nhóm (`EmptyState`) |
| Interactions | Chạm lối tắt → màn con; trưởng nhóm có thay đổi chờ → nút chính "Gửi yêu cầu đổi nhóm" → S27 |
| Navigation | Tab cấp 1. Điểm kết thúc của F1 và F2. → S17, S18, S20, S23, S26, S27 |
| UI constraints | Nhóm EXE201 kế thừa: tách "thành viên hiện tại" và "thay đổi đang chờ duyệt" (quy tắc 7) |

### F2 – Trưởng nhóm EXE201 đổi thành viên

#### S17 · Thành viên nhóm

| Mục | Nội dung |
|---|---|
| Layout | Danh sách dòng thành viên (avatar, họ tên, nhãn "Trưởng nhóm", MSSV · chuyên ngành, liên hệ); khối thay đổi chờ Bộ môn duyệt |
| Components | `ListTile`, `Chip` trạng thái ("Rời nhóm", "Thành viên mới"), `PopupMenuButton`, `AlertDialog` |
| States | Trưởng nhóm · thành viên · dialog xóa / rời / chuyển quyền · thay đổi chờ Bộ môn duyệt |
| Interactions | Trưởng nhóm: menu → xóa thành viên → `AlertDialog` (nút hành động màu Error) → thành viên chuyển sang "Rời nhóm · chờ duyệt" |
| Navigation | Từ S15, S01; → S18 để mời người thay thế |
| UI constraints | Với nhóm kế thừa, xóa chưa có hiệu lực cho tới khi Bộ môn duyệt. Dòng phụ tối đa 2 dòng, không lặp chữ của chip |

#### S18 · Gợi ý thành viên

| Mục | Nội dung |
|---|---|
| Layout | Dòng "Nhóm bạn đang thiếu: …"; danh sách thẻ sinh viên do Matching Service gợi ý, mỗi thẻ có lý do và nút phụ "Gửi lời mời" |
| Components | `Card.outlined` (thẻ sinh viên), `Chip` kỹ năng, `OutlinedButton` |
| States | Có gợi ý · không có gợi ý · đã mời · đang tải |
| Interactions | "Gửi lời mời" → nút đang gửi → thẻ chuyển "Đã mời", `SnackBar` "Đã gửi lời mời"; chạm thẻ → hồ sơ S04 |
| Navigation | Từ S17, S15, S01; nhánh thay thế → S19 tìm theo MSSV |
| UI constraints | Chỉ trưởng nhóm thấy nút mời. Chỉ gợi ý sinh viên chưa có nhóm. Lời mời hết hạn sau 72 giờ |

#### S27 · Tạo yêu cầu đổi nhóm – chọn thay đổi

| Mục | Nội dung |
|---|---|
| Layout | Thanh bước "Bước 1/2"; so sánh nhóm mốc EXE101 với các thay đổi đang chờ (rời nhóm, thành viên mới); cảnh báo cơ cấu nhóm; nút chính "Tiếp tục" |
| Components | `ListTile` + `Chip`, `Checkbox`, banner cảnh báo, `FilledButton` |
| States | Chưa thay đổi (nút khóa + lý do) · có thay đổi · nhóm còn dưới 4 người (chặn) · thiếu chuyên ngành thứ hai (chỉ cảnh báo) |
| Interactions | Chọn các thay đổi đưa vào yêu cầu → "Tiếp tục" → S28 |
| Navigation | Từ S15, S26; → S28; Back về S15 |
| UI constraints | Chỉ EXE201, chỉ trưởng nhóm. Không giới hạn số người thay đổi (quy tắc 7). Thiếu chuyên ngành thứ hai chỉ cảnh báo vì quy tắc 2 chặn lúc chốt nhóm |

#### S28 · Tạo yêu cầu đổi nhóm – lý do và xác nhận

| Mục | Nội dung |
|---|---|
| Layout | Thanh bước "Bước 2/2"; 2 thẻ số liệu cao bằng nhau (Thành viên thay đổi, Tỷ lệ thay đổi); "Xem lại thay đổi"; ô "Lý do thay đổi" nhiều dòng + dòng hỗ trợ; nút chính "Gửi yêu cầu đổi nhóm" cuối trang |
| Components | `AppBar` cấp 2, `Card.outlined` (thẻ số liệu), `ListTile` + `Chip`, `TextField` (`errorText`, `helperText`), `FilledButton`, `AlertDialog` |
| States | Mặc định · thiếu lý do · dialog xác nhận gửi · đang gửi · lỗi mạng (giữ nội dung) · gửi thành công |
| Interactions | Bấm Gửi → kiểm tra lý do (thiếu thì lỗi tại ô, không mở dialog) → `AlertDialog` "Gửi yêu cầu đổi nhóm?" → nút đang gửi → kết quả |
| Navigation | Từ S27; thành công → S29, Back không quay lại form; lỗi → ở lại S28 |
| UI constraints | Lý do bắt buộc. Số lượng và tỷ lệ % do hệ thống tự tính, không cho sửa. Lỗi không dùng `SnackBar` |

#### S29 · Chi tiết yêu cầu đổi nhóm

| Mục | Nội dung |
|---|---|
| Layout | Dòng thời gian: Đã gửi (người gửi, giờ) → Chờ Bộ môn duyệt → Kết quả; 2 thẻ số liệu; chip trạng thái; danh sách thay đổi; ghi chú của Bộ môn khi bị từ chối |
| Components | `Stepper` hoặc dòng thời gian tự dựng (bọc `Semantics`), `Card.outlined`, `Chip`, `ListTile`, `FilledButton` |
| States | Chờ duyệt · đã duyệt · đã từ chối (có nút "Tạo yêu cầu mới") |
| Interactions | Kéo để làm mới trạng thái; bị từ chối → "Tạo yêu cầu mới" → S27 |
| Navigation | Từ S28, S26, A06; đã duyệt → S15 / Đã duyệt đổi thành viên |
| UI constraints | Mỗi bước có mốc thời gian và người duyệt (quy tắc 10). Bị từ chối thì nhóm giữ nguyên như mốc |

### F3 – Giảng viên phản hồi yêu cầu hướng dẫn

#### L01 · Trang chủ giảng viên

| Mục | Nội dung |
|---|---|
| Layout | Lời chào; `DeadlineBanner` hạn phản hồi; 2 thẻ số liệu (Yêu cầu đang chờ, Đang hướng dẫn "6/8" + thanh tiến độ); "Yêu cầu mới nhất" dạng dòng điều hướng + "Xem tất cả"; tab bar 4 mục |
| Components | `DeadlineBanner`, `Card.outlined`, `LinearProgressIndicator`, `ListTile`, `NavigationBar` |
| States | Có yêu cầu chờ · không có (`EmptyState`) · đã đủ sức chứa (banner + chip "Đã đủ") · đang tải |
| Interactions | Chạm yêu cầu → L03; "Xem tất cả" → L02; icon tài khoản → L06 |
| Navigation | Tab cấp 1, điểm bắt đầu F3 |
| UI constraints | Toàn bộ nội dung nằm trong màn hình đầu, không phải cuộn mới thấy yêu cầu |

#### L03 · Chi tiết yêu cầu hướng dẫn

| Mục | Nội dung |
|---|---|
| Layout | Tên nhóm + chip trạng thái; ý tưởng, domain, thành viên, lời nhắn của nhóm; dòng sức chứa "Đã nhận 6/8 nhóm"; hai nút cuối trang "Từ chối" (phụ) / "Chấp nhận" (chính) |
| Components | `Wrap`, `Chip`, `ListTile`, `OutlinedButton`, `FilledButton`, `AlertDialog`, `showModalBottomSheet` |
| States | Mặc định · dialog xác nhận chấp nhận · bottom sheet từ chối kèm lý do · đang gửi · đã đủ sức chứa (khóa Chấp nhận + lý do) · yêu cầu không còn hiệu lực |
| Interactions | Chấp nhận → `AlertDialog` → đang gửi → L04. Từ chối → bottom sheet nhập lý do → gửi → L02 |
| Navigation | Từ L01, L02, A06; thành công → L04; Back về màn cha |
| UI constraints | Giảng viên đủ sức chứa không chấp nhận được, vẫn từ chối được (quy tắc 9). Chấp nhận chỉ là tham khảo; Bộ môn phân công chính thức |

#### L04 · Nhóm tôi hướng dẫn

| Mục | Nội dung |
|---|---|
| Layout | Hai mục "Tôi đã nhận" (tham khảo) và "Bộ môn đã phân công" (chính thức); lọc theo môn và mã lớp; tìm kiếm |
| Components | `SegmentedButton` hoặc tiêu đề mục, `FilterChip`, `Card.outlined` (thẻ nhóm) |
| States | Có dữ liệu · trống · đang tải |
| Interactions | Chạm thẻ nhóm → L05; lọc, tìm kiếm |
| Navigation | Tab cấp 1, điểm kết thúc F3 |
| UI constraints | Phân biệt rõ tham khảo và chính thức bằng tiêu đề mục và chip, không chỉ bằng màu |

---

## 5. Checklist khi hiện thực một màn hình

- [ ] Mọi màu, chữ, khoảng cách lấy từ `Theme.of(context)` / `AppSpacing` / `AppRadius`.
- [ ] Đủ các trạng thái ghi ở mục "States", kể cả đang tải, trống và lỗi.
- [ ] Kiểm tra ở 360 và 412 dp, cỡ chữ hệ thống 130% không vỡ bố cục.
- [ ] Vùng chạm ≥ 48, chữ nội dung ≥ 14 sp, trạng thái có chữ + icon.
- [ ] Back đúng màn cha; sau khi gửi thành công không quay lại form.
- [ ] Đối chiếu frame tương ứng trong Figma `03 Final UI` và luồng trong `06 Prototype`.
