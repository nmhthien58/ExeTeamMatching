# Gap review – Lab 2, Lab 3, Final (PRM393)

> MB-57, 05/11/2026. Đối chiếu từng yêu cầu với hiện trạng repo (`develop`). Kế hoạch và mã task: [task-changes-2026-11-04.md](task-changes-2026-11-04.md). Đề: `docs/PRM393 - Lab 3 …pdf`, `docs/PRM393 - Final Assignment …pdf`.
>
> Giảng viên PRM393 đã cho dùng backend C# (.NET), nên phần "Firebase làm backend" của Final (Firestore, Security Rules + test Emulator, `packages/core/`, không có server riêng) **không áp dụng**; các mục còn lại giữ nguyên.
>
> ✅ đã đóng · 🟡 đang làm · ⬜ chưa làm · 👤 cần Thiên làm tay (Console, thiết bị, tài khoản)

## Lab 2 → app (gap của bản thiết kế khi build)

| Mục | Trạng thái | Cách đóng / ghi chú |
|---|---|---|
| Mọi màn Final UI có màn Flutter | ✅ | [design-traceability.md](design-traceability.md) mục 1; L02, L03 bỏ theo đổi nghiệp vụ 20/10; tab Lịch giảng viên còn placeholder ⏳ |
| 3 luồng F1–F3 chạy đủ, có nhánh lỗi | ✅ | design-traceability mục 2; F1 thử trên máy thật 05/11 |
| Design system thành code (token → ThemeData, component → widget) | ✅ | design-traceability mục 3; không hardcode màu / cỡ chữ (flutter-handoff §5 checklist, MB-36) |
| Màn có loading / empty / error + Thử lại | ✅ | `ViewState` + `states.dart`; checklist §5 cho mọi màn (MB-36, #134, #138) |
| Accessibility: chạm ≥ 48 dp, chữ ≥ 14 sp, 360 / 412 dp, chữ 130% | ✅ | test `accessibility_*_test.dart` |
| App khác Figma được ghi lại | 🟡 | design-traceability mục 4 (D1–D9). **Còn:** vẽ lại A03, header các tab, SkillSlider trong Figma (bridge Figma, cần Thiên mở plugin) 👤 |

## Lab 3

| Yêu cầu | Trạng thái | Bằng chứng / việc còn lại |
|---|---|---|
| §4 `design-traceability.md`, `architecture.md` | ✅ | đã có; architecture mục 5 đủ 47 ViewModel (#151) |
| §5 MVVM: widget không gọi SDK / HTTP, nghiệp vụ ngoài `build()`, state model rõ, service inject | ✅ | MB-48 (#146), MB-49 (#148, #149); 492 test gồm unit test ViewModel với fake |
| §6 Authentication: Google, route theo phiên, đăng xuất xóa dữ liệu, hủy có thông báo | ✅ | MB-47 (#142); user có trên Console → Authentication. **Còn:** ảnh trước / sau đăng nhập cho báo cáo 👤 |
| §6 Cloud Storage: file tạo ra, tải lên `reports/{uid}/`, tiến độ, URL, `storage.rules` | ⬜ | MB-53 (Nguyên); kiểm tra trước gói Blaze 👤 |
| §6 Cloud Messaging: 3 trạng thái app, xin quyền Android 13+, lưu vào A06, chạm điều hướng | ⬜ | MB-52 (Thành) + BE-84 (gửi push từ Notification) |
| §6 Analytics: ≥ 7 sự kiện, kế hoạch, user ID, không PII | 🟡 | [analytics-plan.md](analytics-plan.md); 15 sự kiện có service, 12 đã gắn vào ViewModel (#147, #151). **Còn:** DebugView từng sự kiện 👤 |
| §6 Crashlytics: non-fatal từ luồng lỗi thật, test crash, user ID, custom key | 🟡 | `CrashReporter`, key `role`, `course` (#147, sửa vòng phụ thuộc ở #150). **Còn:** nút thử crash (MB-54), bật Crashlytics trên Console, ảnh dashboard 👤 |
| §6 Remote Config: ≥ 2 tham số đổi hành vi, mặc định trong app, đổi không cần build | 🟡 | `suggestions_max_items` (S07, #151), `ai_idea_enabled` (MB-55). **Còn:** tạo 4 tham số trên Console theo `firebase/remote-config-defaults.json`, ảnh trước / sau 👤 |
| §3 Khu demo Firebase (thường ở Hồ sơ / Cài đặt) | ⬜ | MB-54 (Nam) |
| §7 ARTEMIS: test plan ≥ 10 ca, `e2e_test/`, 3 lần / ca, phiên khám phá, Semantics label | ⬜ | MB-58 (Nguyên) |
| §8 AI code review: ≥ 5 phát hiện, quyết định, ≥ 3 fix | ⬜ | MB-59 (Thiên); SonarCloud đã chạy trên mọi PR, dùng làm nguồn phát hiện |
| §9 README: phiên bản, AI tools, video, bản đồ deliverable | 🟡 | phiên bản Flutter / Firebase đã có (MB-47). **Còn:** AI tools, link video, bản đồ deliverable (MB-60) |
| §9 `evidence/`, report 8–12 trang, slide, video 5–10 phút | ⬜ | MB-60 👤 |

## Final

| Yêu cầu | Trạng thái | Bằng chứng / việc còn lại |
|---|---|---|
| Mobile hoàn thiện Lab 2 + Lab 3, gap review đóng | 🟡 | file này; theo bảng Lab 3 ở trên |
| Dữ liệu thật (không mock) + inbox thông báo realtime | ✅ / 🟡 | dữ liệu từ API .NET; A06 có, push realtime chờ MB-52 / BE-84 |
| Web admin: đăng nhập + từ chối sai vai trò, quản lý 1 thực thể, hàng chờ duyệt, dashboard ≥ 3 số + 1 biểu đồ, form thông báo | 🟡 | màn có sẵn (W01, W11/W27, W17/W18, W02, W23). **Còn:** checklist + ảnh từng màn (FE-48, Nguyên) |
| Web: cùng token Lab 2, layout màn rộng, mọi màn có trạng thái | ✅ | DESIGN.md mục 8; FE-31 kiểm thử web |
| Web: Analytics + Remote Config | 🟡 | FE-46: code đọc `VITE_FIREBASE_*`. **Còn:** tạo web app Firebase, điền `.env` 👤 |
| URL web công khai, mở được ở cửa sổ ẩn danh | 🟡 | BE-85: compose production + Caddy + `docs/setup/deploy.md`. **Còn:** thuê VPS / tên miền, chạy thật 👤 |
| App Check cho AI Logic | 🟡 | debug provider đã bật (MB-47). **Còn:** đăng ký debug token, bật enforce khi làm MB-55 👤 |
| Tính năng Gemini mức 2 (structured output, người dùng xác nhận), Remote Config, fallback, không PII, log 3 sự kiện | ⬜ | MB-55 (service) + MB-56 (S09, Cường); service Analytics đã có sẵn 3 sự kiện AI |
| Luồng xuyên vai trò (mobile ↔ web, realtime), `cross-role-flow.md`, đo độ trễ, chạy 3 lần trên bản triển khai | ⬜ | FE-47 (Thành), cần BE-85 + MB-52 |
| Test: ARTEMIS, checklist web, AI code review ≥ 3 | ⬜ | MB-58, FE-48, MB-59 |
| README, report 10–15 trang, slide 12–15, video 8–12 phút | ⬜ | MB-60 👤 |
| ~~Firestore, Security Rules, test Emulator, `packages/core/`~~ | – | Không áp dụng (dùng backend C#, được giảng viên cho phép) |
