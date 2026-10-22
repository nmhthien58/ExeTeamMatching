# Flutter Handoff – EXE Team Matching System (Mobile)

> Artifact cho **Slide 12 – Flutter Handoff** (PRM323 Lab 2). Lab 2 không yêu cầu viết code Flutter; file này là spec để một lập trình viên Flutter chưa biết dự án dựng được màn hình và các trạng thái.
> Nguồn: [DESIGN.md](DESIGN.md) (token, component, mục 12), [user-flow.md](user-flow.md) (flow, điều hướng), `EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx` (nội dung và trạng thái từng màn), `PROJECT_CONTEXT.md` mục 3 (quy tắc nghiệp vụ).
> Figma: page `03 Final UI` (frame 360 × 800, bản kiểm tra 412 × 915) và `06 Prototype`. Cập nhật: 19/10/2026.

**Phạm vi.** Spec đầy đủ 6 mục cho **18 màn hình và overlay nằm trong 3 flow** (mục 4). Các màn còn lại (S02–S04, S10–S14, S16, S19–S26, S30, S31, L02, L05, L06, A01, A02, A07) dùng cùng quy ước; nội dung và trạng thái của chúng nằm trong file Excel danh sách màn hình.


> **Cập nhật 22/10/2026:** token theo `DESIGN.md` **bản 5** – mã màu FPT theo sổ tay thương hiệu: cam FPT cho mọi thứ bấm được và đang chọn, xanh dương FPT chỉ cho thông tin (nhóm `info`), bỏ nhóm `secondary`.
> **Cập nhật 19/10/2026 (sau góp ý của giảng viên):** token theo `DESIGN.md` bản 4 (cam / xanh FPT); widget mới ở mục 2; A05 viết lại theo quy tắc 16; spec 6 mục cho màn hình mới và màn hình thay đổi ở **mục 4a**. Các màn trong 3 flow của Lab 2 vẫn khớp Figma, trừ phần màu và các chỗ ghi "(bản 4)".
---

## 1. Bảng 1 – Token → Flutter

| Token (Figma variables) | Flutter |
|---|---|
| `color/primary` (cam FPT đậm `#C6510B`) | `ColorScheme.primary` |
| Trắng trên Primary | `ColorScheme.onPrimary` |
| `color/primary-dark` `#A8440F` cho chữ cam (link, nút chữ, nút phụ) | `textButtonTheme` / `outlinedButtonTheme`: `foregroundColor` = Primary Dark; `ColorScheme.secondary` = Primary Dark |
| `color/info` (xanh dương FPT `#0066B3`), `color/info-container`, `color/info-dark` | `ThemeExtension<AppColors>`: `info`, `infoContainer`, `onInfoContainer` – chỉ cho chip domain, chip lý do ghép, trạng thái "Thông tin", banner thông tin |
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
| `color/brand-orange`, `color/brand-green`, `color/primary-border`, nhóm success / warning / neutral, `color/text-disabled` | `ThemeExtension<AppColors>` |
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
| Thẻ đề tài, tóm tắt cho mentor (bản 4) | `ProjectCard` + `ProjectTimeline` (mốc theo kỳ / môn), `MentorBriefCard`, `MentorSessionChip` ("Buổi mentor 1/3") |
| Thanh kéo kỹ năng (bản 4) | Widget riêng `SkillSlider` dựa trên `Slider(min: 1, max: 5, divisions: 4)`, giá trị `null` = chưa đánh giá; `semanticFormatterCallback` đọc "Công nghệ, 3 trên 5" |
| Biểu đồ kỹ năng (bản 4) | Widget riêng `SkillBars` (5 hàng × 5 ô), tham số `highlight` cho kỹ năng nhóm đang thiếu |
| Nhóm ý tưởng (bản 4) | Widget riêng `IdeaListField`: tối đa 3 cặp `TextField` + `CheckboxListTile` "Chưa có ý tưởng" |
| Hộp nhắc giai đoạn (bản 4) | `AlertDialog` qua hàm `showPhaseClosedDialog(context, phase)` |
| Tin nhắn (bản 4) | `MessageBubble`, `MessageComposer`, danh sách `ListView.builder(reverse: true)`; kết nối hub `/hubs/collab` bằng gói `signalr_netcore` |
| Bảng việc, dòng file, khung giờ họp (bản 4) | `TaskCard` (Card), `FileTile` (ListTile), `SlotVoteTile` với `SegmentedButton` Có / Không |
| Chỉ báo quan tâm, nhãn vai trò (bản 4) | `InterestButton` (OutlinedButton.icon ↔ FilledButton.tonal), `RoleChip` (Chip) |
| Nguyện vọng, lý do ghép (bổ sung 20/10) | `PreferenceList` (`ReorderableListView` 3 ô + nút lên/xuống cho người không kéo được), `MatchReasonChip` (Chip Info Container: "Hai bên cùng chọn", "Nguyện vọng 2"…) |
| Checkpoint, lịch họp hằng tuần (bổ sung 20/10) | `CheckpointProgress` (4 chấm + "Checkpoint 2/4", `Semantics` đọc đủ câu), `WeeklySlotGrid` (lưới ô slot, mỗi ô là nút có nhãn), `TimeRangeField` |

## 3. Quy ước chung cho mọi màn hình

- **Frame:** thiết kế ở 360 × 800, kiểm tra ở 412 × 915. Lề ngang 16, lưới 4 px. Dùng `minHeight` thay `height` cho thẻ và nút; không cố định `textScaler`.
- **Accessibility:** vùng chạm ≥ 48 × 48 (`MaterialTapTargetSize.padded`); chữ nội dung ≥ 14 sp; trạng thái luôn có chữ + icon, không chỉ màu; widget tự dựng bấm được bọc `Semantics`; đọc `MediaQuery.disableAnimationsOf(context)` để giảm chuyển động.
- **Đang tải:** skeleton đúng hình nội dung; nút đang gửi thay chữ bằng vòng xoay 20 px, giữ chiều rộng, khóa bấm lần hai.
- **Lỗi:** lỗi tải cả màn → `ErrorState` có nút "Thử lại"; lỗi khi gửi → banner lỗi nền Error Container có "Thử lại", giữ nguyên nội dung đã nhập. Không dùng `SnackBar` cho lỗi.
- **Nút vô hiệu** luôn kèm một dòng nêu lý do.
- **Điều hướng:** màn cấp 1 (tab) không có Back; màn cấp 2 Back về đúng màn cha; sau khi gửi thành công, Back không quay lại form đã gửi.
- **Một nút Primary tô đặc (cam)** mỗi màn hình; điều hướng, lựa chọn, link cũng là cam nhưng ở dạng viền, chữ Primary Dark hoặc nền Primary Container. Xanh dương FPT chỉ cho thông tin không bấm được.
- **Tab bar theo giai đoạn (bản 4):** shell đọc `phase` của môn từ Academic (`GET /api/semesters/current`) lúc khởi động và khi kéo để làm mới; giai đoạn học kỳ thì tab Khám phá đổi thành Dự án (C01). Route tới S05, S08, S10 kiểm tra `phase` trong `redirect` của go_router và mở `showPhaseClosedDialog` thay vì màn hình. API trả `409 round-closed` cũng mở hộp nhắc này.
- **Ít chữ (bản 4):** theo `DESIGN.md` mục 9.1 – tiêu đề 1–3 từ, nút ≤ 3 từ, thẻ tối đa một dòng mô tả.
- **Mẫu tương tác (bản 4):** theo `DESIGN.md` mục 6.4 – Hoàn tác cho thao tác đổi lại được, dialog cho thao tác không đổi lại được; cập nhật lạc quan cho gửi tin, đổi trạng thái việc, chọn khung giờ (Riverpod: cập nhật state trước, rollback khi API lỗi); kéo thả luôn có menu "Chuyển sang…"; badge giữ chỗ cố định, `Semantics(liveRegion: true)` một lần cho mỗi màn.
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

#### A05 · Hoàn thiện hồ sơ lần đầu (bản 4 – một màn)

| Mục | Nội dung |
|---|---|
| Layout | Khối "Thông tin từ bộ môn" thu gọn một dòng (họ tên · MSSV · chuyên ngành), chạm để mở; 5 `SkillSlider`; ô "Giới thiệu" 4 dòng; nút chính "Hoàn tất" cuối trang |
| Components | `AppBar` cấp 2, `ExpansionTile` chỉ đọc, `SkillSlider` ×5, `TextField` nhiều dòng (bộ đếm 500), `FilledButton` |
| States | Mặc định (5 thanh "Chưa đánh giá") · còn thanh chưa đánh giá (nút khóa + "Còn 2 kỹ năng chưa đánh giá") · đang lưu · lỗi lưu (banner lỗi, giữ dữ liệu) |
| Interactions | Kéo / chạm thanh → giá trị hiện "n/5"; "Hoàn tất" → lưu → S01 |
| Navigation | Từ A03 (lần đầu). Lưu xong → S01, Back không quay lại A05 |
| UI constraints | Quy tắc 16: không có ô SĐT, Zalo, email; dữ liệu import chỉ đọc. Giới thiệu không bắt buộc. Mục tiêu hoàn tất ≤ 1 phút |

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

#### S09 · Gửi yêu cầu tham gia (bottom sheet) – bản Lab 2, xem bản 4 ở mục 4a

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

> **Chỉ còn trong Figma Lab 2.** Từ 20/10/2026 không có bước chấp nhận / từ chối (quy tắc 9): L02, L03 không làm; L01 làm theo mục 4a. Giữ spec dưới đây để đối chiếu prototype.

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

## 4a. Spec bổ sung sau góp ý (bản 4, chưa có trong Figma)

Cùng một spec cho mobile và web; web đổi bố cục theo `DESIGN.md` mục 8.2.

### Thay đổi ở màn hình cũ

| Màn hình | Thay đổi | Quy tắc |
|---|---|---|
| S02, S03 | Hồ sơ = `SkillBars` + giới thiệu; S03 sửa bằng `SkillSlider`. Không còn mục liên lạc | 16 |
| S04 | Hồ sơ người khác: `SkillBars` + giới thiệu. Email chỉ hiện nếu người xem là đồng đội, giảng viên, mentor của nhóm | 11, 16 |
| S05, S08, S10 | Chỉ mở được trong giai đoạn lập nhóm; ngoài giai đoạn → `showPhaseClosedDialog` | 18 |
| S15 | Thêm `ProjectCard` ("Tiếp tục từ EXE101 · SP26", nút "Lịch sử" mở `ProjectTimeline` trong bottom sheet), mục "Cố vấn" (giảng viên hướng dẫn + một mentor, `RoleChip`, dòng lý do ghép; chưa ghép → "Chốt giảng viên lúc 18:00 · 17/09" / "Mentor ghép từ tuần 6") và lối tắt "Mở dự án". EXE101: `CheckpointProgress` ("Checkpoint 2/4"). EXE201: dòng "Họp hằng tuần · Thứ Ba 09:15" hoặc nút "Chọn lịch họp" → C09 | 9, 20, 21, 25, 26, 27 |
| S27, S28 | Loại thay đổi mới "Đổi đề tài": ô tên đề tài mới, mô tả, lý do bắt buộc; hiện cạnh thay đổi thành viên trong cùng yêu cầu | 26 |
| C06 | Nhóm EXE201 có thư mục ghim "EXE101 (kỳ trước)" (icon `history`), mọi file trong đó chỉ đọc | 26 |
| S20 | Mỗi yêu cầu tham gia: `SkillBars(highlight: kỹ năng nhóm đang thiếu)`, danh sách ý tưởng hoặc nhãn "Chưa có ý tưởng", hai nút Nhận / Từ chối | 16, 17 |
| S23 | "Nguyện vọng giảng viên": `PreferenceList` 3 ô có thứ tự (kéo để đổi, có nút lên/xuống thay cho kéo – WCAG 2.5.7), dòng hạn chốt; dưới là mục "Quan tâm nhóm bạn" và "Đã hướng dẫn ở EXE101" (avatar + nút "Thêm vào nguyện vọng"), rồi danh sách mọi giảng viên có tìm kiếm. Không còn "Gửi yêu cầu" | 9, 19 |
| S24 | Chi tiết giảng viên: chuyên môn, chip "Quan tâm nhóm bạn"; nút "Thêm vào nguyện vọng" / "Bỏ khỏi nguyện vọng" (snackbar Hoàn tác) | 9 |
| S25 | Trước khi ghép: nguyện vọng 1–3 (chỉnh tới hạn). Sau khi ghép: thẻ giảng viên + `MatchReasonChip` ("Hai bên cùng chọn", "Nguyện vọng 2", "Giảng viên quan tâm", "Theo chuyên môn", "Bộ môn phân công"); nguyện vọng khóa. Nguyện vọng đã đủ nhóm → dòng giải thích | 9 |
| L01 | Giai đoạn lập nhóm: "Nhóm chọn tôi n", "Đang quan tâm 5/12", dòng hạn ghép; đề tài gợi ý mới. Giai đoạn học kỳ: ba thẻ số liệu bấm được "Cần trả lời n", "Trễ hạn nộp n", và "Họp hôm nay n" (EXE201) hoặc "Chưa trình bày CP2 n" (EXE101); EXE201 chưa đăng lịch → thẻ "Đăng lịch họp hằng tuần" → C09 | 9, 22, 23, 25, 27 |
| L02, L03 | **Không làm** (không còn yêu cầu hướng dẫn). Thay bằng tab "Nhóm chọn tôi" của L07 | 9 |
| L04, L05 | Mỗi nhóm có tiến độ việc (thanh), số bài nộp chờ duyệt, `MatchReasonChip`; nhóm mình làm mentor có `RoleChip` "Mentor". EXE101: `CheckpointProgress` trên thẻ, `FilterChip` "Chưa trình bày CP n"; L05 có nút "Đánh dấu Checkpoint" → bottom sheet (chọn CP, ngày mặc định hôm nay, nhận xét ≤ 500) → snackbar Hoàn tác. EXE201: slot họp hằng tuần trên thẻ | 9, 20, 23, 25, 27 |

#### S09 · Gửi yêu cầu tham gia (bản 4)

| Mục | Nội dung |
|---|---|
| Layout | Bottom sheet (web: drawer phải). Ô lời nhắn; mục "Ý tưởng của bạn" dùng `IdeaListField`; dòng nhắc hạn 72 giờ; thanh nút cố định "Hủy" / "Xin tham gia" |
| Components | `showModalBottomSheet`, `TextField`, `IdeaListField`, `TextButton`, `FilledButton` |
| States | Mặc định (chưa có ý tưởng nào, chưa tích) · đã thêm 1–3 ý tưởng · đã tích "Chưa có ý tưởng" (các ý tưởng thu gọn, vô hiệu) · thiếu lựa chọn (dòng lỗi dưới mục ý tưởng) · đang gửi · thành công · lỗi gửi (giữ toàn bộ nội dung) |
| Interactions | "+ Thêm ý tưởng" thêm thẻ (ẩn khi đủ 3); nút xóa trên thẻ; tích ô → thu gọn ý tưởng. "Xin tham gia" khi chưa có ý tưởng và chưa tích → lỗi tại mục, không gửi |
| Navigation | Overlay trên S08; thành công → S08 "đã gửi yêu cầu" + `SnackBar` |
| UI constraints | Quy tắc 17: 1–3 ý tưởng hoặc "Chưa có ý tưởng"; tiêu đề ≤ 80, mô tả ≤ 300 ký tự. Ý tưởng chỉ thành viên nhóm nhận được thấy |

### Workspace nhóm (C01–C09)

#### C01 · Dự án – tổng quan

| Mục | Nội dung |
|---|---|
| Layout | Người xem là mentor: `MentorBriefCard` ở trên cùng (đề tài, tiến độ, "Buổi mentor n/3", 3 file mới nhất, biên bản gần nhất). Đầu trang: tên nhóm, hàng avatar thành viên + cố vấn. Bốn thẻ số liệu bấm được: "Việc của tôi", "Sắp đến hạn", "Họp sắp tới", "Câu hỏi chờ". Hàng lối tắt: Tin nhắn · Việc · File · Lịch họp. EXE101: thẻ `CheckpointProgress` (4 chấm, nhận xét gần nhất). EXE201: thẻ "Họp tuần này" (giờ, link, nút "Vào họp" khi còn ≤ 10 phút). Danh sách "Mới cập nhật" (tối đa 5 dòng) |
| Components | `Card` số liệu, `ListTile`, `Badge` số chưa đọc, avatar xếp chồng, `RoleChip` |
| States | Có dữ liệu · nhóm mới (mỗi thẻ số liệu = 0, một `EmptyState` gợi ý "Tạo việc đầu tiên") · đang tải · lỗi tải |
| Interactions | Chạm thẻ số liệu → C04 lọc sẵn / C07 / C02; kéo để làm mới |
| Navigation | Tab "Dự án" (giai đoạn học kỳ) của sinh viên; từ L04 / M01 với giảng viên, mentor. → C02, C04, C06, C07 |
| UI constraints | Quy tắc 21: chỉ thành viên, giảng viên chính thức, mentor được gán, Bộ môn (chỉ xem). Mỗi thẻ tối đa một dòng (`DESIGN.md` 9.1) |

#### C02 · Tin nhắn – danh sách hội thoại

| Mục | Nội dung |
|---|---|
| Layout | Sinh viên: hai dòng "Nhóm" và "Cố vấn" của nhóm mình. Giảng viên / mentor: một dòng cho mỗi kênh Cố vấn của các nhóm, sắp theo tin mới nhất; bộ lọc "Tất cả / Cần trả lời" |
| Components | `ListTile` (avatar nhóm, tên kênh, tin cuối 1 dòng, thời gian, `Badge` chưa đọc, chip "Cần trả lời"), `SegmentedButton` lọc |
| States | Có dữ liệu · trống · đang tải · mất kết nối realtime (dải mảnh "Đang kết nối lại…", vẫn đọc được lịch sử) |
| Interactions | Chạm → C03; web ≥ 1024 px mở C03 ở cột phải |
| Navigation | Tab "Tin nhắn" của giảng viên / mentor; từ C01 với sinh viên |
| UI constraints | Kênh Nhóm không hiện với giảng viên, mentor (quy tắc 22) |

#### C03 · Kênh tin nhắn

| Mục | Nội dung |
|---|---|
| Layout | App bar: tên kênh + số người. Danh sách tin (mới nhất ở dưới), chia theo ngày. Khung soạn tin dính đáy. Web ≥ 1280 px: cột phải thông tin nhóm |
| Components | `MessageBubble`, `MessageComposer` (đính kèm file từ C06, công tắc "Hỏi cố vấn" ở kênh Cố vấn), `RoleChip`, luồng trả lời trong bottom sheet / drawer |
| States | Có tin · kênh trống ("Chưa có tin nhắn.") · đang tải lịch sử (skeleton phía trên) · tin đang gửi · tin gửi lỗi ("Gửi lại") · câu hỏi chờ / đã trả lời |
| Interactions | Gửi bằng nút hoặc `Enter` (web). Nhấn giữ tin (mobile) / di chuột (web) → Trả lời, Đánh dấu là câu hỏi. Cố vấn trả lời trong luồng → câu hỏi chuyển "Đã trả lời". Cuộn lên tải thêm (cursor) |
| Navigation | Từ C02, C01, thông báo câu hỏi; Back về màn cha |
| UI constraints | Tin ≤ 2.000 ký tự; tin thường không tạo thông báo; chỉ câu hỏi và nhắc tên (`@`) tạo thông báo (PROJECT_CONTEXT §5.7). Đánh dấu đã đọc khi tin hiện trên màn hình |

#### C04 · Bảng việc

| Mục | Nội dung |
|---|---|
| Layout | Mobile: tab trạng thái "Cần làm · Đang làm · Xong" (kèm số), danh sách `TaskCard`, nút nổi "+". Web ≥ 1024 px: ba cột kéo thả. Bộ lọc nhanh: "Của tôi", "Bài nộp", "Trễ hạn" |
| Components | `TabBar`, `TaskCard`, `FilterChip`, `FloatingActionButton` (mobile) / nút "Tạo việc" (web) |
| States | Có dữ liệu · cột trống ("Chưa có việc.") · đang tải · lỗi tải |
| Interactions | Tạo việc (bottom sheet: tên, người làm, hạn). Đổi trạng thái bằng menu thẻ (mobile) hoặc kéo thả (web). Giảng viên thấy nút "Tạo bài nộp" thay cho "Tạo việc". Mentor không có nút tạo |
| Navigation | Từ C01; chạm thẻ → C05 |
| UI constraints | Quy tắc 23: bài nộp chỉ giảng viên tạo; mentor chỉ bình luận. Hạn hiển thị tương đối ("Còn 2 ngày") + tuyệt đối khi mở chi tiết |

#### C05 · Chi tiết việc / bài nộp

| Mục | Nội dung |
|---|---|
| Layout | Tiêu đề, chip trạng thái, người làm, hạn, mô tả. Bài nộp có thêm "Bản đã nộp" (dòng file, phiên bản, thời gian) và kết quả duyệt. Bình luận ở cuối |
| Components | `Chip`, `FileTile`, `TextField` bình luận, `FilledButton` ("Nộp bài" với nhóm, "Đạt" với giảng viên), `OutlinedButton` ("Cần sửa") |
| States | Việc: Cần làm · Đang làm · Xong. Bài nộp: Chưa nộp · Đã nộp · Cần sửa · Đạt · Trễ hạn. Đang tải lên (thanh tiến độ) · lỗi tải lên (giữ file đã chọn, Thử lại) |
| Interactions | Nhóm: chọn file từ C06 hoặc tải lên → "Nộp bài". Giảng viên: "Đạt" hoặc "Cần sửa" (bắt buộc bình luận) |
| Navigation | Từ C04, C01, thông báo; Back về màn cha |
| UI constraints | File ≤ 25 MB, đúng định dạng (quy tắc 24) – kiểm tra trước khi tải lên. Nộp sau hạn vẫn được, chip "Trễ hạn" giữ lại |

#### C06 · File dự án

| Mục | Nội dung |
|---|---|
| Layout | Đường dẫn thư mục; danh sách thư mục rồi file (chỉ bản mới nhất); nút "Tải lên" (thành viên). Chi tiết file: các phiên bản, bình luận |
| Components | `FileTile`, `ListTile` thư mục, `FilePicker`, drawer / bottom sheet chi tiết |
| States | Có dữ liệu · thư mục trống · đang tải lên (tiến độ từng file) · lỗi tải lên · file quá lớn / sai định dạng (lỗi tại chỗ, không tải) |
| Interactions | Tải lên trùng tên → hỏi "Tạo phiên bản mới?" → v(n+1). Chạm file → mở bằng URL ký sẵn. Xóa (người tải lên, trưởng nhóm) → dialog xác nhận |
| Navigation | Từ C01, C03 (đính kèm), C05 (chọn file nộp) |
| UI constraints | Quy tắc 24: giảng viên, mentor chỉ xem và bình luận (không thấy nút tải lên, xóa) |

#### C07 · Lịch họp

| Mục | Nội dung |
|---|---|
| Layout | Ba nhóm: "Chờ chốt", "Sắp tới", "Đã họp". Mỗi dòng: tiêu đề, ngày giờ (hoặc "3 khung giờ"), nơi họp, chip trạng thái; họp hằng tuần EXE201 có chip "Hằng tuần". Nút "Đề xuất họp". Giảng viên: xem lịch mọi nhóm theo ngày, lối tắt "Lịch họp hằng tuần" → C09 |
| Components | `ListTile`, `Chip`, `FilledButton` |
| States | Có dữ liệu · trống · đang tải |
| Interactions | Chạm → C08; "Đề xuất họp" → C08 ở chế độ tạo |
| Navigation | Từ C01, M01, L01, thông báo |
| UI constraints | Quy tắc 25: người tổ chức là trưởng nhóm, giảng viên hướng dẫn hoặc mentor. Họp hằng tuần không bỏ phiếu; chỉ giảng viên hủy / dời một tuần (lý do bắt buộc) |

#### C08 · Đề xuất / chi tiết cuộc họp

| Mục | Nội dung |
|---|---|
| Layout | Tạo: tiêu đề, 1–3 khung giờ (chọn ngày + giờ + thời lượng), nơi họp (phòng / link), nội dung (agenda). Chi tiết: các `SlotVoteTile`, người tham dự, nút "Chốt khung này" (người tổ chức). Sau họp: mục "Biên bản" + danh sách việc rút ra (checkbox "Tạo việc") |
| Components | `DatePicker`, `TimePicker`, `TextField`, `SlotVoteTile`, `CheckboxListTile`, `FilledButton` |
| States | Đang tạo · chờ trả lời · đã chốt · đã hủy (lý do) · đã họp (có / chưa có biên bản) · lỗi lưu (giữ nội dung) |
| Interactions | Thành viên chọn Có / Không từng khung; người tổ chức chốt → mọi người được báo; thêm khung khi không khung nào đủ người. Biên bản: chọn dòng → "Tạo việc" mở bottom sheet đã điền sẵn |
| Navigation | Từ C07, thông báo; chốt xong → C07 "Sắp tới" |
| UI constraints | Mục tiêu ≤ 2 lượt tương tác của người tổ chức để chốt (persona.md chỉ số 12). Nhắc 24 giờ và 1 giờ do Job Service gửi |

#### C09 · Lịch họp hằng tuần (EXE201, bổ sung 20/10/2026)

| Mục | Nội dung |
|---|---|
| Layout | **Giảng viên:** danh sách khung giờ (thứ, giờ bắt đầu – kết thúc), thời lượng slot (mặc định 15 phút), link họp; lưới tuần các slot (trống / tên nhóm); bộ đếm "32 slot · 40 nhóm · còn 3 nhóm chưa chọn"; nút "Đăng lịch". **Trưởng nhóm:** lưới slot (trống / đã có nhóm / của nhóm bạn), dòng hạn "Chọn trước 23:59 · CN tuần 2". Thành viên chỉ xem slot của nhóm. Web ≥ 1024 px: lưới theo cột ngày |
| Components | `WeeklySlotGrid` (ô slot là nút có nhãn đọc được "Thứ Ba 09:15, còn trống"), `TimeRangeField`, `TextField` link, `FilledButton`, bottom sheet đổi slot |
| States | GV: chưa có khung · đang soạn (xem trước số slot) · đã đăng · thiếu slot so với số nhóm (banner cảnh báo, vẫn đăng được). Trưởng nhóm: chưa có lịch ("Giảng viên chưa đăng lịch") · đang chọn · đã chọn · hết hạn (chỉ xem) · slot vừa bị chọn (lỗi tại ô, lưới tự cập nhật) · đang tải · lỗi |
| Interactions | GV thêm / sửa khung → lưới cập nhật ngay; chạm ô có nhóm → bottom sheet "Đổi slot" chọn ô trống khác. Trưởng nhóm chạm ô trống → xác nhận trong bottom sheet → đã chọn (đổi được tới hạn) |
| Navigation | GV: từ L01 (thẻ "Đăng lịch họp hằng tuần"), C07. Trưởng nhóm: từ S15, C01, thông báo "Đã có lịch họp" |
| UI constraints | Quy tắc 25: một slot một nhóm, mỗi nhóm một slot cố định cả kỳ, ai chọn trước được trước; hết hạn hệ thống xếp nhóm chưa chọn; trạng thái ô không chỉ phân biệt bằng màu (kèm chữ / icon) |

### Giảng viên và Mentor

#### L07 · Đề tài (giảng viên và mentor)

| Mục | Nội dung |
|---|---|
| Layout | Hàng giới hạn "Đang quan tâm 5/12 · Ghép lúc 18:00 · 17/09" (mentor: "· Ghép tuần 6"). Ba tab: **"Gợi ý"** (thẻ đề tài: tên nhóm, tên dự án, chip domain trùng chuyên môn (Secondary), "4/6 thành viên", `InterestButton`, dòng "n người khác cũng quan tâm"), **"Nhóm chọn tôi"** (chỉ giảng viên: thẻ có chip "Nguyện vọng 1/2/3"), **"Đã quan tâm"** |
| Components | `Card`, `Chip`, `InterestButton`, `SegmentedButton` / `TabBar`, icon `info` giải thích cách gợi ý |
| States | Có gợi ý · chưa khai báo chuyên môn (`EmptyState` + "Cập nhật chuyên môn" → L06) · không có gợi ý · đã đạt giới hạn quan tâm (nút khóa + lý do) · nhóm vừa được ghép (thẻ chuyển "Đã có giảng viên" / "Đã có mentor") · ngoài giai đoạn quan tâm (chỉ xem, dòng giải thích) · đang tải |
| Interactions | "Quan tâm" ↔ "Đã quan tâm" (bấm lại để rút, snackbar Hoàn tác thay cho xác nhận – `DESIGN.md` 6.4); chạm thẻ → chi tiết đề tài (ý tưởng, thành viên, kỹ năng nhóm, nhóm xếp mình ở nguyện vọng mấy) |
| Navigation | Tab "Đề tài" của giảng viên (giai đoạn lập nhóm) và mentor (trước tuần 6); → chi tiết đề tài |
| UI constraints | Quy tắc 9, 19, 20: quan tâm không giữ chỗ, là một tiêu chí khi hệ thống ghép; chỉ hiện **số** người khác, không hiện tên; giới hạn = số nhóm còn nhận + 2 (10 nếu không đặt tối đa; mentor: tối đa + 2). Không có nút chấp nhận / từ chối |

#### M01 · Trang chủ mentor

| Mục | Nội dung |
|---|---|
| Layout | Lời chào; ba thẻ số liệu bấm được: "Cần trả lời", "Họp sắp tới", "Chờ chốt lịch"; danh sách "Nhóm đang mentor" (tên nhóm, đề tài, `MentorSessionChip`, giảng viên hướng dẫn). Hàng "Đang nhận 3 nhóm · mong muốn 4" chạm để sửa số mong muốn |
| Components | `Card` số liệu, `ListTile`, `LinearProgressIndicator`, `RoleChip` |
| States | Có nhóm · trước tuần 6 (thẻ "Đang quan tâm n đề tài · Ghép vào tuần 6" + nút "Xem đề tài" → L07) · đã tới tuần 6 nhưng chưa được ghép (`EmptyState` "Bộ môn đang hoàn tất ghép mentor.") · đang tải |
| Interactions | Chạm số liệu → C02 lọc "Cần trả lời" / C07; chạm nhóm → C01 của nhóm |
| Navigation | Tab Trang chủ của mentor; icon tài khoản → hồ sơ mentor |
| UI constraints | Quy tắc 20: mỗi nhóm một mentor; số nhóm mong muốn ≤ tối đa do Bộ môn đặt; mentor không có nguyện vọng của nhóm, checkpoint, lịch họp hằng tuần, bài nộp – không hiện các mục này. Ghép theo quan tâm của mentor ở tuần 6, sinh viên không chọn mentor |

## 5. Checklist khi hiện thực một màn hình

- [ ] Mọi màu, chữ, khoảng cách lấy từ `Theme.of(context)` / `AppSpacing` / `AppRadius`.
- [ ] Đủ các trạng thái ghi ở mục "States", kể cả đang tải, trống và lỗi.
- [ ] Kiểm tra ở 360 và 412 dp, cỡ chữ hệ thống 130% không vỡ bố cục.
- [ ] Vùng chạm ≥ 48, chữ nội dung ≥ 14 sp, trạng thái có chữ + icon.
- [ ] Back đúng màn cha; sau khi gửi thành công không quay lại form.
- [ ] Đối chiếu frame tương ứng trong Figma `03 Final UI` và luồng trong `06 Prototype`; màn hình bản 4 đối chiếu mục 4a và `DESIGN.md` bản 4 (Figma chưa có).
- [ ] Màn hình chạy được trên web ở ba mức bề rộng (`DESIGN.md` 8.2) nếu thuộc vai trò Sinh viên, Giảng viên, Mentor.
