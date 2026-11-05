# Review code bằng AI (MB-59)

> PRM393 Lab 3 §8, Phụ lục C; Final §9. Cập nhật 05/11/2026.
> Nguyên tắc: AI tìm, nhóm quyết định. Mỗi phát hiện có quyết định và lý do dựa trên yêu cầu của đề, quy tắc kiến trúc (Lab 3 §5), ảnh hưởng đo được hoặc ràng buộc thật.

## 1. Công cụ và phạm vi

| Công cụ | Phiên bản / gói | Chạy ở đâu | Dùng để |
|---|---|---|---|
| SonarCloud (SonarQube Cloud) | Gói Free, quality gate mặc định "Sonar way" | Tự động trên mọi PR vào `develop` (GitHub App) | Phân tích tĩnh: bug, lỗ hổng, security hotspot, code smell. Quality gate phải Passed trước khi merge |
| Claude Code (Claude Opus 5.5) | Desktop app | Đọc diff của từng PR | Review kiến trúc MVVM, bảo mật, trường hợp lỗi – phần SonarCloud không phân tích (SonarCloud Free không hỗ trợ Dart) |

PR đã review (đều vào `develop`): [#154](https://github.com/nmhthien58/ExeTeamMatching/pull/154), [#161](https://github.com/nmhthien58/ExeTeamMatching/pull/161), [#163](https://github.com/nmhthien58/ExeTeamMatching/pull/163) BE-87, [#164](https://github.com/nmhthien58/ExeTeamMatching/pull/164) MB-61, [#165](https://github.com/nmhthien58/ExeTeamMatching/pull/165) FE-50, [#166](https://github.com/nmhthien58/ExeTeamMatching/pull/166) BE-84, [#167](https://github.com/nmhthien58/ExeTeamMatching/pull/167) MB-53, [#168](https://github.com/nmhthien58/ExeTeamMatching/pull/168) MB-56, [#169](https://github.com/nmhthien58/ExeTeamMatching/pull/169) MB-55, [#171](https://github.com/nmhthien58/ExeTeamMatching/pull/171) MB-54, [#172](https://github.com/nmhthien58/ExeTeamMatching/pull/172) MB-52, [#173](https://github.com/nmhthien58/ExeTeamMatching/pull/173) MB-58, [#157](https://github.com/nmhthien58/ExeTeamMatching/pull/157) MB-24.

Kết quả thô của SonarCloud theo từng PR: [evidence/code-review/sonarcloud-pr-issues.md](../evidence/code-review/sonarcloud-pr-issues.md).

## 2. Bản ghi phát hiện

Loại: Security, Architecture (vi phạm Lab 3 §5), Bug, Accessibility (xếp vào Maintainability / Bug theo đề), Maintainability.

| ID | Công cụ | File:dòng | Loại | Phát hiện (tóm tắt) | Quyết định | Lý do | Commit sửa |
|---|---|---|---|---|---|---|---|
| CR-01 | SonarCloud `docker:S6472` (PR #154) | `web/Dockerfile:10` | Security | Dùng `ARG` để truyền bí mật (`VITE_FIREBASE_API_KEY`) | Rejected | Khóa API của web app Firebase **không phải bí mật**: nó được nhúng vào bundle JavaScript gửi tới mọi trình duyệt, chỉ để định danh project; quyền truy cập do App Check và Security Rules kiểm soát (Lab 3 §9 cũng cho commit `google-services.json` vì lý do này). Build arg không chứa mật khẩu hay khóa server nào | – |
| CR-02 | SonarCloud `docker:S6472` (PR #161) | `web/Dockerfile:15` (bản đầu của PR) | Security | Dùng `ENV` cho biến tên chứa `API_KEY` – giá trị bị ghi vĩnh viễn vào layer của image | Accepted | Khác CR-01: `ENV` lưu giá trị vào metadata của image, ai `docker inspect` cũng đọc được; nếu sau này đặt khóa thật vào đây sẽ lộ. Vite đọc trực tiếp `ARG` trong lệnh `RUN` nên `ENV` thừa. Quality gate của PR đổi từ Failed (Security Rating E) sang Passed | [54dae4e](https://github.com/nmhthien58/ExeTeamMatching/commit/54dae4e) (#161) |
| CR-03 | SonarCloud `typescript:S6845` (PR #165) | `web/src/features/client-errors/ClientErrorDrawer.tsx:40` | Accessibility | `tabIndex` trên phần tử không tương tác (`<pre>` stack trace) | Modified | Không bỏ `tabIndex` như AI đề xuất: `<pre>` là vùng cuộn (`max-h-96 overflow-auto`), bỏ thì người dùng bàn phím không cuộn đọc hết stack trace được (WCAG 2.1.1, quy tắc axe `scrollable-region-focusable`). Thay vào đó cho vùng có `role="region"`, tên "Stack trace" và viền focus nhìn thấy được | [496eb6e](https://github.com/nmhthien58/ExeTeamMatching/commit/496eb6e) |
| CR-04 | SonarCloud `typescript:S6845` (PR #165) | `web/src/features/client-errors/DailyErrorChart.tsx:44` | Bug (Accessibility) | `tabIndex` trên cột biểu đồ | Accepted | Kiểm tra thêm thấy lỗi nặng hơn AI báo: các cột nằm trong vùng `aria-hidden` và có `outline-none`, nên phím Tab dừng ở chỗ vô hình, không có tên, trình đọc màn hình không đọc được (WCAG 4.1.2, axe `aria-hidden-focus`). Số liệu theo ngày đã có ở bảng ẩn cho trình đọc màn hình ngay dưới biểu đồ, nên bỏ focus khỏi cột | [eeebe88](https://github.com/nmhthien58/ExeTeamMatching/commit/eeebe88) |
| CR-05 | Claude Code (PR #163) | `backend/Services/Identity/Identity.Api/Controllers/ClientErrors/ClientErrorRateLimit.cs:60` | Security | Giới hạn tần suất `POST /api/client-errors` (endpoint không cần đăng nhập) lấy khóa là **phần tử đầu** của `X-Forwarded-For` – phần client tự gửi | Accepted | Kẻ gửi spam chỉ cần đổi header mỗi lần (`X-Forwarded-For: 1.1.1.1`, `8.8.8.8`…) là có khóa mới, vượt giới hạn 30 báo cáo/phút và làm đầy bảng `client_error_reports`. Sửa: lấy địa chỉ do proxy tin cậy nối vào cuối header (`ClientErrors:RateLimit:TrustedProxyHops`, mặc định 1 = Gateway; 2 khi có Caddy / Railway phía trước). Test mới `Spoofed_forwarded_prefix_does_not_change_client_key` chứng minh đổi phần đầu không đổi khóa | [17f702f](https://github.com/nmhthien58/ExeTeamMatching/commit/17f702f) |
| CR-06 | Claude Code (rà toàn bộ widget sau khi merge #167, #171) | `mobile/lib/features/tasks/task_detail_screen.dart:196` | Architecture | Widget C05 gọi thẳng `collabApiProvider.downloadUrl(...)` trong `onTap` | Accepted | Vi phạm Lab 3 §5 ("no widget calls … directly; every external call goes through a service via the ViewModel"). Lỗi mạng khi bấm cũng không được bắt (Future lỗi không ai xử lý, người dùng không thấy gì). Chuyển vào `TaskDetailViewModel.openSubmission`, lỗi trả câu tiếng Việt cho SnackBar; thêm test với fake API | [90a2bda](https://github.com/nmhthien58/ExeTeamMatching/commit/90a2bda) |
| CR-07 | Claude Code (PR #172) | `mobile/lib/core/firebase/push_notifications.dart:199` (nhánh `feat/MB-52-fcm-mobile`), gọi từ `mobile/lib/app/app.dart:55` | Maintainability (UX) | Khi người dùng đã từ chối quyền thông báo, **mỗi lần app quay lại foreground** đều phát `permissionDenied` và hiện lại banner "Thông báo đang tắt" | Modified | Giữ cơ chế kiểm tra lại quyền khi resume (cần cho trường hợp người dùng vừa bật quyền trong Cài đặt), nhưng chỉ hiện banner khi trạng thái quyền **đổi** sang denied hoặc lần đầu trong phiên. Banner lặp lại mỗi lần mở app là làm phiền và người dùng sẽ tắt luôn app. PR chưa merge nên không sửa thay; chuyển đề xuất cho chủ PR (Thành) sửa trong nhánh MB-52 | Chờ chủ PR sửa |
| CR-08 | Claude Code | `mobile/lib/features/lecturer_profile/lecturer_profile_screen.dart:77`, `mobile/lib/features/settings/settings_screen.dart:41` | Maintainability | `FutureProvider` gọi API được khai báo trong file màn hình thay vì file ViewModel riêng | Rejected | Không vi phạm §5: widget không gọi service; provider (lớp ViewModel của Riverpod) gọi service qua `*ApiProvider` và test vẫn override được bằng fake. Chuyển file chỉ đổi vị trí, không đổi hành vi, lại đụng màn của Nam sát hạn nộp (ràng buộc phạm vi / thời gian). Ghi lại để làm khi refactor | – |

Tổng: 8 phát hiện (4 từ SonarCloud, 4 từ Claude Code), thuộc 4 loại: Security (CR-01, CR-02, CR-05), Architecture (CR-06), Accessibility / Bug (CR-03, CR-04), Maintainability (CR-07, CR-08). Quyết định: 4 Accepted, 2 Modified, 2 Rejected. Đã sửa 5 phát hiện, mỗi phát hiện một commit có mã CR.

## 3. Kiểm tra sau khi sửa

| Phát hiện | Cách kiểm tra |
|---|---|
| CR-02 | Quality gate SonarCloud của #161: Failed → Passed |
| CR-03, CR-04 | `npm test` (test W30 vẫn qua), `npm run lint`; thử Tab trên W30: focus đi từ bảng lọc vào vùng stack trace có viền, không dừng ở cột biểu đồ |
| CR-05 | `dotnet test backend/Tests/Identity.Tests`: 71 test qua, gồm 7 trường hợp `Client_key_uses_address_appended_by_trusted_proxy` và `Spoofed_forwarded_prefix_does_not_change_client_key` |
| CR-06 | `flutter analyze`, `flutter test test/tasks_view_model_test.dart` (test mới "mở bản đã nộp: lấy link đúng phiên bản, lỗi trả câu cho SnackBar") |

## 4. Công cụ AI đã dùng trong dự án

| Công cụ | Dùng để |
|---|---|
| Claude Code (Anthropic, Claude Opus 5.5) | Viết và sửa code backend, web, mobile; review PR (CR-05 → CR-08); soạn tài liệu (`docs/`); lập và chia task; điều khiển trình duyệt để cập nhật Google Sheet phân công |
| Google Antigravity | Viết code một số task theo prompt của trưởng nhóm |
| SonarCloud | Phân tích tĩnh và quality gate trên mọi PR (CR-01 → CR-04) |
| Gemini qua Firebase AI Logic | Tính năng gợi ý ý tưởng dự án trong app (MB-55, MB-56) |
| Gemini qua Google ARTEMIS | Agent kiểm thử E2E trên điện thoại (MB-58) |

Thành viên dùng thêm công cụ nào khác thì bổ sung vào bảng này trước khi nộp.
