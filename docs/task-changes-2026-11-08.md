# Thay đổi task ngày 08/11/2026 – Import sinh viên, giảng viên và kế thừa nhóm EXE201 thực tế

> Cập nhật theo yêu cầu triển khai thực tế từ tài liệu `docs/prompts/gemini-import-sv-gv.md`.
> Gồm 3 Pull Request độc lập:
> - **PR 1:** Tự suy luận chuyên ngành theo tiền tố MSSV (`feat/BE-91-student-import-major-prefix`).
> - **PR 2:** Tự đối chiếu và kế thừa nhóm EXE201 từ Group List mốc (`feat/BE-92-exe201-auto-inherit-teams`).
> - **PR 3:** Import Excel giảng viên và bắt buộc khai báo số nhóm mong muốn (`feat/BE-93-lecturer-import`).

---

## 1. Bối cảnh và mục tiêu

Khi đưa hệ thống vào thử nghiệm với dữ liệu thực tế tại Đại học FPT (các file `SU26_EXE101 _ Group List.xlsx` và `FA26_EXE201 _ Group List.xlsx`):
1. **Dữ liệu sinh viên:**
   - File danh sách sinh viên thực tế thường không có cột mã ngành chuẩn; tiền tố MSSV (SE, IA, SS, CS, MC, DS...) đã phản ánh chính xác ngành/chuyên ngành. Cần cơ chế tự động ánh xạ tiền tố sang mã ngành hợp lệ trong DB thay vì báo lỗi toàn bộ dòng.
2. **Kế thừa nhóm EXE201:**
   - Dữ liệu mốc từ EXE101 thực tế có định dạng đa dạng (cột `GroupName`, `Họ & tên Leader`, `MSSV`, `Tên đề tài tiếng Việt/tiếng Anh`). Cần bộ phân tích OpenXML đọc được file thực tế, chuẩn hóa dấu tiếng Việt và tự động liên kết đề tài cũ với nhóm EXE201 kế thừa.
3. **Quản lý giảng viên & Cố vấn:**
   - Bộ môn cần import nhanh danh sách giảng viên từ file Excel (email, họ tên, SĐT).
   - Trước khi tham gia thuật toán ghép nhóm tự động (Advisor Matching), giảng viên bắt buộc phải hoàn thiện hồ sơ: chọn ít nhất 1 hướng chuyên môn và khai báo số nhóm mong muốn hướng dẫn (EXE101, EXE201) không vượt quá trần tối đa do Bộ môn quy định.

---

## 2. Chi tiết 3 Pull Request

### PR 1: `feat/BE-91-student-import-major-prefix`
- **Mã task:** `BE-91`, `FE-55`, `MB-65`.
- **Nội dung:**
  - **Mô hình Ngành & Chuyên ngành:** Tiền tố MSSV đại diện cho **Ngành** (Major: SE, SS, HS, IB, SA); **Chuyên ngành** (Specialization: Kỹ thuật phần mềm, Trí tuệ nhân tạo, Marketing, Truyền thông đa phương tiện...) là con của Ngành và do sinh viên tự chọn ở hồ sơ cá nhân.
  - Seed bảng `academic.majors`: `SE` (Công nghệ thông tin), `SS` (Quản trị kinh doanh), `HS` (Truyền thông đa phương tiện), `IB` (Kinh doanh quốc tế), `SA` (Ngôn ngữ). Bỏ các mã chuyên ngành (AI, IA, GD, MC, MKT) khỏi bảng ngành.
  - Ánh xạ `specializations.major_id`: SE, AI, IA, GD, IOT → SE; DM, FIN, LOG, HM, LAW → SS; MC → HS; IB → IB; EN, JP, KR, CN → SA.
  - `Academic.Services/Imports/StudentMajorInferrer.cs`: Tự động nhận diện tiền tố MSSV và gán đúng `MajorId` trong danh mục `academic.majors`.
  - Cập nhật parser file import `StudentImportFile.cs`: hỗ trợ file có hoặc không có cột chuyên ngành, tự động đọc mã lớp (`classCode`).
  - Migration `BE-91_majors_and_student_import.sql`: Bỏ fallback `COALESCE(..., 'SE')`; sử dụng khối DO PL/pgSQL kiểm tra và `RAISE EXCEPTION` nếu còn sinh viên có tiền tố không xác định được ngành.
  - Quản lý danh mục (W12–W14, W27 catalogs): API `GET /api/specializations?majorId=` lọc theo ngành; bắt buộc `majorId` khi tạo chuyên ngành mới; ràng buộc không cho ngừng kích hoạt ngành nếu còn chuyên ngành con đang dùng. Web hiển thị cột Ngành, bộ lọc và ô chọn Ngành trong drawer.
  - Onboarding & Hồ sơ sinh viên (web + mobile): Chỉ hiển thị các chuyên ngành thuộc ngành của sinh viên.
  - Cập nhật thông báo lỗi mẫu MSSV sang `SE184118`.
  - Thêm `majorId` vào snapshot sinh viên và sự kiện `StudentImported`, `StudentProfileUpdated`.

### PR 2: `feat/BE-92-exe201-auto-inherit-teams`
- **Mã task:** `BE-92`, `FE-56`.
- **Nội dung:**
  - `Team.Services/BaselineImport/ExeGroupListParser.cs`: Bộ parser Excel OpenXML chuyên dụng đọc các file `SU26_EXE101 _ Group List.xlsx` và `FA26_EXE201 _ Group List.xlsx`.
  - Chuẩn hóa tiêu đề cột và tên sinh viên loại bỏ dấu tiếng Việt (`FormD` Unicode decomposition) để đối chiếu chính xác.
  - Tự động tạo nhóm kế thừa EXE201, liên kết với đề tài (`projects`) và gán đúng trưởng nhóm theo thông tin mốc.
  - **Sửa quy tắc đa dạng ngành / chuyên ngành:**
    - Lưu `major_id` trong `team_members`, `team_baseline_members`, `change_request_items`; chuyển `specialization_id` thành nullable (bỏ gán mặc định `0`).
    - Tập trung hóa quy tắc đếm đa dạng tại `TeamDiversityRule.CountDiversity`: đếm số khóa phân biệt `(major_id, specialization_id ?? "ngành")`. Hai sinh viên khác ngành → tính đa dạng; cùng ngành nhưng khác chuyên ngành đã khai → tính đa dạng; cùng ngành và chưa khai chuyên ngành → coi là cùng nhánh.
    - Thêm consumer `StudentProfileUpdatedHandler` trong Team Service để đồng bộ `specialization_id` và `major_id` vào `team_members` khi sinh viên cập nhật hồ sơ cá nhân.
  - **Sửa xác định học kỳ liền trước:**
    - Thay thế logic tìm nhóm EXE101 gần nhất bằng so sánh `StartDate` của các học kỳ từ `academic.ListSemestersAsync`, chọn học kỳ có `StartDate < currentSemester.StartDate` lớn nhất.
  - Web UI (FE-56): Hỗ trợ tải file Group List thực tế trên màn hình W06/W08 với bảng xem trước danh sách nhóm, thành viên và đề tài.

### PR 3: `feat/BE-93-lecturer-import`
- **Mã task:** `BE-93`, `FE-57`, `MB-66`.
- **Nội dung:**
  - **Database Migration:**
    - `deploy/db/02_academic_db.sql` & `deploy/db/migrations/BE-93_lecturer_desired_teams.sql`: Thêm các cột `desired_teams_exe101`, `desired_teams_exe201`, `profile_completed_at` vào bảng `academic.lecturers` kèm ràng buộc kiểm tra. Thêm `SET ROLE ... RESET ROLE;`.
  - **Academic Backend (BE-93):**
    - `ImportsController.cs` & `ImportService.cs`: Các endpoint `GET /api/imports/lecturers/template`, `POST /api/imports/lecturers/preview`, `POST /api/imports/lecturers/{previewId}/confirm`.
    - `LecturerService.cs`: Validate số nhóm mong muốn không vượt quá trần tối đa; tự động cập nhật `ProfileCompletedAt` khi đã chọn ≥ 1 domain và khai báo số nhóm mong muốn.
  - **Advisor Matching Integration:**
    - `Team.Services/AdvisorMatching/AdvisorSources.cs`: `LecturerAdvisorSource` loại bỏ giảng viên có `isProfileCompleted == false` khỏi danh sách tham gia ghép; ưu tiên sức chứa theo `desiredTeams` trước khi dùng `maxTeams`.
  - **Frontend Web (FE-57):**
    - W11: Nút "Import Excel", hộp thoại import kèm xem trước và cảnh báo lỗi, hiển thị badge "Chưa khai báo" và cột số nhóm mong muốn.
    - L06: Banner cảnh báo hồ sơ chưa hoàn thiện, form nhập số nhóm mong muốn và chọn domain kèm validation client-side.
    - Router Gate (`requireCompletedLecturerProfile`): Chuyển hướng giảng viên chưa hoàn thiện hồ sơ về `/gv/ho-so`.
  - **Mobile App (MB-66):**
    - L06 (`lecturer_profile_screen.dart`, `lecturer_profile_view_model.dart`): Cảnh báo chưa hoàn thiện, form nhập số nhóm mong muốn EXE101/EXE201 với trần tối đa, cập nhật ViewModel và API client.
  - **Documentation:**
    - Cập nhật `docs/quy-trinh-he-thong.md` (Luồng 1, Luồng 2, Luồng 6).
    - Cập nhật `docs/ux/PROJECT_CONTEXT.md` (Rule 5, Rule 9).

---

## 3. Thống kê tiền tố MSSV từ dữ liệu thực tế (`SU26_EXE101 _ Group List.xlsx`)

Dưới đây là thống kê từ bộ dữ liệu thực tế tại FPT University Hồ Chí Minh:

| Tiền tố | Ngành phổ biến nhất | Chuyên ngành phổ biến trong dữ liệu | Số SV | Trạng thái ánh xạ & Ghi chú |
|---|---|---|---|---|
| `SE` | Công nghệ thông tin (844 SV) | Kỹ thuật phần mềm (462), Thiết kế đồ họa & mỹ thuật số (144), An toàn thông tin (85), Trí tuệ nhân tạo (84), Hệ thống thông tin (29), Thiết kế Game (18), Vi mạch bán dẫn (17) | 888 | **Chính thức**: Ánh xạ ngành CNTT (`SE`) |
| `SS` | Quản trị kinh doanh (165 SV) / Công nghệ truyền thông (91 SV) | Marketing (74), Kinh doanh quốc tế (63), Truyền thông đa phương tiện (70), Quản trị khách sạn (14), PR (13), Tài chính (10) | 265 | **Chính thức**: Ánh xạ ngành Quản trị kinh doanh (`SS`) |
| `SA` | Khối Ngành Ngôn ngữ (9 SV) | Ngôn ngữ Anh (9), Kinh doanh quốc tế (2), Marketing (2) | 18 | **Chính thức**: Ánh xạ ngành Ngôn ngữ (`SA`) |
| `CE` | Công nghệ thông tin (3 SV) | Kỹ thuật phần mềm (2), Thiết kế đồ họa (1) | 3 | Cơ sở Cần Thơ (Khối CNTT) |
| `CS` | Công nghệ truyền thông (4 SV) | Truyền thông đa phương tiện (3), Quan hệ công chúng (1) | 4 | Cơ sở Cần Thơ (Truyền thông / QTKD) - Cần bộ môn xác nhận |
| `DE` | Công nghệ thông tin (5 SV) | An toàn thông tin (3), Kỹ thuật phần mềm (1), Thiết kế đồ họa (1) | 7 | Cơ sở Đà Nẵng (Khối CNTT) |
| `DS` | Công nghệ truyền thông (1 SV) | Truyền thông đa phương tiện (1) | 1 | Cơ sở Đà Nẵng (< 3 SV) - Cần bộ môn xác nhận |
| `HS` | Quản trị kinh doanh (5 SV) | Marketing (2), Truyền thông đa phương tiện (1), KDQT (1), Tài chính (1) | 5 | Cơ sở Hà Nội (Sheet ghi QTKD, gợi ý là Truyền thông) - Cần bộ môn xác nhận |
| `QE` | Công nghệ thông tin (4 SV) | Thiết kế đồ họa (2), Kỹ thuật phần mềm (2) | 5 | Cơ sở Quy Nhơn (Khối CNTT) |
| `QS` | Quản trị kinh doanh (3 SV) | Marketing (2), Quản trị kinh doanh (1) | 3 | Cơ sở Quy Nhơn (QTKD) |

> [!NOTE]
> Các tiền tố ngoài cơ sở chính hoặc có mẫu nhỏ / dữ liệu ngành mâu thuẫn (`CS`, `DS`, `HS`, `CE`, `DE`, `QE`, `QS`) được liệt kê để Bộ môn xác nhận quy tắc ánh xạ chính thức, không đoán bừa.

---

## 4. Tiêu chí kiểm thử và xác nhận chất lượng (Verification Gates)

| Cổng kiểm thử | Lệnh thực thi | Kết quả |
|---|---|---|
| **Backend Build** | `dotnet build backend/ExeTeamMatching.sln` | 0 errors, 0 warnings |
| **Backend Unit Tests** | `dotnet test backend/ExeTeamMatching.sln` | 784/784 passed |
| **Frontend Web Tests** | `npm --prefix web test` | 249/249 passed (36 suites) |
| **Frontend Web Build** | `npm --prefix web run build` | Build thành công (exit code 0) |
| **Mobile Tests** | `flutter test` | 607/607 passed |
| **Mobile Analyzer** | `flutter analyze mobile/` | No issues found! (0 errors, 0 warnings) |
