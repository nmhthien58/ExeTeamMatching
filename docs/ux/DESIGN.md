# DESIGN.md – EXE Team Matching System

> Quy ước thiết kế cho **Mobile (Flutter, Material 3)** và **web quản trị (React)**.
> **Bản 3 – 13/10/2026.** Bản 1 chuyển nguyên từ `EXE_Team_Matching_Quy_uoc_thiet_ke.docx`. Bản 2 (12/10/2026) bổ sung để đáp ứng yêu cầu accessibility và design system của PRM323 Lab 2; mọi điểm khác bản 1 được liệt kê ở [Phụ lục B](#phụ-lục-b--thay-đổi-so-với-bản-1). Bản 3 ghi lại các quy ước rút ra từ ba vòng tinh chỉnh trong [ai-design-log.md](ai-design-log.md); mọi điểm khác bản 2 được liệt kê ở [Phụ lục C](#phụ-lục-c--thay-đổi-so-với-bản-2).
> Dùng kèm: [persona.md](persona.md), [user-flow.md](user-flow.md), file Danh sách màn hình.

Cả nhóm vẽ trên một bộ quy ước duy nhất: **nền trắng, màu nhấn xanh da trời, font Be Vietnam Pro, lưới 4 px và component theo Material 3**. Mọi màu, kiểu chữ và component lấy từ thư viện chung do người nhận gói G1 dựng; không ai tự đặt mã màu hay cỡ chữ riêng trong màn hình của mình.

## Nguyên tắc

1. **Trạng thái luôn nhìn thấy được.** Mọi nhóm, lời mời và yêu cầu đều hiển thị trạng thái hiện tại và mốc thời gian. Đây là nỗi lo lớn nhất của cả bốn persona.
2. **Yên tĩnh ở mọi nơi, nổi bật ở một chỗ.** Giao diện phẳng, trắng, viền mảnh. Điểm nhấn riêng của sản phẩm là **thanh điều kiện nhóm** (6 chấm thành viên và 2 dòng kiểm tra) – xuất hiện ở mọi nơi có nhóm, không trang trí thêm ở chỗ khác.
3. **Không bao giờ chỉ dùng màu.** Mỗi trạng thái có chữ, và có icon khi không gian cho phép.
4. **Nói rõ việc tiếp theo.** Màn trống, lỗi và nút bị khóa đều giải thích lý do và cách đi tiếp.
5. **Token trước, màn hình sau.** Đổi một token thì mọi màn hình đổi theo; không có giá trị gõ tay.

---

## 1. Frame, lưới và co giãn

| Hạng mục | Mobile | Web |
|---|---|---|
| Frame thiết kế | **360 × 800** (dài hơn thì kéo dài frame, giữ rộng 360) | **1440 × 900** (dài hơn thì kéo dài frame) |
| Frame kiểm tra | **412 × 915** – bắt buộc cho mọi màn hình trong 3 flow | 1280 × 800 |
| Lề nội dung | 16 px hai bên | Sidebar 240 px; nội dung lề 24 px |
| Lưới | 4 cột, gutter 16 px | 12 cột, gutter 24 px trong vùng nội dung |
| Vùng hệ thống | Status bar 24 px; app bar 56 px; tab bar 80 px; thanh điều hướng hệ thống 24 px phía dưới | Header 64 px |
| Đơn vị | Mọi kích thước và khoảng cách là bội số của 4 | Như mobile |

### 1.1 Quy tắc co giãn từ 360 lên 412

- Lề giữ 16 px; cột giãn theo chiều rộng. Không đổi cỡ chữ, cỡ icon hay chiều cao component.
- Thẻ, ô nhập, nút cuối màn hình, banner: **Fill** theo chiều ngang. Không đặt chiều rộng cố định.
- Hàng chip: **Wrap** xuống dòng, không cắt chip.
- Tên nhóm và họ tên: tối đa 2 dòng rồi cắt bằng dấu "…". Dòng phụ: 1 dòng. Riêng dòng "MSSV · chuyên ngành" của dòng thành viên được xuống tối đa 2 dòng, vì chuyên ngành là thông tin dùng để ra quyết định và không được cắt.
- Dialog: cách mép màn hình 24 px mỗi bên (rộng 312 ở frame 360, 364 ở frame 412), tối đa 400.
- Bottom sheet và snackbar: rộng hết màn hình (snackbar cách mép 16).
- Trong Figma, mọi frame màn hình đặt constraint **Left & Right** và auto layout để kéo từ 360 sang 412 không vỡ.

### 1.2 Vùng an toàn

- Nội dung và nút không nằm dưới status bar hay thanh điều hướng hệ thống.
- Nút cố định cuối màn hình nằm trong một thanh nền Surface, viền trên 1 px Border, padding 16, phía trên vùng hệ thống.
- Danh sách cuộn có khoảng đệm dưới bằng chiều cao tab bar hoặc thanh nút cố định, để dòng cuối không bị che.

### 1.3 Web

- Sidebar cố định 240 px; vùng nội dung giãn, rộng tối đa 1200 px và căn trái.
- Hỗ trợ từ 1280 px trở lên. Hẹp hơn thì bảng cuộn ngang trong khung của nó, trang không cuộn ngang.

---

## 2. Màu sắc

Tông chủ đạo là trắng và xanh da trời: khoảng **70%** diện tích là trắng, **20%** là nền xanh rất nhạt và viền xám xanh, **10%** là xanh đậm cho hành động chính. Bộ xanh lấy đúng dải Light Blue của Material nên có sẵn trong Flutter.

Cột **Token** là tên biến trong Figma (collection `Color`) và là tên dùng khi handoff.

### 2.1 Màu thương hiệu

| Tên màu | Token | Hex | Dùng cho | Flutter |
|---|---|---|---|---|
| Primary | `color/primary` | `#0277BD` | Nút chính, link, icon và tab đang chọn, viền focus, phần đã đạt của thanh tiến độ. Được đặt chữ trắng lên trên. | `Colors.lightBlue.shade800` |
| Primary Dark | `color/primary-dark` | `#01579B` | Trạng thái nhấn của nút chính; **mọi chữ đặt trên Primary Container**. | `Colors.lightBlue.shade900` |
| Sky | `color/sky` | `#29B6F6` | Chỉ trang trí: minh họa. Không dùng làm nền cho khối nội dung, kể cả lời chào trang chủ. Chữ đặt lên trên dùng Text Primary. Không dùng để truyền đạt thông tin. | `Colors.lightBlue.shade400` |
| Primary Border | `color/primary-border` | `#B3E5FC` | Viền thẻ đang chọn, nền avatar chữ cái (chữ Primary Dark). | `Colors.lightBlue.shade100` |
| Primary Container | `color/primary-container` | `#E1F5FE` | Nền chip domain, mục đang chọn ở tab bar và sidebar, banner thông tin, phần nền của thanh tiến độ. | `Colors.lightBlue.shade50` |

### 2.2 Màu nền, viền và chữ

| Tên màu | Token | Hex | Dùng cho | Flutter |
|---|---|---|---|---|
| Surface | `color/surface` | `#FFFFFF` | Nền thẻ, app bar, tab bar, bottom sheet, dialog, sidebar. | `Colors.white` |
| Background | `color/background` | `#F5FAFE` | Nền trang phía sau các thẻ; nền hàng tiêu đề của bảng web. | `Color(0xFFF5FAFE)` |
| Border | `color/border` | `#D9E2EC` | Viền **trang trí**: viền thẻ, đường kẻ phân cách, viền chip không bấm được; nền nút bị vô hiệu. | `Color(0xFFD9E2EC)` |
| Border Strong | `color/border-strong` | `#728AA3` | Viền của **điều khiển bấm được**: ô nhập, dropdown, checkbox, chip lọc chưa chọn, chấm thành viên còn trống. | `Color(0xFF728AA3)` |
| Text Primary | `color/text-primary` | `#102A43` | Tiêu đề và nội dung chính; nền snackbar. | `Color(0xFF102A43)` |
| Text Secondary | `color/text-secondary` | `#486581` | Mô tả, nhãn phụ, **chữ gợi ý trong ô nhập**, icon không được chọn. | `Color(0xFF486581)` |
| Text Disabled | `color/text-disabled` | `#9FB3C8` | Chỉ dùng cho chữ và icon của thành phần **bị vô hiệu**. | `Color(0xFF9FB3C8)` |
| Scrim | `color/scrim` | `#102A43` 40% | Lớp phủ phía sau dialog, bottom sheet, drawer. | `Color(0x66102A43)` |

### 2.3 Màu trạng thái (chữ / nền)

| Tên màu | Token | Hex | Dùng cho | Flutter |
|---|---|---|---|---|
| Success | `color/success` | `#1B5E20` | Chữ và icon trạng thái thành công. | `Color(0xFF1B5E20)` |
| Success Container | `color/success-container` | `#E8F5E9` | Nền chip và banner thành công. | `Color(0xFFE8F5E9)` |
| Warning | `color/warning` | `#8A5300` | Chữ và icon trạng thái đang chờ hoặc sắp hết hạn. | `Color(0xFF8A5300)` |
| Warning Container | `color/warning-container` | `#FFF4E0` | Nền chip và banner cảnh báo. | `Color(0xFFFFF4E0)` |
| Error | `color/error` | `#B3261E` | Chữ lỗi, viền ô nhập lỗi, nút hành động nguy hiểm (chữ trắng). | `Color(0xFFB3261E)` |
| Error Container | `color/error-container` | `#FDECEA` | Nền chip và banner lỗi hoặc bị từ chối. | `Color(0xFFFDECEA)` |
| Neutral Container | `color/neutral-container` | `#EEF2F6` | Nền chip trạng thái trung tính (chữ dùng Text Secondary). | `Color(0xFFEEF2F6)` |

### 2.4 Quy tắc dùng màu

- Chữ trắng chỉ đặt trên Primary, Primary Dark, Error và Text Primary. **Không đặt chữ trắng trên Sky.**
- Chữ trên Primary Container dùng Primary Dark; icon trên Primary Container được dùng Primary.
- Mọi cặp chữ / nền phải đạt **4,5:1**; icon mang nghĩa và viền điều khiển phải đạt **3:1**. Bảng số đo ở [Phụ lục A](#phụ-lục-a--độ-tương-phản).
- Mỗi màn hình chỉ có **một** nút Primary. Hành động phụ dùng nút viền hoặc nút chữ.
- Không dùng màu ngoài bảng. Cần màu mới thì báo người giữ thư viện thêm vào, kèm số đo tương phản.
- Chỉ vẽ giao diện sáng, không vẽ dark mode.

---

## 3. Chữ

Font duy nhất: **Be Vietnam Pro** (Google Fonts), hiển thị dấu tiếng Việt tốt, có sẵn trong Figma và gói `google_fonts` của Flutter. Chỉ dùng ba độ đậm: Regular 400, Medium 500, SemiBold 600.

| Kiểu chữ | Token | Cỡ / dòng | Độ đậm | Dùng cho |
|---|---|---|---|---|
| Display | `type/display` | 24 / 32 | SemiBold | Tiêu đề lớn: lời chào trang chủ (đặt trực tiếp trên nền trang), tiêu đề trang web, con số ở thẻ số liệu |
| Title | `type/title` | 20 / 28 | SemiBold | Tiêu đề app bar, tiêu đề dialog và bottom sheet |
| Subtitle | `type/subtitle` | 16 / 24 | SemiBold | Tên nhóm trên thẻ, tiêu đề mục trong màn hình, tiêu đề banner hạn chót |
| Body Large | `type/body-large` | 16 / 24 | Regular | Chữ trong ô nhập, đoạn mô tả ý tưởng |
| Body | `type/body` | 14 / 20 | Regular | Nội dung mặc định, dòng phụ trên thẻ, dòng hỗ trợ và dòng lỗi dưới ô nhập, dòng trong bảng web, nhãn của thẻ số liệu trên mobile |
| Label | `type/label` | 14 / 20 | Medium | Chữ trên nút, nhãn ô nhập, tab, **chữ trong chip** |
| Caption | `type/caption` | 12 / 16 | Regular | Chỉ cho thông tin phụ trợ: thời gian, bộ đếm ký tự, chú thích ảnh |
| Caption Bold | `type/caption-bold` | 12 / 16 | SemiBold | Nhãn tab bar, tiêu đề cột bảng web |

- **Nội dung người dùng cần đọc để hoàn thành việc đang làm tối thiểu 14 px.** Cỡ 12 chỉ dùng cho bốn trường hợp ghi ở hai dòng Caption. Không dùng chữ nhỏ hơn 12 px.
- Viết hoa chữ cái đầu câu, không viết hoa toàn bộ. Căn trái; chỉ căn giữa ở màn trống và dialog.
- Đoạn văn dài (mô tả ý tưởng, lý do) rộng tối đa khoảng 70 ký tự mỗi dòng trên web.
- Số liệu xếp cột trong bảng web bật *tabular figures* để các chữ số thẳng hàng.
- **Cỡ chữ hệ thống:** ứng dụng không khóa tỷ lệ chữ. Thẻ, nút, chip dùng chiều cao **tối thiểu** thay vì cố định để chữ lớn không bị cắt. Kiểm tra mỗi màn hình trong flow ở tỷ lệ chữ 130%.

---

## 4. Khoảng cách, bo góc, độ nổi

### 4.1 Khoảng cách

| Token | Giá trị | Dùng cho |
|---|---|---|
| `space/1` | 4 | Giữa icon và chữ trong chip |
| `space/2` | 8 | Giữa các phần tử trong một thẻ; giữa nhãn và ô nhập; giữa các chip |
| `space/3` | 12 | Giữa các thẻ trong danh sách; padding dọc của dòng danh sách |
| `space/4` | 16 | Lề màn hình mobile; padding thẻ; giữa các ô nhập trong form |
| `space/6` | 24 | Giữa các mục lớn; padding bottom sheet và dialog; lề nội dung web |
| `space/8` | 32 | Giữa các khối ở màn trống và trang web |

### 4.2 Bo góc

| Token | Giá trị | Dùng cho |
|---|---|---|
| `radius/sm` | 8 | Ảnh nhỏ, ô bảng, ô nhập trên web |
| `radius/md` | 12 | Nút, ô nhập mobile, banner |
| `radius/lg` | 16 | Thẻ, modal web |
| `radius/xl` | 24 | Góc trên bottom sheet, dialog mobile |
| `radius/full` | 999 | Chip, avatar, viên thuốc ở tab bar |

### 4.3 Độ nổi

| Token | Bóng | Dùng cho |
|---|---|---|
| `elevation/0` | Không bóng, viền 1 px Border | Thẻ, app bar, tab bar, ô nhập, bảng |
| `elevation/1` | Y 1, blur 4, `#102A43` 8% | Thanh nút cố định cuối màn hình khi nội dung cuộn bên dưới; app bar khi đã cuộn |
| `elevation/2` | Y 4, blur 16, `#102A43` 8% | Bottom sheet, dialog, drawer, snackbar, nút nổi |

### 4.4 Vùng chạm

- **Mobile: mọi phần tử bấm được có vùng chạm tối thiểu 48 × 48 px**, kể cả khi phần nhìn thấy nhỏ hơn (chip cao 32, icon 24). Trong Figma, bọc phần tử trong một frame 48 × 48 trong suốt.
- Hai vùng chạm cạnh nhau cách nhau tối thiểu 8 px.
- Web: điều khiển cao tối thiểu 40 px; nút chỉ có icon tối thiểu 32 × 32 px.

### 4.5 Chuyển động

| Token | Thời lượng | Dùng cho |
|---|---|---|
| `motion/fast` | 100 ms | Phản hồi khi nhấn (ripple, đổi màu) |
| `motion/base` | 200 ms | Chip đổi trạng thái, hiện/ẩn banner, đóng dialog và bottom sheet |
| `motion/slow` | 300 ms | Chuyển màn hình, mở dialog và bottom sheet |

- Chuyển động chỉ dùng để trả lời thao tác của người dùng hoặc báo có thay đổi. Không có hiệu ứng tự chạy để trang trí.
- Đóng nhanh hơn mở. Đường cong theo mặc định của Material 3.
- Khi người dùng bật "giảm chuyển động" trong hệ điều hành: bỏ trượt và phóng to, chỉ còn mờ dần; skeleton không nhấp nháy.

---

## 5. Icon và hình ảnh

- Một bộ icon duy nhất: **Material Symbols Rounded**, nét 400, không tô đặc. Icon đang chọn ở tab bar thì tô đặc.
- Kích thước: 24 px mặc định; 20 px trong chip và ô nhập; 48 px cho icon ở màn trống.
- Màu icon: Text Secondary khi bình thường, Primary khi đang chọn hoặc là hành động.
- **Nút chỉ có icon** phải có tên đọc được cho trình đọc màn hình và ghi trong handoff (ví dụ icon chuông: "Thông báo, 3 chưa đọc"). Icon đứng cạnh chữ cùng nghĩa là icon trang trí, không cần tên.
- Số chưa đọc trên icon chuông: chấm tròn nền Error, chữ trắng Caption Bold, tối thiểu 16 × 16; trên 99 hiện "99+".
- Avatar: hình tròn 40 px trong danh sách, 72 px ở hồ sơ. Không có ảnh thì hiện 2 chữ cái đầu màu Primary Dark trên nền Primary Border.
- Minh họa (onboarding, màn trống) dùng một phong cách nét phẳng, tông xanh Sky và Primary Container. Minh họa không chứa thông tin mà chữ bên dưới không nói.

---

## 6. Component mobile

### 6.1 Quy cách

| Component | Quy cách |
|---|---|
| App bar | Cao 56 px, nền trắng, tiêu đề kiểu Title căn trái, nút quay lại bên trái, tối đa 2 icon bên phải. Màn hình cấp 1 không có nút quay lại. |
| Tab bar | Cao 80 px, nền trắng, viền trên 1 px. Sinh viên 5 mục, giảng viên 4 mục; mục nào cũng có icon và nhãn. Mục đang chọn: icon tô đặc màu Primary trên nền viên thuốc Primary Container, nhãn Caption Bold màu Text Primary. Mục chưa chọn: icon nét và nhãn màu Text Secondary. |
| Nút chính | Cao 48 px, bo 12, nền Primary, chữ trắng kiểu Label. Rộng hết chiều ngang khi nằm cuối màn hình. |
| Nút phụ / nút chữ | Nút phụ: viền 1 px Primary, chữ Primary, nền trắng; được có một icon 20 px màu Primary đứng trước nhãn (thuộc tính `Icon` của `Button/Secondary`, mặc định tắt). Nút chữ: chỉ chữ Primary. Hành động nguy hiểm (xóa thành viên, rời nhóm): nền Error, chữ trắng, luôn qua dialog xác nhận. Đăng xuất không làm mất dữ liệu nên **không** phải hành động nguy hiểm: dùng nút phụ có icon `logout`, vẫn qua dialog xác nhận, và màu Error chỉ xuất hiện ở nút trong dialog. |
| Ô nhập | Cao 56 px, bo 12, viền 1 px **Border Strong**. Nhãn kiểu Label nằm phía trên, cách 8 px; ô bắt buộc không đánh dấu, ô tùy chọn ghi "(không bắt buộc)" sau nhãn. Chữ gợi ý màu Text Secondary. Dòng hỗ trợ kiểu Body màu Text Secondary bên dưới, cách 4 px. |
| Thẻ | Nền trắng, bo 16, viền 1 px Border, padding 16. Cả thẻ bấm được thì không thêm nút "Xem". |
| Thẻ nhóm | Tên nhóm (Subtitle), tên dự án (Body, Text Secondary), tối đa 2 chip domain rồi "+n", thanh điều kiện nhóm dạng gọn ("4/6 thành viên"), dòng "Đang thiếu: …" nếu có. Chip trạng thái, nếu có, nằm cùng hàng với tên nhóm và căn phải; hàng chip bên dưới chỉ chứa chip domain. **Thẻ do người ngoài nhóm xem** (danh sách nhóm đang tuyển, nhóm gợi ý) không có chip trạng thái; dòng thành viên ghi thêm số chỗ còn lại: "4/6 thành viên · còn 2 chỗ". Nhóm đã đủ 6/6 không xuất hiện trong danh sách nhóm đang tuyển. |
| Đầu màn hình chi tiết nhóm | Tên nhóm (Title) bên trái, chip trạng thái bên phải, cùng một hàng. Tên nhóm tối đa 2 dòng, chip không co; khi không đủ chỗ, chip rớt xuống dòng ngay dưới tên. Hàng chip bên dưới chỉ chứa chip domain. |
| Thẻ sinh viên | Avatar 40, họ tên (Subtitle), MSSV · chuyên ngành (**Body**, Text Secondary), tối đa 3 chip kỹ năng. Khi người xem là trưởng nhóm, thẻ có nút phụ "Gửi lời mời" ở cuối, căn phải. |
| Dòng thành viên | Avatar 40, họ tên, dòng phụ "MSSV · chuyên ngành" (Body, Text Secondary, tối đa 2 dòng). Nhãn vai trò "Trưởng nhóm" đứng sau tên và rớt xuống dòng dưới khi không vừa. Khi dòng có chip trạng thái, dòng phụ không lặp lại chữ của chip: cạnh chip "Rời nhóm" chỉ ghi "Digital Marketing". |
| Dòng điều hướng | Icon 24 bên trái, tiêu đề Body Large, dòng phụ Body màu Text Secondary, mũi tên `chevron_right` bên phải; cả dòng bấm được, vùng chạm tối thiểu 48. Mọi nội dung **bấm được để đi sang màn hình khác** (lời mời đang chờ, việc cần làm, yêu cầu đang chờ) dùng dòng điều hướng đặt trong thẻ, không dùng banner. |
| Thẻ số liệu | Nhãn Body màu Text Secondary, con số Display. Các thẻ cùng hàng cao bằng nhau. Nhãn được xuống 2 dòng ở 360 px. |
| Thẻ việc cần làm ngay | Dùng ở trang chủ khi có một việc gắn với hạn chót: tiêu đề Subtitle, mô tả Body, một nút chính nêu đúng hành động ("Mời người thay thế"). Đặt ngay dưới banner hạn chót và là nút Primary duy nhất của màn hình. |
| Chip | Cao 32 px (vùng chạm 48 nếu bấm được), bo tròn, padding ngang 12, chữ **Label**. Chip domain: nền Primary Container, chữ Primary Dark. Chip kỹ năng: viền Border, chữ Text Secondary. Chip trạng thái: icon 16 px và chữ, màu theo mục 7. Chip lọc chưa chọn: viền Border Strong, chữ Text Primary. Chip lọc đang chọn: nền Primary, chữ trắng, **có dấu tích 16 px phía trước**. |
| Banner hạn chót | Bo 12, padding 12 × 16, **icon 20 px bên trái và dòng chữ nêu thời gian còn lại**. Tiêu đề kiểu Subtitle, dòng mô tả (nếu có) kiểu Body. Banner là thông tin tĩnh: không bấm được và không ghi "Chạm để xem". Còn hơn 72 giờ: nền Primary Container, icon lịch, "Hạn chót 18:00 · 17/09/2026". Còn 72 giờ trở xuống: nền Warning Container, icon đồng hồ, "Còn 2 ngày 5 giờ". Còn 24 giờ trở xuống: nền Error Container, icon cảnh báo, "Còn 5 giờ". Đã khóa: nền Neutral Container, icon ổ khóa, "Đợt đăng ký đã đóng". |
| Thanh điều kiện nhóm | 6 chấm thành viên 12 px: chỗ đã có tô Primary, chỗ trống viền Border Strong; luôn kèm chữ "4/6 thành viên". Dạng đầy đủ thêm 2 dòng kiểm tra "4–6 thành viên" và "Ít nhất 2 chuyên ngành", mỗi dòng có icon dấu tích màu Success hoặc dấu chấm than màu Warning. |
| Bottom sheet | Bo góc trên 24, thanh kéo 32 × 4 px màu Border Strong, padding 24. Sheet không cao hơn màn hình trừ 48 px; nội dung dài cuộn bên trong sheet. Các nút nằm trong **thanh cố định cuối sheet** (nền Surface, viền trên 1 px Border, padding 16), phía trên vùng điều hướng hệ thống 24 px, nên luôn nhìn thấy. Đóng được bằng kéo xuống, chạm lớp phủ **và** nút Hủy hoặc nút đóng. |
| Dialog xác nhận | Cách mép 24 px, bo 24, tiêu đề Title, nội dung Body, 2 nút chữ căn phải: "Hủy" bên trái, hành động bên phải. Tiêu đề là câu hỏi nêu rõ hành động ("Gửi yêu cầu đổi nhóm?"); nút hành động lặp lại động từ đó. Hành động nguy hiểm: nút chữ màu Error. |
| Thông báo nhanh | Snackbar nền Text Primary, chữ trắng, nằm trên tab bar, tự ẩn sau 4 giây. Chỉ dùng cho **kết quả thành công** ("Đã gửi lời mời"). Lỗi cần người dùng xử lý thì dùng dòng lỗi tại ô nhập hoặc banner lỗi, không dùng snackbar. |
| Màn trống | Icon hoặc minh họa 48–120 px, một dòng tiêu đề Subtitle, một dòng mô tả Body, tối đa một nút dẫn tới việc nên làm tiếp. |
| Đang tải | **Danh sách và thẻ:** skeleton đúng hình dạng nội dung, nền Neutral Container, giữ nguyên bố cục khi dữ liệu về. **Nút đang gửi:** vòng xoay 20 px thay cho chữ, giữ nguyên chiều rộng nút, khóa bấm lần hai. **Kéo để làm mới:** vòng xoay Primary ở đầu danh sách. Không dùng vòng xoay toàn màn hình, trừ lần mở ứng dụng đầu tiên. |
| Màn lỗi | **Lỗi cả màn hình** (không tải được dữ liệu): icon 48 px màu Text Secondary, tiêu đề Subtitle nói điều gì xảy ra, mô tả Body nói cách xử lý, nút chính "Thử lại". **Banner lỗi** (lỗi khi gửi, mất mạng): nền Error Container, icon lỗi, chữ Error kiểu Body, nút chữ "Thử lại" bên phải; nội dung người dùng đã nhập được giữ nguyên. |

### 6.2 Trạng thái bắt buộc của component

Mỗi component trong thư viện Figma phải có đủ các biến thể sau.

| Component | Trạng thái |
|---|---|
| Nút (chính, phụ, chữ, nguy hiểm) | Mặc định · Nhấn · Focus · Đang gửi · Vô hiệu |
| Ô nhập | Trống · Focus · Đã nhập · Lỗi · Vô hiệu |
| Thẻ | Mặc định · Nhấn · Đang chọn · Skeleton |
| Chip lọc | Chưa chọn · Đang chọn · Vô hiệu |
| Mục tab bar | Chưa chọn · Đang chọn · Có số chưa đọc |
| App bar | Cấp 1 (không quay lại) · Cấp 2 (có quay lại) · Đã cuộn |
| Dialog | Xác nhận thường · Xác nhận nguy hiểm |
| Đang tải | Skeleton danh sách · Skeleton chi tiết · Nút đang gửi |
| Màn trống | Chưa có dữ liệu · Không có kết quả lọc |
| Màn lỗi | Lỗi cả màn hình · Banner lỗi · Mất mạng |

**Cách thể hiện từng trạng thái**

| Trạng thái | Quy cách |
|---|---|
| Nhấn | Nút chính: nền Primary Dark. Nút phụ, nút chữ, thẻ, dòng danh sách: phủ Primary 8%. Không đổi kích thước. |
| Focus | Viền 2 px Primary. Với nút và chip: vòng 2 px Primary cách mép 2 px. Áp dụng cho bàn phím ngoài trên mobile và mọi điều khiển trên web. |
| Đang chọn | Thẻ: viền 2 px Primary và icon dấu tích ở góc. Chip: xem mục 6.1. |
| Lỗi (ô nhập) | Viền 2 px Error, **icon lỗi 20 px ở cuối ô**, dòng lỗi kiểu Body màu Error bên dưới nêu cách sửa. Lỗi hiện khi rời ô hoặc khi bấm gửi, không hiện khi đang gõ. |
| Vô hiệu | Nút: nền Border, chữ Text Disabled. Ô nhập: nền Background, viền Border, chữ Text Disabled. **Nút vô hiệu luôn kèm một dòng Body màu Text Secondary nêu lý do** ("Đợt đăng ký đã đóng lúc 18:00 · 17/09/2026"). Thẻ có hành động bị khóa (giảng viên đã đủ nhóm) **không làm mờ cả thẻ**: chữ giữ màu bình thường, chỉ hành động bị khóa và thẻ có chip trạng thái nêu lý do ("Đã đủ"). Màn hình không có hành động nào cho người xem cũng ghi một dòng nêu lý do ("Chỉ trưởng nhóm gửi được lời mời."). |

---

## 7. Màu và icon của trạng thái

Mỗi trạng thái nghiệp vụ luôn dùng cùng một nhóm màu **và cùng một icon** ở mọi màn hình, cả mobile và web. Chip trạng thái luôn có chữ; icon đứng trước chữ.

| Nhóm màu | Icon (Material Symbols) | Các trạng thái | Chip mẫu |
|---|---|---|---|
| Warning | `schedule` | Đang chờ · Chờ duyệt · Chờ xác nhận · Sắp mở · Chưa đủ điều kiện | Đang chờ |
| Success | `check_circle` | Đã chấp nhận · Đã duyệt · Giảng viên đã nhận · Đủ điều kiện · Hoạt động | Đã chấp nhận |
| Error | `cancel` | Đã từ chối · Giảng viên từ chối | Đã từ chối |
| Primary | `verified` | Đang mở · Đã chốt · Giảng viên chính thức | Đang mở |
| Neutral | `remove_circle` | Nháp · Hết hạn · Đã thu hồi · Đã khóa · Đã đủ | Nháp |

"Chưa đủ điều kiện" dùng icon `error` (dấu chấm than) thay cho `schedule`, vì đây là việc người dùng phải xử lý chứ không phải chờ.

"Đã khóa" dùng cho đợt đăng ký; "Đã đủ" dùng cho giảng viên đã nhận đủ số nhóm. Danh sách chia theo tab chỉ đặt vào tab "chờ" những mục còn việc phải làm; mục đã thu hồi, hết hạn hoặc bị từ chối nằm ở tab "Đã đóng".

---

## 8. Web quản trị

| Hạng mục | Quy cách |
|---|---|
| Sidebar | Rộng 240 px, nền trắng, viền phải 1 px. Mục đang chọn: nền Primary Container, **chữ Primary Dark**, icon Primary, thanh dọc 3 px Primary ở mép trái. Logo ở trên cùng. |
| Header trang | Cao 64 px: tiêu đề trang kiểu Display bên trái, nút hành động chính bên phải. |
| Bảng | Hàng tiêu đề cao 44 px, nền Background, chữ Caption Bold. Hàng dữ liệu cao 52 px, viền dưới 1 px, không tô xen kẽ. Cột số căn phải. Cột hành động ở cuối. Hàng tiêu đề dính trên cùng khi cuộn. Cột sắp xếp được có icon mũi tên chỉ chiều. Cột chứa chip trạng thái rộng tối thiểu bằng chip dài nhất cộng 24 px padding; chip không được cắt. |
| Trạng thái của bảng | Đang tải: 8 hàng skeleton. Trống: màn trống đặt trong khung bảng. Lỗi: banner lỗi phía trên bảng, có nút "Thử lại". |
| Tìm kiếm và lọc | Một hàng phía trên bảng: ô tìm kiếm bên trái (rộng 320 px), các bộ lọc dạng dropdown kế bên, nút xuất Excel bên phải. Bộ lọc đang áp dụng hiện thành chip có nút gỡ, kèm nút chữ "Xóa bộ lọc". |
| Phân trang | Góc dưới bên phải bảng: "1–20 trên 1.250", nút trước / sau. Mặc định 20 dòng mỗi trang. |
| Form | Một cột, rộng tối đa 640 px, ô nhập cao 40 px, viền Border Strong, nút Lưu và Hủy ở cuối, căn phải. Lỗi hiện tại từng ô; form có từ 2 lỗi trở lên thì thêm tóm tắt lỗi ở đầu form. |
| Drawer và modal | Drawer chi tiết trượt từ phải, rộng 480 px. Modal xác nhận rộng 480 px, bo 16. Cả hai đóng được bằng phím Esc và trả focus về nút đã mở nó. |
| Thẻ số liệu | Dùng ở Tổng quan: nhãn Caption, con số Display, không icon trang trí. |
| Trang duyệt | Căn cứ để quyết định (lý do của người gửi, các dòng thay đổi) nằm phía trên và trong màn hình đầu ở 1440 × 900, trước các số liệu phụ. Trong danh sách so sánh trước / sau, các dòng có thay đổi đứng đầu danh sách. |
| Bàn phím | Mọi thao tác làm được bằng bàn phím; thứ tự Tab theo thứ tự nhìn; vòng focus 2 px Primary cách mép 2 px luôn hiển thị. |

---

## 9. Câu chữ trên giao diện

Giao diện tiếng Việt, xưng "bạn" với người dùng. Các từ dưới đây dùng thống nhất, không thay bằng từ khác.

| Dùng | Không dùng | Ghi chú |
|---|---|---|
| Nhóm | Team, đội | |
| Trưởng nhóm | Leader, nhóm trưởng | Tài liệu kỹ thuật vẫn gọi là Leader |
| Thành viên | Member | |
| Lời mời | Invitation, thư mời | Trưởng nhóm gửi cho sinh viên |
| Yêu cầu tham gia | Đơn xin, request | Sinh viên gửi cho nhóm |
| Yêu cầu đổi nhóm | Đơn đổi nhóm, change request | Chỉ có ở EXE201 |
| Giảng viên | GV, mentor, thầy cô | Trong bảng web được viết tắt "GV" ở tiêu đề cột |
| Bộ môn | Admin, phòng đào tạo | |
| Đợt đăng ký | Round, kỳ đăng ký | |
| Hạn chót | Deadline, hạn cuối | |
| Domain | Lĩnh vực, chủ đề | Giữ nguyên tên tiếng Anh của 18 domain |
| Chuyên ngành | Ngành hẹp, major | |

- Nút bắt đầu bằng động từ: "Gửi lời mời", "Xin tham gia", "Duyệt", "Từ chối", "Lưu thay đổi". Không dùng "OK", "Đồng ý" chung chung.
- Một hành động giữ một tên suốt luồng: nút "Gửi lời mời" → dialog "Gửi lời mời?" → snackbar "Đã gửi lời mời".
- Ngày giờ: `18:00 · 17/09/2026`. Thời gian còn lại: "Còn 2 ngày 5 giờ". Số lượng: "4/6 thành viên".
- Thông báo lỗi nói điều gì xảy ra và cách sửa: "Nhóm cần ít nhất 2 chuyên ngành khác nhau", không viết "Dữ liệu không hợp lệ". Không xin lỗi, không đổ lỗi cho người dùng.
- Màn trống nói việc nên làm tiếp: "Chưa có lời mời nào. Xem các nhóm đang tuyển để xin tham gia."
- Dữ liệu trong mockup lấy từ bộ dữ liệu mẫu ở file context, không dùng tên và số điện thoại thật.

---

## 10. Tổ chức file Figma

| Hạng mục | Quy ước |
|---|---|
| File nộp Lab (mobile) | Đúng 6 trang theo thứ tự: **01 User Flow · 02 Wireframe · 03 Final UI · 04 Design System · 05 Components · 06 Prototype**. Không thêm trang khác vào file này. |
| Trang 03 Final UI | Chia thành các section: Chung · Sinh viên · Giảng viên · Kiểm tra 412. |
| Trang 04 Design System | Bảng màu, kiểu chữ, khoảng cách, bo góc, độ nổi, icon – tất cả hiển thị từ variables và styles. |
| File web và nháp | Màn hình web quản trị, bìa và nháp đặt ở file riêng "EXE Team Matching – Web", dùng chung thư viện. |
| Tên frame | `<Mã> – <Tên màn hình> / <Trạng thái>`. Ví dụ: `S08 – Chi tiết nhóm / Đã gửi yêu cầu`. Frame kiểm tra thêm hậu tố `@412`. |
| Tên component | `<Loại>/<Biến thể>/<Trạng thái>`. Ví dụ: `Button/Primary/Disabled`, `Chip/Status/Warning`. |
| Variables | Ba collection: `Color`, `Space` (gồm cả radius), `Motion`. Tên biến đúng cột Token trong file này. Kiểu chữ và độ nổi là text style và effect style, đặt tên `type/…` và `elevation/…`. |
| Không gõ tay | Không gõ mã hex, cỡ chữ, khoảng cách hay bo góc trực tiếp vào màn hình; mọi giá trị gắn với variable hoặc style. |
| Auto layout | Mọi thẻ, danh sách, form dùng auto layout với khoảng cách theo token ở mục 4. |
| Thư viện | Người nhận gói G1 giữ trang 04 và 05. Cần component mới thì nhắn người giữ thư viện; không tách (detach) component. |
| Sắp xếp màn hình | Mỗi màn hình một hàng ngang: trạng thái mặc định bên trái, các biến thể nối tiếp sang phải, cách nhau 80 px. |

---

## 11. Kiểm tra trước khi báo xong một màn hình

**Cấu trúc**
- [ ] Frame đúng kích thước và đặt tên theo mẫu `<Mã> – <Tên> / <Trạng thái>`.
- [ ] Đã vẽ đủ các trạng thái ghi trong file Danh sách màn hình, gồm đang tải, trống và lỗi nếu màn hình có tải dữ liệu.
- [ ] Mọi màu, kiểu chữ, khoảng cách và bo góc đều gắn variable hoặc style của thư viện.
- [ ] Mọi component lấy từ thư viện, không bị tách.
- [ ] Khoảng cách là bội số của 4, lề hai bên 16 px (mobile) hoặc 24 px (web).

**Accessibility**
- [ ] Mọi chữ đạt 4,5:1; icon mang nghĩa và viền điều khiển đạt 3:1 (đối chiếu Phụ lục A).
- [ ] Mọi phần tử bấm được có vùng chạm ≥ 48 × 48 px, cách nhau ≥ 8 px.
- [ ] Nội dung cần đọc ≥ 14 px; cỡ 12 chỉ ở thời gian, bộ đếm, nhãn tab bar.
- [ ] Không có thông tin nào chỉ thể hiện bằng màu: trạng thái có chữ và icon, lỗi có dòng lỗi.
- [ ] Đã kéo frame sang 412 px: không vỡ bố cục, không cắt chữ, không tràn ngang.
- [ ] Thông tin dùng để ra quyết định (chuyên ngành, trạng thái, số chỗ) không bị cắt bằng "…" ở 360 px.
- [ ] Không có thẻ nào bị làm mờ bằng độ trong suốt; thành phần bị khóa dùng đúng quy cách "Vô hiệu" ở mục 6.2.
- [ ] Nút chỉ có icon đã ghi tên đọc được trong handoff.

**Nội dung và luồng**
- [ ] Chỉ có một nút Primary.
- [ ] Nút vô hiệu có dòng giải thích lý do.
- [ ] Từ ngữ đúng bảng ở mục 9; trạng thái đúng màu và icon ở mục 7.
- [ ] Đã nối prototype tới màn hình trước và sau trong luồng; nút quay lại về đúng màn hình cha; không có ngõ cụt.
- [ ] Nút hành động chính nằm trong màn hình đầu hoặc trong thanh cố định; bottom sheet không che mất nút của chính nó.

---

## 12. Ánh xạ sang Flutter

### 12.1 Token → Flutter

| Token | Flutter |
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
| `color/sky`, `color/primary-border`, nhóm success / warning / neutral, `color/text-disabled` | `ThemeExtension<AppColors>` (Material không có vai trò tương ứng) |
| `type/display` … `type/caption` | `TextTheme`: `headlineSmall`, `titleLarge`, `titleMedium`, `bodyLarge`, `bodyMedium`, `labelLarge`, `bodySmall`, `labelSmall` |
| `space/*`, `radius/*` | Hằng số trong `AppSpacing`, `AppRadius` |
| `elevation/0–2` | `elevation: 0`, `1`, `3` với `shadowColor` là Text Primary |
| `motion/*` | Hằng số `Duration` trong `AppMotion` |

Nền tảng: `ThemeData(useMaterial3: true)` với `ColorScheme` khai báo tay theo bảng trên (không dùng `fromSeed`, vì sẽ sinh ra màu khác bảng). Font: `GoogleFonts.beVietnamProTextTheme()`. Trong code không viết `Color(0x…)` hay `TextStyle(fontSize: …)` trực tiếp; luôn lấy qua `Theme.of(context)`.

### 12.2 Component → widget

| Thiết kế | Flutter |
|---|---|
| Nút chính / phụ / chữ | `FilledButton` / `OutlinedButton` / `TextButton` |
| Nút phụ có icon | `OutlinedButton.icon` |
| Nút chỉ có icon | `IconButton` kèm `tooltip` (cũng là tên cho trình đọc màn hình) |
| Tab bar, app bar | `NavigationBar`, `AppBar` |
| Thẻ | `Card.outlined` |
| Chip domain, kỹ năng, trạng thái | `Chip` |
| Chip lọc | `FilterChip` (có sẵn dấu tích khi chọn) |
| Ô nhập | `TextField` với `OutlineInputBorder` bo 12, `errorText`, `helperText` |
| Bottom sheet, dialog, thông báo nhanh | `showModalBottomSheet` (với `isScrollControlled: true`, nội dung cuộn trong `Flexible` và thanh nút đặt ngoài vùng cuộn, bọc `SafeArea`), `AlertDialog`, `SnackBar` |
| Dòng điều hướng, dòng thành viên | `ListTile` trong `Card.outlined` |
| Hàng tên nhóm + chip trạng thái | `Wrap` với `alignment: WrapAlignment.spaceBetween` |
| Skeleton | Khối `Container` nền Neutral Container, hoặc gói `skeletonizer` |
| Kéo để làm mới | `RefreshIndicator` |
| Màn trống, màn lỗi, banner hạn chót, thanh điều kiện nhóm | Widget riêng của dự án: `EmptyState`, `ErrorState`, `DeadlineBanner`, `TeamRequirementBar` |
| Icon | `Icons.*_rounded` hoặc gói `material_symbols_icons` |

### 12.3 Ghi chú accessibility khi hiện thực

- Giữ `materialTapTargetSize: MaterialTapTargetSize.padded` (mặc định) để chip và icon có vùng chạm 48.
- Không đặt `textScaler` cố định; dùng `minHeight` thay cho `height` ở thẻ và nút.
- Widget tự dựng có thể bấm phải bọc `Semantics` với nhãn và trạng thái (đang chọn, vô hiệu).
- Đọc `MediaQuery.disableAnimationsOf(context)` để giảm chuyển động.

---

## 13. Ánh xạ sang React (web quản trị) – đề xuất

`PROJECT_CONTEXT.md` chỉ quy định web dùng React. Phần này đề xuất **Tailwind CSS + shadcn/ui** vì cả hai dùng token dạng biến CSS, khớp một-một với bảng màu ở mục 2.

```css
@import "tailwindcss";

:root {
  --background: #F5FAFE;
  --foreground: #102A43;
  --card: #FFFFFF;
  --card-foreground: #102A43;
  --primary: #0277BD;
  --primary-foreground: #FFFFFF;
  --accent: #E1F5FE;
  --accent-foreground: #01579B;
  --muted: #EEF2F6;
  --muted-foreground: #486581;
  --border: #D9E2EC;
  --input: #728AA3;
  --ring: #0277BD;
  --destructive: #B3261E;
  --success: #1B5E20;
  --success-container: #E8F5E9;
  --warning: #8A5300;
  --warning-container: #FFF4E0;
  --destructive-container: #FDECEA;
  --radius: 0.5rem;
}

@theme inline {
  --font-sans: "Be Vietnam Pro", sans-serif;
  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-primary: var(--primary);
  --color-border: var(--border);
  --color-input: var(--input);
  --color-ring: var(--ring);
}
```

| Thiết kế | shadcn/ui |
|---|---|
| Sidebar | `Sidebar` |
| Bảng, phân trang, sắp xếp | `Table` + `DataTable` (TanStack Table), `Pagination` |
| Tìm kiếm và lọc | `Input`, `Select`, `Badge` cho bộ lọc đang áp dụng |
| Form | `Form` (react-hook-form + zod), `Input`, `Textarea`, `Select` |
| Drawer chi tiết | `Sheet` (side="right") |
| Modal xác nhận | `AlertDialog` |
| Chip trạng thái | `Badge` với 5 variant theo mục 7 |
| Banner | `Alert` |
| Thông báo nhanh | `Sonner` |
| Skeleton | `Skeleton` |

---

## Phụ lục A – Độ tương phản

Tính theo công thức WCAG 2.x từ mã hex ở mục 2. Ngưỡng: **4,5:1** cho chữ thường; **3:1** cho chữ lớn (từ 24 px, hoặc 18,66 px đậm), icon mang nghĩa và viền điều khiển.

### A.1 Chữ

| Chữ / nền | Dùng ở | Tỷ lệ | Ngưỡng | Kết quả |
|---|---|---|---|---|
| Text Primary / Surface | Nội dung chính | 14,64 | 4,5 | Đạt |
| Text Primary / Primary Container | Chữ trong banner thông tin | 13,03 | 4,5 | Đạt |
| Text Primary / Warning Container | Chữ trong banner cảnh báo | 13,43 | 4,5 | Đạt |
| Text Primary / Error Container | Chữ trong banner lỗi | 12,80 | 4,5 | Đạt |
| Text Primary / Sky | Chữ đặt trên mảng minh họa màu Sky | 6,36 | 4,5 | Đạt |
| Text Secondary / Surface | Mô tả, chữ gợi ý trong ô nhập | 6,08 | 4,5 | Đạt |
| Text Secondary / Background | Mô tả trên nền trang | 5,79 | 4,5 | Đạt |
| Text Secondary / Neutral Container | Chip trung tính | 5,40 | 4,5 | Đạt |
| Text Secondary / Primary Container | Mô tả trong banner thông tin | 5,41 | 4,5 | Đạt |
| Trắng / Primary | Nút chính, chip lọc đang chọn | 4,80 | 4,5 | Đạt |
| Trắng / Primary Dark | Nút chính khi nhấn | 7,40 | 4,5 | Đạt |
| Trắng / Error | Nút nguy hiểm, số chưa đọc | 6,54 | 4,5 | Đạt |
| Trắng / Text Primary | Snackbar | 14,64 | 4,5 | Đạt |
| Primary / Surface | Nút chữ, link | 4,80 | 4,5 | Đạt |
| Primary / Background | Link trên nền trang | 4,57 | 4,5 | Đạt |
| Primary Dark / Primary Container | Chip domain, mục sidebar đang chọn | 6,59 | 4,5 | Đạt |
| Primary Dark / Primary Border | Chữ cái trên avatar | 5,47 | 4,5 | Đạt |
| Success / Success Container | Chip thành công | 7,00 | 4,5 | Đạt |
| Warning / Warning Container | Chip đang chờ | 5,81 | 4,5 | Đạt |
| Error / Error Container | Chip từ chối | 5,72 | 4,5 | Đạt |
| Error / Surface | Dòng lỗi dưới ô nhập | 6,54 | 4,5 | Đạt |
| Success / Surface | Dòng kiểm tra đạt | 7,87 | 4,5 | Đạt |
| Warning / Surface | Dòng kiểm tra chưa đạt | 6,33 | 4,5 | Đạt |

### A.2 Icon và viền điều khiển

| Nét / nền | Dùng ở | Tỷ lệ | Ngưỡng | Kết quả |
|---|---|---|---|---|
| Border Strong / Surface | Viền ô nhập, chip lọc, chấm trống | 3,57 | 3 | Đạt |
| Border Strong / Background | Viền ô nhập trên nền trang | 3,40 | 3 | Đạt |
| Primary / Surface | Viền focus, icon hành động, chấm đã có | 4,80 | 3 | Đạt |
| Primary / Primary Container | Icon tab đang chọn, thanh tiến độ | 4,27 | 3 | Đạt |
| Text Secondary / Surface | Icon chưa chọn | 6,08 | 3 | Đạt |
| Error / Surface | Viền ô nhập lỗi | 6,54 | 3 | Đạt |

### A.3 Cặp không được dùng

| Cặp | Tỷ lệ | Quy định |
|---|---|---|
| Trắng / Sky | 2,30 | Cấm. Chữ trên Sky dùng Text Primary. |
| Primary / Primary Container (cho chữ) | 4,27 | Cấm cho chữ. Dùng Primary Dark. |
| Text Disabled / Surface | 2,15 | Chỉ cho thành phần vô hiệu (WCAG miễn trừ). Không dùng cho chữ gợi ý. |
| Border / Surface | 1,31 | Chỉ cho viền trang trí. Viền điều khiển dùng Border Strong. |
| Sky / Primary Container | 2,05 | Cấm cho thanh tiến độ. Dùng Primary trên Primary Container. |

---

## Phụ lục B – Thay đổi so với bản 1

Bản 1 là nội dung nguyên văn của file Word. Các điểm dưới đây đã đổi hoặc thêm ở bản 2.

| # | Mục | Bản 1 | Bản 2 | Lý do |
|---|---|---|---|---|
| 1 | 2.2 | Viền ô nhập dùng Border `#D9E2EC` | Thêm **Border Strong `#728AA3`** cho viền mọi điều khiển bấm được | Border trên trắng chỉ 1,31:1; Lab yêu cầu ≥ 3:1 cho điều khiển |
| 2 | 2.2 | Chữ gợi ý trong ô nhập dùng Text Disabled | Dùng Text Secondary; Text Disabled chỉ cho thành phần vô hiệu | 2,15:1 không đạt 4,5:1 |
| 3 | 2.1, 8 | Mục sidebar đang chọn: chữ Primary trên Primary Container | Chữ Primary Dark | 4,27:1 không đạt 4,5:1 |
| 4 | 2.1 | Sky: "không đặt chữ nhỏ màu trắng", dùng cho thanh tiến độ | Cấm mọi chữ trắng trên Sky; Sky chỉ trang trí; thanh tiến độ dùng Primary | 2,30:1 không đạt cả ngưỡng chữ lớn |
| 5 | 2.2 | Chưa có | Thêm token Scrim | Lớp phủ dialog và bottom sheet chưa được quy định |
| 6 | 3 | Dòng lỗi dưới ô nhập, dòng phụ thẻ sinh viên: Caption 12 | Body 14 | Lab yêu cầu chữ nội dung ≥ 14 sp |
| 7 | 3, 6 | Chữ trong chip: Caption Bold 12 | Label 14 | Như trên; khớp mặc định của chip Material 3 |
| 8 | 3 | Chưa có | Quy tắc cỡ chữ hệ thống, kiểm tra ở 130% | Chữ lớn không được làm vỡ bố cục |
| 9 | 1 | Chỉ có frame 360 × 800 | Thêm frame kiểm tra 412 × 915, quy tắc co giãn và vùng an toàn | Lab yêu cầu kiểm tra 360 và 412 dp |
| 10 | 4 | "Cao tối thiểu 48 px" | Vùng chạm tối thiểu 48 × 48, cách nhau 8; chip và icon nhỏ được bọc vùng chạm | Lab yêu cầu 48 × 48 dp; chip cao 32 vi phạm quy định cũ |
| 11 | 4 | Một kiểu bóng | Ba cấp `elevation/0–2` | Lab yêu cầu token elevation |
| 12 | 4 | Chưa có | Token chuyển động và quy tắc giảm chuyển động | Thiếu quy định cho chuyển cảnh loading → result |
| 13 | 2–4 | Chỉ có tên màu và kiểu chữ | Thêm tên token cho màu, chữ, khoảng cách, bo góc, độ nổi | Lab chấm variables/styles; cần tên thống nhất giữa Figma và Flutter |
| 14 | 6 | Chưa có component Đang tải và Màn lỗi | Thêm cả hai | Lab yêu cầu đủ 9 nhóm component, gồm Loading và Error |
| 15 | 6 | Chưa có bảng trạng thái | Thêm mục 6.2: trạng thái bắt buộc và cách thể hiện | Lab yêu cầu states cho component |
| 16 | 6, 7 | Chip lọc đang chọn, banner hạn chót, chip trạng thái phân biệt bằng màu | Thêm dấu tích, icon và dòng chữ; mỗi nhóm trạng thái có icon cố định | Lab yêu cầu không chỉ dùng màu |
| 17 | 6 | Nút vô hiệu chỉ đổi màu | Luôn kèm dòng giải thích lý do | Người dùng cần biết vì sao không bấm được |
| 18 | 6 | Snackbar cho kết quả thao tác | Chỉ cho kết quả thành công; lỗi dùng dòng lỗi hoặc banner | Snackbar tự ẩn sau 4 giây, người dùng có thể bỏ lỡ lỗi |
| 19 | 6 | Dialog rộng 312 px | Cách mép 24 px mỗi bên, tối đa 400 | Để co giãn đúng ở 412 |
| 20 | 8 | Chưa có | Trạng thái của bảng, bộ lọc đang áp dụng, quy tắc bàn phím và focus | Web cần đủ trạng thái và dùng được bằng bàn phím |
| 21 | 10 | Các trang: 00 Cover · 01 Foundations · 02 Components · 03–05 · 99 Nháp | File nộp Lab đúng 6 trang theo đề; web và nháp ở file riêng | Kịch bản demo của Lab kiểm tra đúng 6 trang theo thứ tự |
| 22 | 12 | Bảng ánh xạ component | Tách hai bảng token → Flutter và component → widget, thêm ghi chú accessibility | Lab yêu cầu đúng hai bảng này trong handoff |
| 23 | 13 | Chưa có | Ánh xạ sang Tailwind + shadcn/ui cho web | Đề xuất; cần nhóm xác nhận thư viện web |

---

## Phụ lục C – Thay đổi so với bản 2

Bản 3 ghi lại các quy ước rút ra từ ba vòng tinh chỉnh ngày 12/10/2026. Cột "Nguồn" là số mục trong [ai-design-log.md](ai-design-log.md) và, nếu có, mã trong [design-decisions.md](design-decisions.md) Phần B.

| # | Mục | Bản 2 | Bản 3 | Nguồn |
|---|---|---|---|---|
| 1 | 2.1, 3 | Sky dùng cho nền header trang chủ | Sky chỉ còn cho minh họa; lời chào là chữ Display trên nền trang | Vòng 1, 1.1 (B1) |
| 2 | 3, 6.1 | Tiêu đề banner hạn chót kiểu Label 14 | Kiểu Subtitle 16; banner là thông tin tĩnh, không bấm được | Vòng 1, 1.2 và 1.3 (B4) |
| 3 | 6.1 | Chưa có | Dòng điều hướng: nội dung bấm được để sang màn hình khác dùng dòng có mũi tên, không dùng banner | Vòng 1, 1.3 (B4); vòng 2, 2.6 (B9) |
| 4 | 6.1 | Chưa có | Thẻ việc cần làm ngay, có nút Primary duy nhất của trang chủ | Vòng 1, 1.4 |
| 5 | 6.1 | Chip trạng thái chưa quy định vị trí | Trên thẻ và ở đầu màn hình chi tiết: cùng hàng với tên nhóm, căn phải, rớt xuống dưới tên khi không vừa | Vòng 1, 1.6 (B7) |
| 6 | 6.1 | Nút phụ không có icon; đăng xuất dùng nút nguy hiểm | Nút phụ có icon tùy chọn; đăng xuất dùng nút phụ có icon `logout` | Vòng 1, 1.7 (B11) |
| 7 | 6.1 | Thẻ số liệu chỉ quy định cho web | Thêm thẻ số liệu mobile: nhãn Body 14, các thẻ cao bằng nhau | Vòng 1, 1.5; vòng 3, 3.2 (B12) |
| 8 | 6.1 | Thẻ sinh viên không có hành động | Trưởng nhóm thấy nút phụ "Gửi lời mời" trên thẻ | Vòng 2, 2.1 (B2) |
| 9 | 6.1 | Bottom sheet: "nút chính nằm cuối" | Sheet không cao hơn màn hình trừ 48 px; nút nằm trong thanh cố định phía trên vùng hệ thống | Vòng 2, 2.3 (B3) |
| 10 | 6.1 | Thẻ nhóm luôn có thể có chip trạng thái | Người ngoài nhóm không thấy chip trạng thái mà thấy "n/6 thành viên · còn k chỗ"; nhóm 6/6 không nằm trong danh sách tuyển | Vòng 2, 2.4 (B6) |
| 11 | 7 | Chưa có | Tab "chờ" chỉ chứa mục còn việc phải làm; mục đã thu hồi, hết hạn, bị từ chối vào tab "Đã đóng" | Vòng 2, 2.5 (B9) |
| 12 | 6.2 | Chỉ nút vô hiệu có dòng nêu lý do | Màn hình không có hành động cho người xem cũng ghi lý do | Vòng 2, 2.2 |
| 13 | 8 | Chưa có | Trang duyệt: căn cứ nằm trên và trong màn hình đầu; dòng có thay đổi đứng đầu danh sách | Vòng 2, 2.7 (W3) |
| 14 | 1.1, 6.1 | Dòng phụ: 1 dòng | Dòng "MSSV · chuyên ngành" của dòng thành viên tối đa 2 dòng; không lặp chữ của chip; nhãn "Trưởng nhóm" được rớt dòng | Vòng 3, 3.1 (B8) |
| 15 | 6.2, 7 | Chưa quy định thẻ có hành động bị khóa | Không làm mờ cả thẻ; thêm trạng thái "Đã đủ" vào nhóm Neutral | Vòng 3, 3.3 (B13) |
| 16 | 8 | Chưa quy định chiều rộng cột trạng thái | Cột chứa chip rộng tối thiểu bằng chip dài nhất cộng 24 px | Vòng 3, 3.4 (W1) |
| 17 | 11 | Chưa có | Thêm 3 mục kiểm tra: không cắt thông tin ra quyết định, không làm mờ thẻ, nút chính luôn nhìn thấy | Vòng 2 và 3 |
| 18 | 12.2 | Chưa có | Thêm ánh xạ `OutlinedButton.icon`, `ListTile`, `Wrap` và cách dựng bottom sheet có thanh nút cố định | Vòng 1 và 2 |
| 19 | Phụ lục A.1 | "Text Primary / Sky – Chữ trên header trang chủ" | Đổi thành "Chữ đặt trên mảng minh họa màu Sky"; số đo giữ nguyên | Vòng 1, 1.1 |

Không có token màu, kiểu chữ hay khoảng cách nào được thêm hoặc đổi giá trị ở bản 3, nên bảng tương phản ở Phụ lục A và các bảng ánh xạ token ở mục 12.1 và 13 giữ nguyên.
