# Triển khai lên VPS (BE-85)

Mục tiêu: web, API và file chạy bằng HTTPS trên tên miền công khai, mở được ở cửa sổ ẩn danh (PRM393 Final §7, PRN232 §3). Dùng lại đúng `deploy/docker-compose.yml` và chồng thêm `deploy/docker-compose.prod.yml`. File prod làm 3 việc:
- Caddy cấp chứng chỉ Let's Encrypt và làm reverse proxy.
- Chỉ mở cổng 80 và 443; mọi service khác chỉ nghe trong mạng Docker.
- Gắn tên miền vào CORS, link tải file và bản build web.

| Tên miền | Trỏ tới | Ghi chú |
|---|---|---|
| `WEB_DOMAIN` (vd. `exe.example.com`) | `web:80` (React) | Bộ môn, sinh viên, giảng viên, mentor |
| `API_DOMAIN` (vd. `api.exe.example.com`) | `gateway:8080` | REST, SignalR `/hubs/collab`, Swagger `/swagger`. Caddy trả 404 cho `/api/dev/*` |
| `FILES_DOMAIN` (vd. `files.exe.example.com`) | `object-storage:8333` (S3) | Link tải file ký sẵn |

> Các service .NET vẫn chạy `ASPNETCORE_ENVIRONMENT=Development`, vì một phần cấu hình nằm ở `appsettings.Development.json`. Vì vậy `/api/dev/token` (cấp JWT không cần Google) vẫn tồn tại bên trong mạng Docker. Caddy chặn đường này từ Internet, còn seed vẫn gọi thẳng `gateway:8080` nên vẫn chạy được. Muốn chuyển hẳn sang `Production` thì phải chuyển các cấu hình đó sang biến môi trường trước.

## 1. Chuẩn bị

- **VPS:** Ubuntu 22.04/24.04, **tối thiểu 4 vCPU, 8 GB RAM, 40 GB SSD**. Hệ thống có 8 service .NET, cộng Kafka, Postgres, Redis, SeaweedFS. Mở firewall cho cổng 22, 80 và 443 (cả TCP lẫn UDP).
- **Tên miền:** tạo 3 bản ghi **A** (`WEB_DOMAIN`, `API_DOMAIN`, `FILES_DOMAIN`) trỏ về IP của VPS. Đợi DNS cập nhật xong (`nslookup <tên miền>`) rồi mới chạy, nếu không Caddy sẽ không lấy được chứng chỉ.
- **Docker:**
  ```bash
  curl -fsSL https://get.docker.com | sh
  ```
  Sau đó chạy `sudo usermod -aG docker $USER`, rồi đăng nhập lại.

## 2. Lấy mã và cấu hình

```bash
git clone https://github.com/nmhthien58/ExeTeamMatching.git && cd ExeTeamMatching
git switch main            # hoặc develop khi demo bản mới nhất
cp deploy/.env.prod.example deploy/.env.prod
cp deploy/seaweedfs/s3.prod.example.json deploy/seaweedfs/s3.prod.json
```

- **`deploy/.env.prod`:**
  - Điền 3 tên miền và `ACME_EMAIL`.
  - Mọi mật khẩu và khóa (`POSTGRES_PASSWORD`, `*_DB_PASSWORD`, `JWT_SIGNING_KEY`, `S3_ACCESS_KEY`, `S3_SECRET_KEY`) phải khác giá trị dev. Sinh bằng `openssl rand -base64 32`.
  - `ADMIN_EMAIL` là email Google thật của Bộ môn.
  - Có web app Firebase (FE-46) thì điền thêm `VITE_FIREBASE_*`.
- **`deploy/seaweedfs/s3.prod.json`:** ghi đúng `S3_ACCESS_KEY` và `S3_SECRET_KEY` giống trong `.env.prod`. Khóa dev ở `s3.json` đã nằm trong repo công khai, không dùng ngoài máy dev.
- Hai file trên đều có trong `.gitignore`, không commit.

## 3. Chạy

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml --env-file deploy/.env.prod up -d --build
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml --env-file deploy/.env.prod ps
```

- Lần đầu build mất khoảng 10–15 phút. Đợi mọi container lên `healthy`.
- Chạy `docker logs exe-caddy` để xem Caddy đã lấy chứng chỉ chưa. Xong thì `https://API_DOMAIN/health` trả 200.
- Nạp dữ liệu demo nếu cần (seed BE-57, chạy trong mạng Docker):
  ```bash
  docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml --env-file deploy/.env.prod --profile seed run --rm seed
  ```

**Cập nhật bản mới:** `git pull` rồi chạy lại lệnh `up -d --build`. Dữ liệu nằm trong volume Docker nên không mất.

## 4. Việc trên Console (một lần)

- **Google Cloud → APIs & Services → Credentials → Web client:**
  - Thêm `https://WEB_DOMAIN` vào *Authorized JavaScript origins*.
  - Nếu Identity kiểm tra danh sách origin thì thêm cả vào đó.
- **Firebase → Authentication → Settings → Authorized domains:** thêm `WEB_DOMAIN`.
- **Firebase → Remote Config:** tạo tham số `web_notice` (FE-46) để hiện thông báo trên web.

## 5. Mobile trỏ về server

Build APK demo trỏ về Gateway công khai:

```bash
cd mobile
flutter build apk --release --dart-define=API_BASE_URL=https://API_DOMAIN --dart-define=APP_CHECK_DEBUG=true
```

APK cài tay nên dùng debug provider của App Check. Đăng ký debug token của thiết bị theo `docs/setup/google-sign-in.md` mục 7.

## 6. Kiểm tra sau khi triển khai

- [ ] `https://WEB_DOMAIN` mở được ở cửa sổ ẩn danh, đăng nhập Google được.
- [ ] `https://API_DOMAIN/swagger` hiện Swagger gộp; `https://API_DOMAIN/api/dev/token` trả **404**.
- [ ] Tải lên rồi tải xuống một file ở C06: link trỏ `https://FILES_DOMAIN/...` và mở được.
- [ ] Tin nhắn realtime (SignalR) chạy qua `wss://API_DOMAIN/hubs/collab`.
- [ ] Mobile (APK release) đăng nhập và tải dữ liệu từ `API_DOMAIN`.
- [ ] Từ máy ngoài, các cổng 5432, 9092, 6379, 8080 không truy cập được (`nc -zv <ip> 5432` phải thất bại).

## Ghi chú

- **Email:** Notification gửi qua Mailpit (SMTP giả trong mạng Docker), nên thư không ra ngoài. Muốn gửi thật thì đặt `Smtp__Host`, `Smtp__Port`, `Smtp__User`, `Smtp__Password` của một dịch vụ SMTP trong file override.
- **Kafka UI:** không chạy trên VPS (profile `debug`). Khi cần thì chạy `--profile debug up -d kafka-ui` và mở qua `ssh -L 8085:localhost:8085`. Lưu ý cổng đã bị gỡ khỏi host, nên phải thêm lại cổng `127.0.0.1:8085` trong override tạm.
- **Sao lưu:** `docker exec exe-postgres pg_dumpall -U postgres > backup.sql`.
