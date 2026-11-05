# Kiểm thử E2E bằng ARTEMIS (MB-58)

Danh sách ca và kết quả: [docs/test-plan.md](../docs/test-plan.md).

**Commit ARTEMIS đã pin:** `351ca8422f7b5b54e80a9c1ce03a222e02415b6b` (29/09/2026), khai báo trong `pyproject.toml`.

**Trạng thái:** code chưa chạy lần nào. Máy viết code không có Python, thiết bị Android và key Gemini.

## Chuẩn bị

1. Cài Python 3.12+ và `uv`.
2. Clone ARTEMIS đúng commit trên, đặt `GEMINI_API_KEY` trong `.env` của ARTEMIS, chạy `.\start.bat`. Tạo key miễn phí ở Google AI Studio, không commit.
3. Cắm điện thoại, bật USB debugging, rồi:

```bash
adb reverse tcp:8080 tcp:8080
```

```bash
adb reverse tcp:9000 tcp:9000
```

4. Chạy backend Docker có seed, cài APK theo mục 1 của test-plan, đăng nhập Google bằng tay.

## Chạy

```bash
cp .env.example .env
```

```bash
uv sync
```

```bash
uv run pytest --runs 3
```

Ca đánh dấu `pending` bị bỏ qua cho tới khi thêm tên vào `ENABLED_PENDING`, ví dụ `ENABLED_PENDING=MB-53,MB-56`.

Kết quả từng lần chạy ghi ở `evidence/artemis/results.csv`, kèm `trace_id`. Trace và video xem ở console ARTEMIS `http://localhost:8000`, tải về `evidence/artemis/` để nộp.

## Phiên khám phá

Đặt `ARTEMIS_PROFILE=pro`, chạy một phiên 15–30 phút từ console ARTEMIS, ghi vấn đề vào mục 6 của test-plan.
