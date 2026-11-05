# Triển khai backend lên Railway (BE-85) – bản khảo sát

> Bước 1 của BE-85, 05/11/2026. Chưa triển khai gì. Project Railway `exe-team-matching` (gói Hobby, hard limit 10 USD) đang trống, đã `railway link` trong repo.
> Mục tiêu: điện thoại và web Hosting gọi được backend từ mạng bất kỳ, không phụ thuộc laptop.

## 1. Các container hiện có và cách đưa lên Railway

RAM là ước tính (Docker trên laptop chưa bật nên chưa đo bằng `docker stats`): service .NET 8 lúc rảnh 120–250 MB.

| Container | Nguồn | RAM ước tính | Trên Railway | Public? |
|---|---|---|---|---|
| postgres | `postgres:16-alpine` + `deploy/db/*.sql` (tạo 6 DB, 6 user, seed danh mục) | 100–150 MB | **Service Docker riêng** build từ image `postgres:16-alpine` chép sẵn `deploy/db` vào `/docker-entrypoint-initdb.d`, có volume. Không dùng plugin Postgres của Railway vì script khởi tạo cần chạy y như local (user riêng cho từng service) | Không |
| kafka | `apache/kafka:3.8.0` (KRaft, 1 node) | 400–600 MB (heap 256–512 MB) | Service image, có volume; listener nội bộ `kafka.railway.internal:29092` | Không |
| redis | `redis:7-alpine` | 10 MB | Plugin Redis của Railway hoặc service image | Không |
| object-storage | `chrislusf/seaweedfs:3.97` (S3) | 80–150 MB | Service image, có volume; file `s3.json` sinh lúc khởi động từ biến môi trường (khóa mới, không dùng khóa dev đã công khai) | **Có** – link tải file ký sẵn |
| identity-api, academic-api, team-api, matching-grpc, notification, jobs, collab-api | `deploy/docker/dotnet.Dockerfile` | 7 × 150–250 MB | 7 service build từ repo, mỗi service đặt biến `PROJECT`, `APP_DLL` | Không |
| gateway | `deploy/docker/dotnet.Dockerfile` | 150–200 MB | Service build từ repo | **Có** – REST, SignalR `/hubs/collab` |
| mailpit | SMTP giả | 20 MB | Service image (Notification cần SMTP; không có thì gửi email lỗi nhưng thông báo trong app vẫn chạy) | Không |
| kafka-ui, web, seed | – | – | Không đưa lên. Web giữ ở Firebase Hosting; seed chạy một lần từ laptop trỏ vào Gateway Railway | – |

**Tổng:** 12 service, khoảng **2,2–3 GB RAM** khi rảnh.

## 2. Thay đổi cần trong repo (nhỏ, giữ nguyên `docker compose` local)

1. `deploy/docker/dotnet.Dockerfile` hiện cần build context = `backend/`. Railway build với context = gốc repo, nên thêm `ARG SRC=.` và đổi `COPY . .` thành `COPY ${SRC} .` (local vẫn mặc định `.`; Railway đặt `SRC=backend`). Thêm `.dockerignore` ở gốc để không gửi `web/node_modules`, `mobile/build`.
2. `deploy/railway/postgres.Dockerfile`: `FROM postgres:16-alpine` + `COPY deploy/db /docker-entrypoint-initdb.d`.
3. `deploy/railway/seaweedfs.Dockerfile`: sinh `s3.json` từ `S3_ACCESS_KEY`, `S3_SECRET_KEY` rồi chạy `weed server`.
4. Notification đọc service account từ file (`FIREBASE_SERVICE_ACCOUNT_PATH`). Railway không mount file từ máy được, nên thêm cách đọc từ biến `FIREBASE_SERVICE_ACCOUNT_JSON` (nội dung JSON, đặt bằng Railway Variables, không commit). Sửa trong `FcmPushSender.cs`, giữ cách đọc file cho local.
5. Chặn `/api/dev/*` ở Gateway khi chạy công khai (Caddy đang làm việc này ở bản VPS; Railway không có Caddy): thêm cấu hình `Gateway:BlockDevEndpoints=true` qua biến môi trường.
6. Rate limit báo cáo lỗi (CR-05): đặt `ClientErrors__RateLimit__TrustedProxyHops=2` cho identity-api (proxy của Railway + Gateway).

## 3. Biến môi trường (tên và nguồn, không ghi giá trị)

| Biến | Đặt cho | Nguồn |
|---|---|---|
| `POSTGRES_PASSWORD`, `*_DB_PASSWORD` (6 cái), `ADMIN_EMAIL` | postgres | Sinh mới (`openssl rand -base64 32`); `ADMIN_EMAIL` = email Google tài khoản Bộ môn demo |
| `ConnectionStrings__*Db` | từng service .NET | `Host=postgres.railway.internal;…;Password=${{postgres.<SERVICE>_DB_PASSWORD}}` |
| `Jwt__SigningKey` | mọi service .NET | Sinh mới, dùng chung (shared variable) |
| `Kafka__BootstrapServers` | service .NET | `kafka.railway.internal:29092` |
| `ConnectionStrings__Redis` | academic, matching | `${{Redis.REDIS_URL}}` hoặc `redis.railway.internal:6379` |
| `*__BaseUrl`, `Jobs__*Url`, `ReverseProxy__Clusters__*__Address` | team, academic, collab, jobs, gateway | `http://<service>.railway.internal:8080` |
| `ObjectStorage__ServiceUrl` / `ObjectStorage__PublicUrl` | collab-api | `http://object-storage.railway.internal:8333` / `https://<domain object-storage>` |
| `S3_ACCESS_KEY`, `S3_SECRET_KEY` | object-storage, collab-api | Sinh mới |
| `FIREBASE_SERVICE_ACCOUNT_JSON` | notification | Thiên dán nội dung file JSON vào Railway Variables |
| `Cors__AllowedOrigins__*` | gateway | Đã có 2 origin Hosting trong `appsettings.json` |
| `ASPNETCORE_ENVIRONMENT` | service .NET | Giữ `Development` như bản VPS (một phần cấu hình nằm ở `appsettings.Development.json`), chặn `/api/dev/*` bằng mục 2.5 |

## 4. Chi phí ước tính (giá Railway: RAM 10 USD / GB-tháng, CPU 20 USD / vCPU-tháng, tính theo phút)

| Cách chạy | RAM | CPU (ước 0,3 vCPU trung bình) | Tổng |
|---|---|---|---|
| Chạy liên tục 1 tháng | ~2,5 GB × 10 = 25 USD | ~6 USD | **~31 USD/tháng** – vượt hard limit 10 USD sau khoảng 9–10 ngày |
| Chỉ bật quanh buổi demo, ví dụ 3 ngày (72 giờ) | 2,5 × 10 × 72/730 ≈ 2,5 USD | ≈ 0,6 USD | **~3 USD** |
| Thêm: build image lần đầu (12 service) | – | – | Thường vài phút CPU, không đáng kể |

Gói Hobby đã gồm 5 USD usage mỗi tháng, nên chạy quanh buổi demo gần như không tốn thêm.

**Tắt khi không demo:** mỗi service → Settings → **Serverless** (tự ngủ khi không có request, Kafka/Postgres không nên bật) hoặc dùng `railway down` / xóa deployment rồi `railway redeploy` khi cần. Volume giữ dữ liệu.

## 5. Rủi ro

| Rủi ro | Cách xử lý |
|---|---|
| Kafka KRaft trên mạng riêng của Railway (IPv6) | Listener `PLAINTEXT://[::]:29092`, advertised `kafka.railway.internal:29092`; thử Kafka trước khi deploy các service khác |
| 12 service khởi động không theo thứ tự (Railway không có `depends_on`) | Các service .NET đã thử lại kết nối Kafka/DB; deploy theo thứ tự hạ tầng → service → gateway, kiểm tra `/health` từng cái |
| Job ghép tự động (Hangfire) chạy trên dữ liệu demo | Seed lại sau khi deploy; nếu job làm lệch dữ liệu demo thì tắt service `jobs` lúc demo |
| Link file S3 ký sẵn phải trỏ domain công khai | `ObjectStorage__PublicUrl` = domain Railway của object-storage |
| Lộ `/api/dev/token` (cấp JWT không cần Google) ra Internet | Mục 2.5 – bắt buộc trước khi tạo public domain cho gateway |
| Hết 10 USD giữa chừng | Chỉ bật quanh demo; email cảnh báo ở 5 USD |

## 6. Thứ tự triển khai (sau khi Thiên duyệt)

1. PR nhỏ cho các thay đổi mục 2 (Thiên duyệt vì đụng BuildingBlocks/Dockerfile/Notification).
2. postgres → redis → kafka → object-storage → mailpit.
3. identity → academic → matching → team → notification → collab → jobs → gateway, mỗi service đợi `/health` 200.
4. Tạo domain cho gateway và object-storage; kiểm tra `/api/dev/token` trả 404.
5. Seed dữ liệu demo từ laptop: `SEED_GATEWAY_URL=https://<gateway>` (seed cần `/api/dev/token` → chạy seed **trước** khi bật chặn, hoặc tạm cho seed đi qua mạng riêng).
6. Build lại web (`VITE_API_BASE_URL=https://<gateway>`) và deploy Hosting; build APK release trỏ về gateway.
7. Chạy checklist kiểm tra trong prompt BE-85, ghi kết quả vào file này.
