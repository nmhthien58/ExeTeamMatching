# FE-47: Luồng đổi nhóm xuyên vai trò

> Phiên bản: 2026-11-05. Nguồn đã đối chiếu: `docs/contracts/openapi/team.yaml`, `docs/contracts/events.md`,
> `backend/Services/Notification/Notification.Worker/` (nhánh `feat/BE-84-fcm-push`),
> `mobile/lib/core/firebase/push_notifications.dart`, `mobile/lib/features/change_requests/` (nhánh `feat/MB-52-fcm-mobile`).
> Phụ lục C (Final Assignment PRM393) yêu cầu tài liệu luồng xuyên vai trò, sơ đồ sequence và đo độ trễ thực tế.

---

## 1. Phạm vi và trạng thái

| Hạng mục | Trạng thái |
|---|---|
| Tài liệu mô tả API, sự kiện, sơ đồ | Hoàn thành – đối chiếu nguồn |
| Làm rõ cơ chế realtime (mục 4) | Hoàn thành – đã bổ sung realtime invalidation trên MB-52 |
| Kiểm thử thực tế 3 lần trên bản triển khai | **Chưa thực hiện (chờ BE-85)** – xem mục 7 |

**Phụ thuộc và trạng thái môi trường:**
- BE-84 ([PR #166](https://github.com/nmhthien58/ExeTeamMatching/pull/166)) đã merge vào `develop`.
- MB-52 ([PR #172](https://github.com/nmhthien58/ExeTeamMatching/pull/172)): đã rebase `develop`, giải quyết xung đột `pubspec.yaml`, sửa CR-07 và bổ sung realtime invalidation cho S26/S29 theo góp ý duyệt của Thiên; 582 unit test passed, `flutter analyze` sạch; đang chờ Thiên merge.
- Cấu hình môi trường demo (theo `docs/setup/deploy.md`): Web dùng bản trên Firebase Hosting (`https://exe-team-matching.web.app`), backend Docker chạy trên laptop demo (Gateway port 8080).
- Máy thật Android kết nối qua: `adb reverse tcp:8080 tcp:8080`, build/chạy với `--dart-define=API_BASE_URL=http://localhost:8080`.
- File service account Firebase `deploy/secrets/firebase-service-account.json` đã được đặt vào thư mục secrets (đã gitignore).

---

## 2. Điều kiện tiên quyết trước khi kiểm thử

1. Môi trường demo theo BE-85 (`docs/setup/deploy.md`): Backend Docker chạy trên laptop demo (`http://localhost:8080`), Web trên Firebase Hosting (`https://exe-team-matching.web.app`) đã được cấp quyền CORS từ Gateway.
2. Mobile Android kết nối laptop qua `adb reverse tcp:8080 tcp:8080`, chạy với `--dart-define=API_BASE_URL=http://localhost:8080`.
3. Nhánh MB-52 được merge vào develop hoặc chạy trực tiếp từ `feat/MB-52-fcm-mobile`.
4. Kafka, Notification Service (BE-84) và FCM đang chạy; service account FCM đọc từ biến môi trường.
5. Dữ liệu demo ẩn danh:
   - Nhóm EXE201 kế thừa có trưởng nhóm (tài khoản demo).
   - Đợt đổi nhóm đang mở (`registration_rounds` với `round_type = CHANGE_REQUEST` và `closes_at` chưa qua).
   - Có thay đổi chờ gửi hợp lệ (mốc khác với nhóm hiện tại).
   - Tài khoản Bộ môn.
6. Mobile đăng nhập đúng trưởng nhóm, đã cấp quyền thông báo, device token đã được đăng ký BE-84.

---

## 3. Các bước luồng và API

| Bước | Người làm / màn | API qua Gateway | Service xử lý | Sự kiện Kafka |
|---|---|---|---|---|
| **1a** | Trưởng nhóm (S26, mobile): xem danh sách yêu cầu cũ | `GET /api/teams/{teamId}/change-requests` | Team Service | — |
| **1b** | Trưởng nhóm (S27, mobile): xem thay đổi chờ gửi | `GET /api/teams/{teamId}/pending-changes` _(xem team.yaml tag ChangeRequests/Projects)_ | Team Service | — |
| **1c** | Trưởng nhóm (S27→S28, mobile): gửi yêu cầu | `POST /api/teams/{teamId}/change-requests` body: `{ itemIds, reason }` | Team Service: lưu `change_requests`, ghi Outbox | `ChangeRequestSubmitted` → `exe.change-request` |
| **2a** | Bộ môn (W17, web): xem danh sách | `GET /api/change-requests` (phân trang, lọc trạng thái) | Team Service | — |
| **2b** | Bộ môn (W18, web): xem chi tiết, so sánh mốc | `GET /api/change-requests/{id}` → `ChangeRequestDetail` | Team Service | — |
| **2c** | Bộ môn (W18, web): duyệt | `POST /api/change-requests/{id}/approve` body: `{ note? }` | Team Service: áp dụng thay đổi, ghi Outbox | `ChangeRequestApproved` → `exe.change-request` |
| **3** | Notification Service: consumer Kafka | Consumer nhận `ChangeRequestApproved`; `NotificationEventHandler.HandleAsync` → tạo `UserNotification` per thành viên (type `STUDENT`) và per giảng viên hướng dẫn (`LECTURER`); sau `SaveChangesAsync`, `NotificationProcessedEventStore.MarkProcessedAsync` gọi `FcmPushDispatcher.SendAsync` | Notification Service (BE-84) | — |
| **4** | FCM → mobile: nhận push | FCM gửi data payload: `{ type: "ChangeRequestApproved", id: <notificationId>, linkUrl: "app://change-requests" }` | Firebase Cloud Messaging | — |
| **5** | Mobile (MB-52): xử lý push | `PushNotificationsController._onForeground` / `_onOpened` → `_refreshInbox(message)`: tự động invalidate `notificationsViewModelProvider`, `changeRequestsProvider`, `changeRequestDetailProvider`, `pendingChangesProvider`, `myTeamProvider` | Flutter Riverpod | — |
| **6** | Sinh viên (A06, mobile): kiểm tra thông báo | `GET /api/notifications` (tự động load danh sách mới sau invalidate) | Notification Service | — |
| **7** | Sinh viên (S26/S29, mobile): xem kết quả | `GET /api/teams/{teamId}/change-requests` (tự động reload trạng thái mới) | Team Service | — |

**Lưu ý về recipient `ChangeRequestSubmitted`:** theo `NotificationEventHandler` (dòng 135), sự kiện này chỉ báo `ADMIN` (Bộ môn), không có push về mobile cho trưởng nhóm.

---

## 4. Phân tích cơ chế cập nhật realtime (đã đối chiếu nguồn)

### 4.1 Push FCM và A06

- Khi `ChangeRequestApproved` được Notification Service xử lý, `FcmPushDispatcher.SendAsync` tra `device_tokens` theo `userId` (kể cả giảng viên kiêm mentor dùng cùng email).
- Mobile (MB-52): `PushNotificationsController._onForeground/_onOpened` gọi `_refreshInbox(message)`:
  - **Invalidate `notificationsViewModelProvider`** → A06 tải lại danh sách thông báo.
  - **Load lại `unreadCountViewModelProvider`** → badge A06 cập nhật số chưa đọc.
- Thông báo tiêu đề: **"Đổi nhóm đã được duyệt"**, nội dung: "Bộ môn đã duyệt yêu cầu đổi nhóm.", `linkUrl = "app://change-requests"`.

### 4.2 S26/S29 cập nhật realtime

- Ban đầu trong bản phác thảo của MB-52, `_refreshInbox()` chỉ invalidate `notificationsViewModelProvider` và `unreadCountViewModelProvider`, người dùng ở S26/S29 phải kéo tay (`RefreshIndicator`) để thấy cập nhật.
- Theo thỏa thuận review PR #172 và #175 (Thiên đã đồng ý), Thành đã bổ sung xử lý trực tiếp trong `_refreshInbox([PushMessage? message])` của MB-52:
  Khi nhận push có `type == 'ChangeRequestApproved'` / `'ChangeRequestRejected'` hoặc `linkUrl` chứa `app://change-requests`, controller sẽ tự động invalidate:
  - `changeRequestsProvider` (danh sách yêu cầu S26)
  - `changeRequestDetailProvider` (chi tiết yêu cầu S29)
  - `pendingChangesProvider` (thay đổi chờ gửi S27/S28)
  - `myTeamDetailProvider` & `myTeamProvider` (thông tin đội S13–S15).
- Theo thiết kế, khi push đến ở foreground, S26 hoặc S29 đang mở sẽ được yêu cầu tải lại. Test fake đã xác nhận provider danh sách S26 được nạp lại; vẫn cần kiểm thử UI và API thật để xác nhận cả S26/S29 trên môi trường demo.

### 4.3 Tóm tắt cơ chế đồng bộ

- **A06 (Thông báo):** Invalidate `notificationsViewModelProvider` + nạp lại `unreadCountViewModelProvider`.
- **S26 (Danh sách yêu cầu):** Tự động reload qua `changeRequestsProvider`.
- **S29 (Chi tiết yêu cầu):** Tự động reload qua `changeRequestDetailProvider`.
- **S13–S15 (Nhóm của tôi):** Tự động reload thông tin thành viên mới sau khi thay đổi được duyệt.

---

## 5. Sơ đồ sequence

```mermaid
sequenceDiagram
    actor Leader as Trưởng nhóm (mobile S27–S28)
    actor Admin as Bộ môn (web W17–W18)
    participant GW as API Gateway
    participant Team as Team Service
    participant Kafka as Kafka exe.change-request
    participant Notify as Notification Service (BE-84)
    participant FCM as Firebase Cloud Messaging

    Leader->>GW: POST /api/teams/{teamId}/change-requests
    GW->>Team: Lưu yêu cầu + ghi Outbox
    Team-->>GW: 201 ChangeRequest
    Team->>Kafka: ChangeRequestSubmitted (recipients: ADMIN)
    Kafka->>Notify: ChangeRequestSubmitted → tạo thông báo cho Bộ môn

    Admin->>GW: GET /api/change-requests (W17)
    GW->>Team: Danh sách yêu cầu
    Admin->>GW: GET /api/change-requests/{id} (W18)
    GW->>Team: Chi tiết + so sánh mốc
    Admin->>GW: POST /api/change-requests/{id}/approve (T0)
    GW->>Team: Áp dụng thay đổi + ghi Outbox
    Team-->>GW: 200 ChangeRequest(APPROVED)
    Team->>Kafka: ChangeRequestApproved (recipients: thành viên STUDENT, giảng viên LECTURER)

    Kafka->>Notify: ChangeRequestApproved
    Notify->>Notify: Tạo UserNotification per thành viên và giảng viên
    Notify->>Notify: SaveChangesAsync → MarkProcessedAsync
    Notify->>FCM: FcmPushDispatcher.SendAsync (data: type, id, linkUrl=app://change-requests)
    FCM-->>Leader: Push notification (tiêu đề: "Đổi nhóm đã được duyệt")

    Note over Leader: _onForeground/_onOpened → _refreshInbox(message)
    Note over Leader: invalidate notificationsViewModelProvider (A06 tải lại)
    Note over Leader: unreadCountViewModelProvider.load() (badge A06 cập nhật)
    Note over Leader: invalidate changeRequestsProvider & myTeamProvider (S26/S29 tự làm mới)
    Leader->>GW: GET /api/notifications (A06 sau invalidate)
    Leader->>GW: GET /api/teams/{teamId}/change-requests (S26 tự động tải lại)
```

---

## 6. Môi trường triển khai (BE-85 đã duyệt và hoàn tất)

Theo tài liệu triển khai chuẩn `docs/setup/deploy.md` (BE-85, chốt ngày 05/11/2026):

| Thành phần | Môi trường / Địa chỉ | Trạng thái |
|---|---|---|
| **Web React** | Firebase Hosting: `https://exe-team-matching.web.app` | Đã deploy, gọi Gateway `http://localhost:8080` của laptop demo |
| **Backend** | Docker Desktop trên laptop demo (`http://localhost:8080`) | 13 container (Gateway, 8 service, Kafka, Postgres...) |
| **Mobile** | Điện thoại Android cắm cáp | `adb reverse tcp:8080 tcp:8080`, `--dart-define=API_BASE_URL=http://localhost:8080` |
| **FCM** | `deploy/secrets/firebase-service-account.json` | Đã đặt vào secrets, container `notification` mount read-only |
| **CORS** | Gateway `Cors:AllowedOrigins` | Đã cho phép `https://exe-team-matching.web.app` |

---

## 7. Kịch bản kiểm thử thực tế (chưa thực hiện)

### Chuẩn bị

Chuẩn bị **ba yêu cầu độc lập và hợp lệ** (không dùng lại yêu cầu đã duyệt). Mỗi yêu cầu phải khác nhau về itemIds hoặc lý do.

### Quy trình mỗi lần thử

1. Ghi môi trường, phiên bản app/web/backend và thời điểm thử.
2. Trưởng nhóm (mobile) thao tác S26 → S27 → S28, gửi yêu cầu.
3. Bộ môn (web) mở W17, tìm đúng yêu cầu, mở W18 và duyệt.
4. Ghi **T0** = thời điểm phản hồi HTTP 200 từ `POST /approve` trả về.
5. Ghi riêng:
   - **T_push**: thời điểm push xuất hiện trên thiết bị.
   - **T_a06**: thời điểm thông báo hiển thị ở A06 (sau invalidate).
   - **T_s26**: thời điểm S26 tự cập nhật trạng thái (sau invalidate).
6. Đo độ trễ bằng cùng đồng hồ hoặc video có thể đối chiếu.
7. Chạm thông báo push: kiểm tra liên kết `app://change-requests` mở danh sách yêu cầu S26, sau đó chọn đúng yêu cầu để xem S29. Kiểm tra A06 riêng.
8. Lưu ảnh hoặc video **không lộ token, email thật, service account, dữ liệu cá nhân**.

### Bảng kết quả

| Lần | Môi trường / phiên bản | T0 duyệt thành công | T_push | T_a06 | S26/S29 tự cập nhật | Độ trễ cập nhật (T_s26 − T0) | Bằng chứng |
|-----|------------------------|---------------------|--------|-------|--------------------|-----------------------|------------|
| 1 | Chưa thực hiện (chờ BE-85) | — | — | — | Dự kiến Có (sau MB-52) | Chưa đo | — |
| 2 | Chưa thực hiện (chờ BE-85) | — | — | — | Dự kiến Có (sau MB-52) | Chưa đo | — |
| 3 | Chưa thực hiện (chờ BE-85) | — | — | — | Dự kiến Có (sau MB-52) | Chưa đo | — |

> **Ghi chú:** cột "S26/S29 tự cập nhật" dự kiến "Có" do MB-52 đã bổ sung `invalidate` cho `changeRequestsProvider`, `changeRequestDetailProvider`, `pendingChangesProvider`, `myTeamProvider` khi nhận push (theo duyệt của Thiên).
> Sẽ ghi nhận số đo chính xác sau khi chạy 3 lần thực tế trên bản triển khai.
> Không điền số 0 hoặc tạo số đo khi chưa chạy thực tế.

---

## 8. Điều cần hỏi / chờ trước khi hoàn tất kiểm thử

1. **Thiên**: Merge PR #172 (MB-52) vào `develop` sau khi SonarCloud xanh.
2. **BE-85**: Chờ backend deploy hoàn chỉnh (Railway có URL công khai, hoặc chạy Docker laptop + `adb reverse` theo `docs/setup/deploy.md`).
3. Dữ liệu demo ẩn danh sẵn sàng trên môi trường triển khai (nhóm EXE201 kế thừa, đợt đổi nhóm đang mở, tài khoản Bộ môn).

---

> **Hai sự kiện `UserMentioned` đã gửi vào Kafka local trước đó** (kiểm thử FCM) **không phải bằng chứng của FE-47**:
> đó là sự kiện `exe.collab`, không phải `ChangeRequestApproved` trên `exe.change-request`.
