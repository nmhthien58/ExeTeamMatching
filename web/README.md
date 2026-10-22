# Web (React) – Bộ môn, Sinh viên, Giảng viên, Mentor

```bash
cd web
cp .env.example .env.local   # VITE_API_BASE_URL, VITE_GOOGLE_CLIENT_ID
npm install
npm run dev        # http://localhost:3000
npm test           # Vitest
npm run lint
npm run build
```

Cấu trúc: `src/app` (router), `src/shared` (nền dùng chung – Thiên), `src/features/<mảng>` (màn hình của từng thành viên, AGENTS.md mục 2). Import tuyệt đối bằng `@/...`.

## Nền dùng chung (FE-02 → FE-07, FE-32)

| Cần gì | Dùng |
|---|---|
| Màu, chữ, bo góc, độ nổi | Class Tailwind theo token `docs/ux/DESIGN.md`: `bg-primary`, `text-text-secondary`, `bg-secondary-container`, `text-display`, `text-body`, `rounded-lg`, `shadow-elevation-1`… (khai báo ở `src/index.css`). Không viết mã màu trực tiếp |
| Gọi API | `api` trong `@/shared/api/apiClient` qua React Query (`useQuery` / `useMutation`). Tự gắn JWT, tự refresh khi 401. Lỗi là `ApiError` có `code` (ví dụ `round-closed`), `fieldErrors`, `traceId` |
| Người dùng, vai trò | `useAuth()` (`user`, `hasRole`, `logout`) |
| Tiêu đề trang + nút chính | `<PageHeader title="…" action={<Button>…</Button>} />` – khung hiển thị ở header 64 px |
| Nút, icon, chip trạng thái, banner, dialog | `Button` (`primary` / `outline` / `text` / `danger`, `loading`), `Icon` (Material Symbols Rounded), `StatusChip` (5 nhóm màu mục 7), `Banner`, `Dialog`, `Skeleton` trong `@/shared/components/ui` |
| Trạng thái màn hình (W26) | `EmptyState`, `NoResults`, `ErrorState` (Thử lại + mã tra cứu), `TableSkeleton`, `Forbidden`, `NotFound` trong `@/shared/components/States` |
| Bảng dữ liệu (FE-07) | `DataTable` – tìm kiếm (phím `/`), bộ lọc, sắp xếp theo cột (`meta.sortKey`), phân trang 20 dòng, đồng bộ URL, đủ trạng thái |
| Giai đoạn môn (rule 18) | `usePhases()`, `useMyCourse()`; bọc route Khám phá bằng `TeamFormationOnly` (hộp nhắc khi đợt đã đóng) |

## Khung theo vai trò

- `/admin/*` – W25 (sidebar 240 px). `/sv/*`, `/gv/*`, `/mentor/*` – khung FE-32 theo bề rộng: < 600 tab bar dưới, 600–1023 rail 80 px, ≥ 1024 sidebar 240 px. Tab đổi theo giai đoạn (Khám phá ↔ Dự án, tab Đề tài của giảng viên, tab Nhóm của mentor từ tuần 6).
- Danh sách route nằm ở `src/shared/layout/navigation.ts` kèm mã màn hình và người phụ trách. Mục nào chưa có màn hình thì hiện `ScreenPlaceholder`.
- Người làm màn hình viết route trong `src/features/<mảng>/routes.tsx` rồi thêm vào `src/app/featureRoutes.tsx` (theo khung `admin`, `student`, `lecturer`, `mentor`, đường dẫn tương đối). Route trùng đường dẫn với một mục điều hướng sẽ thay `ScreenPlaceholder` của mục đó. Mỗi mảng chỉ thêm dòng import và dòng route của mình để tránh conflict; không sửa `App.tsx`.
- Đã có màn hình thật (22/10/2026): W02, W20–W22 (`/admin`, `/admin/chua-co-nhom/*`); C01, C04–C06 (`/sv/du-an/*`, `/gv/du-an/:workspaceId/*`, `/mentor/du-an/:workspaceId/*`); S07, S18, S19, S30, S31.
- Trang đăng nhập `/dang-nhap` (W01 / A03) cho mọi vai trò; đăng nhập xong vào đúng khung theo `roles` trong token.
