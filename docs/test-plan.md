# Kế hoạch kiểm thử E2E (MB-58)

> PRM393 Lab 3 §7, Phụ lục B; Final §9. Công cụ: [Google ARTEMIS](https://github.com/google/artemis), không dùng Patrol. Mỗi ca là một hàm pytest trong `e2e_test/`, gửi một câu lệnh qua `ArtemisClient` và kiểm tra giá trị agent báo về.
>
> **Trạng thái (06/11/2026):** đã chạy trên thiết bị thật – 9/10 ca chạy được đạt 3/3 (TC-19 AI: app đã chạy đúng, trượt vì Gemini quá tải phía Google, F4–F5). Phiên khám phá đã chạy ngày 07/11/2026 (mục 6). Còn thiếu: các ca `pending`. Commit ARTEMIS pin ở `e2e_test/README.md`. Cột Kết quả ở mục 5 còn trống cho tới khi chạy trên máy thật. Đã đối chiếu với đề Lab 3 §7 (06/11/2026).

## 1. Môi trường

| Mục | Giá trị |
|---|---|
| Thiết bị | Điện thoại Android thật, cắm cáp, bật USB debugging |
| Backend | Railway (`gateway-production-6183.up.railway.app`), dữ liệu seed `SP90` (`deploy/seed`) |
| Kết nối | Mạng của điện thoại; không cần `adb reverse` |
| Bản app | APK release theo `docs/setup/railway.md` mục 8 (`FIREBASE_DEMO=true`, App Check debug token dùng chung) |
| Tài khoản | Sinh viên chưa có nhóm theo `SEED_STUDENT_EMAIL`; trưởng nhóm EXE201 và giảng viên theo seed. Đăng nhập Google làm tay một lần trước khi chạy, ARTEMIS không nhập mật khẩu |
| Model | Profile Flash cho hồi quy, Pro cho phiên khám phá. Key Gemini đặt trong `e2e_test/.env` (đã gitignore) |

## 2. Luồng quan trọng và màn hình

| Luồng | Màn hình | Ca |
|---|---|---|
| Đăng nhập, phiên | A01, A03, A04 | TC-01, TC-02 |
| F1 – Sinh viên tham gia nhóm | S01, S05, S07, S08, S09, S12 | TC-03, TC-04, TC-05 |
| F2 – Trưởng nhóm EXE201 đổi thành viên | S15, S26, S27, S28, S29 | TC-06, TC-07 |
| F3 – Giảng viên | L01, L04, L05, L07 | TC-08, TC-09 |
| Các tab của từng khung | S01, S05 / C01, S15, C02, A06 (Hồ sơ S02 mở từ avatar) | TC-10 |
| Analytics | S08 → sự kiện `view_team` trong Logcat | TC-21 |
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
| TC-10 | Chính | S01 | Bấm lần lượt mọi tab | Đúng 5 tab, có Tin nhắn, không màn nào lỗi | – |
| TC-11 | Chính | S23 | Mở một giảng viên trong danh sách | S24 hiện tên giảng viên và chuyên môn | – |
| TC-12 | Chính | C01 của nhóm có việc | Bấm "Xuất báo cáo PDF" | Tiến độ, rồi "Đã lưu báo cáo" và nút mở file | MB-53 |
| TC-13 | Lỗi | C01, tắt mạng trước khi xuất | Bấm "Xuất báo cáo PDF" | Thông báo lỗi tiếng Việt và nút thử lại, app không đóng | MB-53 |
| TC-14 | Chính | Avatar → S02 → A07, bản `FIREBASE_DEMO` | Đọc mục Remote Config đang dùng | Số nhóm gợi ý tối đa khớp giá trị trên Console | – |
| TC-15 | Android gốc | S08 mở từ S05 | Bấm nút Back hệ thống | Quay về S05, danh sách vẫn giữ vị trí | – |
| TC-16 | Android gốc | Cài mới, Android 13+ | Mở app, chạm Cho phép ở hộp xin quyền thông báo | Hộp đóng, app vào màn chính | MB-52 |
| TC-17 | Chính | Avatar → S02 → A07 | Bấm Đăng xuất, xác nhận | Màn đăng nhập A03 | – |
| TC-18 | Chính | Bộ môn vừa duyệt yêu cầu trên web | Mở A06 trên mobile | Thông báo duyệt, chạm vào mở S28 trạng thái Đã duyệt | MB-52, FE-47 |
| TC-19 | Chính | S09, cờ `ai_idea_enabled` bật | Bấm "Gợi ý bằng AI", nhập mô tả, dùng gợi ý | Ý tưởng điền sẵn có nhãn "Gợi ý của AI" | MB-55, MB-56 |
| TC-20 | Lỗi | S09, cờ `ai_idea_enabled` tắt | Mở phần ý tưởng | Không có nút AI, vẫn nhập tay được | Tắt cờ trên Console |
| TC-21 | Analytics | S05 | Mở nhóm đầu tiên | Tên nhóm ở S08; Logcat (debug Analytics bật) có `view_team` | – |

Tổng 21 ca, gồm 6 ca lỗi (TC-02, 05, 07, 09, 13, 20) và 2 ca Android gốc (TC-15, 16). 1 ca kiểm tra sự kiện Analytics (TC-21). Đề yêu cầu tối thiểu 10 ca, 1 ca lỗi, 1 tương tác Android gốc, 1 ca kiểm tra Analytics.

Cột Chờ: điều kiện phải chuẩn bị trước, ứng với marker `pending` (xem `e2e_test/README.md`). Không phụ thuộc thứ tự: mỗi ca tự bắt đầu từ điểm ghi ở cột Điểm bắt đầu.

## 4. Cách chạy

```bash
cd e2e_test
cp .env.example .env
uv sync
uv run pytest --runs 3
```

Mỗi ca chạy 3 lần. Kết quả từng lần ghi ở `evidence/artemis/results.csv`, kèm `trace_id` để mở trace trên console ARTEMIS. Ca chờ task khác có marker `pending`, bật bằng biến `ENABLED_PENDING`.

## 5. Kết quả

Chạy ngày 06/11/2026 trên Samsung Galaxy A71 (Android 13), APK release backend Railway, ARTEMIS commit `351ca84`, profile Flash (`gemini-3.8-flash`). Lệnh: `uv run pytest --runs 3 -v` trong `e2e_test/`; bảng sinh bằng `uv run python summarize.py`; trace mở ở console ARTEMIS `http://localhost:8000` theo cột Trace. Mỗi lần chạy ghi ở `evidence/artemis/results.csv`.

| Ca | Lần 1 | Lần 2 | Lần 3 | Tỷ lệ đạt | Thời gian TB | Ghi chú |
|---|---|---|---|---|---|---|
| TC-01 | ✅ | ✅ | ✅ | 3/3 | 88 s | |
| TC-03 | ✅ | ✅ | ✅ | 3/3 | 70 s | |
| TC-04 | ✅ | ✅ | ✅ | 3/3 | 35 s | |
| TC-05 | ✅ | ✅ | ✅ | 3/3 | 68 s | Lượt 1 được 2/3 (F1); chạy lại sau khi sửa |
| TC-10 | ✅ | ✅ | ✅ | 3/3 | 65 s | Đủ 5 tab, có Tin nhắn |
| TC-14 | ✅ | ✅ | ✅ | 3/3 | 22 s | `suggestions_max_items = 5` khớp Console |
| TC-15 | ✅ | ✅ | ✅ | 3/3 | 76 s | Nút Back hệ thống (tương tác Android gốc) |
| TC-17 | ✅ | ✅ | ✅ | 3/3 | 134 s | Lượt 1 được 0/3 (F2, F3); chạy lại sau khi sửa. Đăng nhập lại qua hộp chọn tài khoản Google (tương tác Android gốc) |
| TC-19 | ❌ | ✅ | ❌ | 1/3 | 510 s | Lượt 06/11, xem F4 |
| TC-19 (07/11) | ❌ | ❌ | ❌ | 0/3 | 447 s | Sau khi đổi model: app hiện đúng "AI đang quá tải…", Gemini trả 503 – xem F5 |
| TC-21 | ✅ | ✅ | ✅ | 3/3 | 34 s | Logcat có `view_team` (`evidence/artemis/logcat/`) |

Các ca còn lại (TC-02, 06–09, 11–13, 16, 18, 20) cần điều kiện riêng (marker `pending`, bảng ở `e2e_test/README.md`) và chưa chạy ở lượt này để giữ chi phí model (~200 nghìn đồng cho cả bộ).

### Ca dưới 3/3: nguyên nhân và cách sửa

| # | Ca | Hiện tượng | Nguyên nhân | Nhóm | Sửa |
|---|---|---|---|---|---|
| F1 | TC-05 lần 1 | Agent đã trả JSON đúng nhưng test báo "không trả JSON" | Bộ test đọc bước cuối của phiên trước khi server ARTEMIS lưu xong | Timing (bộ test) | `final_report` đọc lại tối đa 6 lần, cách 3 s |
| F2 | TC-17 lần 1 | Agent báo tab là `1` | Câu lệnh "báo tab đang chọn" mơ hồ (số thứ tự hay tên) | Câu lệnh mơ hồ | Đổi thành "tên chữ in dưới icon, ví dụ Trang chủ, không dùng số thứ tự" |
| F3 | TC-17 lần 2, 3 | Đăng nhập lại báo "Không dùng được trên ứng dụng này" | Câu lệnh "chọn tài khoản đầu tiên" chọn tài khoản Bộ môn; Identity chặn đúng (`wrong-client`). Lỗi app đi kèm: bị từ chối thì app không đăng xuất Google nên lần sau không hiện hộp chọn tài khoản, kẹt tới khi xóa dữ liệu | Câu lệnh mơ hồ + **app** | Câu lệnh chọn theo `TEST_ACCOUNT_EMAIL`; app đăng xuất cả Google khi bị từ chối, hiện cả lý do ("Tài khoản Bộ môn chỉ đăng nhập trên web."), có test |
| F4 | TC-19 lần 1, 3 | "Không tạo được gợi ý" lặp lại, agent thử tới hết giờ | Logcat: App Check không đổi được debug token dùng chung (HTTP 403, rồi `Too many attempts`) – token chưa đăng ký trên Console | Cấu hình (không phải code) | Đăng ký token "APK demo dùng chung" trên Console; app phân loại lỗi App Check thành `denied` với câu riêng. Sau khi đăng ký vẫn `unknown`: xem F5 |
| F5 | TC-19 (06/11 còn lại, 07/11 cả 3 lần) | `unknown`, sau đó "AI đang quá tải…" | Gọi thẳng API: App Check đổi token thành công; Gemini trả 404 vì `gemini-2.5-flash` không còn cấp cho dự án mới. Sau khi đổi sang `gemini-3.8-flash` (PR #194 + Remote Config), Gemini trả 503 "high demand" | **Cấu hình** (model ngừng cấp), rồi **Timing** (dịch vụ ngoài quá tải) | Đổi model ở code và Remote Config; lỗi 503 hiện câu "AI đang quá tải…" thay vì `unknown`. Chạy lại khi Gemini hết quá tải |

## 6. Phiên khám phá

Một phiên 15–30 phút, profile Pro, bắt đầu từ S01 với tài khoản sinh viên. Vấn đề tìm được ghi ở bảng dưới.

| # | Màn | Vấn đề | Mức độ | Xử lý |
|---|---|---|---|---|
| E1 | Thử AI (A07) | Câu nhắc khi để trống ô mô tả bị cắt thành "…trước khi ..." | Trung bình | **App**, đã sửa: `errorMaxLines: 2` |
| E2 | Chỉnh sửa hồ sơ (S03) | Nút "Lưu" cuối trang bị thanh tab dưới che gần hết (phiên thử) | Cao | Báo Nam xác nhận trên máy |
| E3 | Khám phá / Chi tiết nhóm | Nhóm trạng thái "Nháp" vẫn ở "Nhóm đang tuyển" và bấm được "Xin tham gia" (phiên thử) | Trung bình | Báo Cường xác nhận đúng quy tắc hay lỗi |
| E4 | Khám phá, Tạo nhóm, Hồ sơ | Chữ tiếng Anh lẫn vào: tên domain trong danh mục, chữ "Domain", "Plan truyền thông" | Thấp | Dữ liệu danh mục và nhãn; chủ mảng quyết định |
| E5 | Tạo nhóm, Chỉnh sửa hồ sơ | Bộ đếm ký tự đọc "characters remaining" (chữ mặc định của Flutter) | Thấp | Chưa sửa: cần thêm bản địa hóa `vi` cho Material |
| E6 | Khám phá | Toast "Đã sao chép vào bộ nhớ tạm." khi nhập ô tìm kiếm | – | Không phải lỗi app: toast của Android khi ARTEMIS nhập chữ qua bộ nhớ tạm |
| E7 | Cài đặt | Mục "Demo Firebase" hiện cho người dùng (phiên thử) | – | Không phải lỗi: cố ý bật trong APK demo (`FIREBASE_DEMO=true`) để chấm Lab 3 |
| E8 | Trang chủ | "Nhóm còn thiếu 2 người" cạnh "2/6 thành viên · còn 4 chỗ" (phiên thử) | Thấp | Không mâu thuẫn (thiếu tới tối thiểu 4, còn chỗ tới tối đa 6), chữ dễ hiểu nhầm |

Phiên chính chạy ngày 07/11/2026, profile Pro, 17,9 phút, trace `ad42e826-cbb8-4620-8636-614274edbada`, qua 14 màn (Trang chủ, Khám phá, Bộ lọc, Chi tiết nhóm, Sinh viên tìm nhóm, Hồ sơ sinh viên, Nhóm, Tạo nhóm, Tin nhắn, Thông báo, Hồ sơ, Chỉnh sửa hồ sơ, Cài đặt, Thử AI). Phiên thử trước đó (6,8 phút, trace `1e0902c0-22f2-477a-a2c1-1bbacd0400c8`) dừng sớm nên không tính là phiên khám phá, nhưng vấn đề tìm được vẫn ghi ở trên. Lệnh: `uv run python explore.py`; báo cáo JSON ở `evidence/artemis/explore-*.json`.
