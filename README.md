# EXE Team Matching System

Ứng dụng ghép nhóm môn EXE101 / EXE201 tại Đại học FPT, thay cho file Google Sheets nhập tay (~220 nhóm, ~1.250 sinh viên mỗi kỳ).
Sinh viên và giảng viên dùng **Mobile App (Flutter)**; Bộ môn EXE dùng **Web App (React)**.

- **Figma:** https://www.figma.com/design/tX0K0MdkTKt5wJcvWnPbxr
- **Môn:** PRM323 Lab 2 – Thiết kế UI/UX có hỗ trợ AI · PRN232 (backend)

## Artifact Lab 2 (`docs/ux/`)

| Artifact | File | Slide |
|---|---|---|
| Persona, problem statement, chỉ số thành công | [persona.md](docs/ux/persona.md) | 02, 02b |
| Information architecture, 3 user flow | [user-flow.md](docs/ux/user-flow.md), [ảnh Figma 01](docs/ux/user-flow-figma-01.png) | 03, 04 |
| Design system, quy ước, token | [DESIGN.md](docs/ux/DESIGN.md) | 09, 10 |
| AI log: prompt, phản biện, quyết định, ảnh trước/sau | [ai-design-log.md](docs/ux/ai-design-log.md), [ai-log-truoc-sau/](docs/ux/ai-log-truoc-sau/) | 05, 06, 06b |
| Design decisions, accessibility | [design-decisions.md](docs/ux/design-decisions.md) | 07, 10 |
| Flutter handoff: 2 bảng ánh xạ, 6 mục cho mỗi màn hình | [flutter-handoff.md](docs/ux/flutter-handoff.md) | 12, 12b |
| Danh sách màn hình (mobile A01–L06, web W01–W26) | [EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx](docs/ux/EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx) | 03 |
| Context cho AI coding assistant | [PROJECT_CONTEXT.md](docs/ux/PROJECT_CONTEXT.md) | – |

## Khác

| Thư mục / file | Nội dung |
|---|---|
| [AGENTS.md](AGENTS.md) | Quy tắc cho thành viên và AI agent: tài liệu phải đọc, ai phụ trách thư mục nào, quy tắc nhánh, commit, PR |
| [Google Sheet phân công task](https://docs.google.com/spreadsheets/d/17i2s3hWEHvSUk5tS-TCRMvDZ9nsaShP44PwQjivsZ7I/edit?usp=sharing) | Phân công task cho 5 thành viên (Backend, Frontend Web, Mobile) – bản chính; [bản xlsx](docs/EXE_Team_Matching_Phan_chia_task.xlsx) trong repo chỉ là bản chụp ban đầu |
| [docs/EXE_Database_Design.pdf](docs/EXE_Database_Design.pdf) | Thiết kế database (mỗi service 1 database) |
| [docs/lab2-slide/](docs/lab2-slide/) | Slide trình bày Lab 2 và hướng dẫn chấm |
| [docs/c4-architecture/](docs/c4-architecture/) | Sơ đồ kiến trúc C4 (C0, C1) |

Figma file gồm 6 page: `01 User Flow`, `02 Wireframe`, `03 Final UI`, `04 Design System`, `05 Components`, `06 Prototype`.
