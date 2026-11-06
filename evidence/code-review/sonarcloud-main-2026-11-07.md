# SonarCloud – lần quét nhánh `main` ngày 07/11/2026

Lần quét `main` đầu tiên sau 10 ngày (trước đó bị hủy vì vượt hạn mức 50.000 dòng của gói miễn phí; đã thêm `.sonarcloud.properties` và dọn dự án cũ). Quality Gate không đạt: Security Rating E, Reliability Rating D trên code mới. 11 vấn đề, quyết định từng cái:

| # | File | Vấn đề (mức) | Quyết định | Lý do |
|---|---|---|---|---|
| 1 | `web/src/index.css` L115 (2 vấn đề) | `background` ghi đè `background-image`, `background-attachment` (Reliability, High) | **Sửa** | Lỗi thật: hai quầng sáng nền của FE-49 bị xóa. Đổi thành `background-color` |
| 2 | `web/src/features/lecturers/rules.ts` L5 | Regex email có thể backtracking chậm (Reliability, Medium) | **Sửa** | Viết lại phần tên miền theo từng nhãn tách bằng dấu chấm, chạy tuyến tính; vẫn nhận `a@fpt.edu.vn`, chặn `a@b`, `a@b..c` |
| 3 | `web/Dockerfile` L10 | Dùng `ARG` cho secret `VITE_FIREBASE_API_KEY` (Security, Blocker) | **Báo nhầm (Safe)** | API key web của Firebase là công khai, luôn nằm trong bundle JS; bảo vệ bằng App Check và giới hạn key, không bằng việc giấu key |
| 4 | `deploy/seed/seed-demo.mjs` L27, L42 (3 vấn đề) | SSRF, API traversal, log injection từ input chưa lọc (Security, High/Medium/Low) | **Chấp nhận** | Script tạo dữ liệu demo, chỉ thành viên chạy tay trên máy mình; input là biến môi trường của chính người chạy, không chạy trên server |
| 5 | `web/Dockerfile` L5 | `npm ci` thiếu `--ignore-scripts` (Security, Medium) | **Chấp nhận** | esbuild (Vite) cần script cài đặt để lấy bản đúng nền tảng; phiên bản khóa trong `package-lock.json` |
| 6 | `web/Dockerfile` L18 | Image nginx chạy bằng root (Security, Low) | **Chấp nhận cho demo** | Chỉ phục vụ file tĩnh; đổi sang image không root phải đổi cổng trong Docker Compose – để sau |
| 7 | `StudentChangeRequestCreatePage.tsx` L75, `profile/components.tsx` L60 | Label thiếu chữ (Reliability, Medium) | **Báo nhầm** | Label có chữ ("Tên đề tài mới", tên kỹ năng) nằm trong `<span>` con; trình đọc màn hình đọc được |
| 8 | `client-errors/ClientErrorDrawer.tsx` L42 | `tabIndex` trên phần tử không tương tác (Reliability, Medium) | **Cố ý (Won't fix)** | Vùng stack trace cuộn được phải nhận Tab cho người dùng bàn phím (WCAG 2.1.1); đã có `role="region"`, `aria-label` |

Sửa ở PR `fix/sonarcloud-main-findings`. Các vấn đề "Báo nhầm / Chấp nhận / Cố ý" được đánh dấu trên SonarCloud (Safe, Accepted, False positive) kèm lý do như bảng trên.
