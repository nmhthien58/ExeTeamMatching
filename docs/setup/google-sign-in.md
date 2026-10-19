# Tạo OAuth client cho đăng nhập Google (qua Firebase Console)

Mục tiêu: có **Web client ID** dùng chung cho web, mobile và Identity Service, kèm OAuth client cho Android, mà không phải tự cấu hình màn hình đồng ý (consent screen) hay xin chuyển trạng thái từ Testing sang Production.

Tạo qua Firebase vì khi bật nhà cung cấp Google trong Firebase Authentication, Firebase tự tạo:
- Web client "Web client (auto created by Google Service)";
- màn hình đồng ý ở trạng thái **In production** với các quyền cơ bản (`openid`, `email`, `profile`). Các quyền này không nhạy cảm nên không cần Google xác minh.

Hệ thống **không dùng Firebase Auth SDK** để đăng nhập. Client lấy Google ID token, gửi lên `POST /api/auth/google`, và Identity Service tự kiểm tra token rồi cấp JWT của hệ thống (xem `docs/contracts/openapi/identity.yaml`). Firebase chỉ dùng để tạo OAuth client cho nhanh.

## 1. Tạo project và bật đăng nhập Google (Thiên làm một lần)

1. Vào https://console.firebase.google.com → **Create a project** (hoặc **Add project**). Tên gợi ý: `exe-team-matching`. Google Analytics: tắt cũng được.
2. Menu trái **Build → Authentication** → **Get started**.
3. Tab **Sign-in method** → **Add new provider** → **Google** → bật **Enable**:
   - *Public-facing name*: `EXE Team Matching` (người dùng thấy tên này khi chọn tài khoản);
   - *Project support email*: email của bạn → **Save**.
4. Bấm lại vào dòng **Google** → mở mục **Web SDK configuration** → chép **Web client ID** (dạng `1234…-abc….apps.googleusercontent.com`). Đây là giá trị dùng ở bước 4. **Không** cần dùng *Web client secret*.

Kiểm tra trạng thái màn hình đồng ý: mở https://console.cloud.google.com, chọn đúng project → **Google Auth Platform → Audience**. *Publishing status* phải là **In production**, *User type* là **External**. Nếu thấy *Testing* thì bấm **Publish app**; với 3 quyền cơ bản thì việc này không cần xác minh.

## 2. Thêm app Android

1. Firebase Console → biểu tượng bánh răng → **Project settings** → **Your apps** → **Add app** → Android.
2. *Android package name*: `vn.edu.fpt.exe.exe_team_matching` (khớp `applicationId` trong `mobile/android/app/build.gradle.kts`).
3. *Debug signing certificate SHA-1*: thêm SHA-1 của **từng máy** chạy app, vì mỗi máy có một debug keystore khác nhau. Lấy SHA-1 bằng:
   ```bash
   keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android
   ```
   Trên Windows, `keytool` nằm trong `C:\Program Files\Android\Android Studio\jbr\bin\`. Thêm cả SHA-256 ở **Project settings → Your apps → Android → Add fingerprint**.
   Cách khác: cả nhóm dùng chung một file `debug.keystore` để chỉ cần đăng ký một SHA-1.
4. Tải `google-services.json`, đặt vào `mobile/android/app/` (file này đã nằm trong `.gitignore`, gửi cho nhóm qua kênh riêng).

Thêm SHA-1 xong, Google tự tạo OAuth client loại Android. Nếu thiếu SHA-1 của máy nào thì đăng nhập trên máy đó báo lỗi `DEVELOPER_ERROR` / `ApiException: 10`.

## 3. Cho phép web chạy ở localhost

1. Google Cloud Console → **APIs & Services → Credentials** → mở **Web client (auto created by Google Service)**.
2. **Authorized JavaScript origins** → thêm `http://localhost:3000` và `http://localhost` → **Save**. Có thể mất vài phút mới có hiệu lực.
3. Khi đưa web lên server thật, thêm domain đó vào đây.

## 4. Điền client ID vào từng phần

> **Đã điền (20/10/2026)** – project Firebase của nhóm, Web client ID `221888974127-ho8c6t3rqct6f7r5o2e8gott7i8lrd7s.apps.googleusercontent.com`:
> `web/.env.example` (sao chép thành `web/.env.local`), giá trị mặc định `GOOGLE_SERVER_CLIENT_ID` trong `mobile/lib/core/config/env.dart`,
> `Google:ClientId` trong `backend/Services/Identity/Identity.Api/appsettings.json`. Thành viên khác chỉ cần nhờ Thiên thêm **SHA-1 máy mình** vào app Android trong Firebase.
> Không cần `google-services.json` và không cần thêm plugin Gradle của Firebase (hệ thống không dùng Firebase SDK).

Client ID không phải bí mật, nhưng vẫn để trong file môi trường để mỗi máy tự đặt.

| Phần | Đặt ở đâu | Dùng để |
|---|---|---|
| Web (FE-04) | `web/.env.local`: `VITE_GOOGLE_CLIENT_ID=<Web client ID>` | Google Identity Services trả ID token |
| Mobile (MB-07) | `--dart-define=GOOGLE_SERVER_CLIENT_ID=<Web client ID>` và `google-services.json` | Gói `google_sign_in`: truyền Web client ID vào `serverClientId` để ID token có `aud` = Web client ID |
| Identity Service (BE-12) | Biến môi trường `Google__ClientId=<Web client ID>` (hoặc `dotnet user-secrets`) | `GoogleJsonWebSignature.ValidateAsync` với `Audience = [Google:ClientId]` |

Vì web và mobile đều xin token cho cùng Web client ID, Identity Service chỉ cần chấp nhận **một** audience.

## 5. Thử nhanh

- Web: `npm run dev`, bấm đăng nhập Google; trong DevTools phải nhận được `credential` (ID token).
- Mobile: chạy trên emulator có Google Play; chọn tài khoản thì nhận được `idToken` khác null.
- Dán ID token vào https://jwt.io để xem: `aud` = Web client ID, `iss` = `https://accounts.google.com`, `email_verified` = `true`.

## Lỗi hay gặp

| Lỗi | Nguyên nhân |
|---|---|
| `idToken` null trên Android | Chưa truyền `serverClientId` = Web client ID |
| `ApiException: 10` / `DEVELOPER_ERROR` | SHA-1 của máy chưa được thêm vào Firebase, hoặc sai package name |
| Web báo `origin_mismatch` | Chưa thêm `http://localhost:3000` vào Authorized JavaScript origins |
| Màn hình Google ghi "app chưa được xác minh" / chỉ cho test user | Consent screen đang ở Testing → **Publish app** |
| Identity trả 403 `not-in-list` | Email chưa có trong danh sách import (đúng quy tắc 5), hoặc chưa đặt `ADMIN_EMAIL` cho tài khoản Admin |
