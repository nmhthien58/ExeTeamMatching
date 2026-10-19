# Web quản trị (React) – Bộ môn EXE

```bash
cd web
cp .env.example .env.local
npm install
npm run dev        # http://localhost:3000
npm run build
npm run lint
```

Cấu trúc: `src/app` (router, khung), `src/shared` (API client, component, layout dùng chung – Thiên), `src/features/<mảng>` (màn hình của từng thành viên, xem AGENTS.md mục 2). Import tuyệt đối bằng `@/...`.
