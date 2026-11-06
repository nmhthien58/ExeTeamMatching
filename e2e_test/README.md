# Kiểm thử E2E bằng ARTEMIS (MB-58)

Danh sách ca và kết quả: [docs/test-plan.md](../docs/test-plan.md).

**Commit ARTEMIS đã pin:** `351ca8422f7b5b54e80a9c1ce03a222e02415b6b` (29/09/2026), khai báo trong `pyproject.toml`.

**Trạng thái (06/11/2026):** code đã đối chiếu với `artemis-client` ở commit trên và với chữ thật trong app `develop` (tab Tin nhắn, avatar Hồ sơ, S09 AI, xuất PDF); cú pháp Python đã kiểm tra. **Chưa chạy trên thiết bị.**

## Chuẩn bị (một lần)

1. Python 3.12+ và `uv` (máy Thiên đã có).
2. Clone ARTEMIS đúng commit vào **đường dẫn ngắn** (Windows lỗi khi đường dẫn dài), ví dụ `C:\artemis`:

```bash
git -c core.longpaths=true clone https://github.com/google/artemis.git C:/artemis
```

```bash
git -C C:/artemis checkout 351ca8422f7b5b54e80a9c1ce03a222e02415b6b
```

3. Tạo key Gemini miễn phí ở Google AI Studio (https://aistudio.google.com/apikey). Chép `C:\artemis\.env.example` thành `C:\artemis\.env`, điền `GEMINI_API_KEY=`. Không commit key.
4. Cắm điện thoại, bật USB debugging. App gọi backend Railway nên **không cần** `adb reverse`.
5. Cài APK demo (backend Railway, `FIREBASE_DEMO=true`), đăng nhập Google bằng tay bằng **tài khoản test** sinh viên chưa có nhóm (`SEED_STUDENT_EMAIL`).

## Chạy server ARTEMIS (cửa sổ 1)

Không dùng `start.bat` nếu không muốn nó cài cấu hình MCP vào các IDE; chạy thẳng:

```bash
cd C:/artemis && uv sync && uv run python -m artemis ui --port 8000 --no-open
```

Console xem trace, video: http://localhost:8000

## Chạy bộ test (cửa sổ 2, thư mục `e2e_test`)

```bash
cp .env.example .env
```

Trong `.env`: `ADB` trỏ tới adb.exe nếu lệnh `adb` không có trong PATH (máy Thiên: `C:/Users/Thien/AppData/Local/Android/Sdk/platform-tools/adb.exe`).

```bash
uv sync
```

```bash
uv run pytest --runs 3 -v
```

```bash
uv run python summarize.py
```

`summarize.py` in bảng kết quả (tỷ lệ đạt, thời gian trung bình, trace) để dán vào mục 5 của test-plan. Từng lần chạy ghi ở `evidence/artemis/results.csv`; Logcat của ca Analytics ở `evidence/artemis/logcat/`.

### Ca cần điều kiện riêng (marker `pending`)

Bỏ qua mặc định; bật bằng `ENABLED_PENDING` (tên ngăn cách dấu phẩy) khi đã chuẩn bị điều kiện:

| Tên | Ca | Chuẩn bị |
|---|---|---|
| `sinh viên đã có nhóm` | TC-11, TC-12, TC-13 | Đăng nhập tài khoản sinh viên đã có nhóm (hoặc chấp nhận lời mời Khởi Hành), chạy riêng sau các ca chưa có nhóm |
| `tài khoản giảng viên` | TC-08, TC-09 | Đăng nhập tài khoản giảng viên |
| `đợt đổi nhóm đang mở` | TC-06, TC-07 | Bộ môn mở đợt đổi nhóm EXE201, đăng nhập trưởng nhóm EXE201 |
| `Bộ môn duyệt yêu cầu trên web` | TC-18 | Duyệt một yêu cầu trên web ngay trước khi chạy |
| `cài mới app` | TC-16 | Gỡ app rồi cài lại (hộp xin quyền thông báo chỉ hiện lần đầu) |
| `cờ ai_idea_enabled tắt` | TC-20 | Tắt `ai_idea_enabled` trên Remote Config, chờ app nhận |
| `tài khoản Google ngoài danh sách` | TC-02 | Máy có thêm một Gmail không có trong hệ thống |

Ví dụ: `ENABLED_PENDING="sinh viên đã có nhóm" uv run pytest --runs 3 -k "tc11 or tc12 or tc13"`.

## Phiên khám phá

Đặt `ARTEMIS_PROFILE=pro`, chạy một phiên 15–30 phút từ console ARTEMIS (không có kịch bản, bắt đầu từ Trang chủ sinh viên), ghi vấn đề vào mục 6 của test-plan.
