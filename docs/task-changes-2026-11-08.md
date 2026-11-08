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
  - `Academic.Services/Imports/StudentMajorInferrer.cs`: Tự động nhận diện tiền tố MSSV (SE, IA, SA, SS, CS, MC, DS, AI, IS...) và gán đúng `MajorId` trong danh mục `academic.majors`.
  - Cập nhật parser file import `StudentImportFile.cs`: hỗ trợ file có hoặc không có cột chuyên ngành, tự động đọc mã lớp (`classCode`).
  - Web UI (FE-55) & Mobile (MB-65): Hiển thị ngành được tự động nhận diện và lớp học trong danh sách sinh viên.

### PR 2: `feat/BE-92-exe201-auto-inherit-teams`
- **Mã task:** `BE-92`, `FE-56`.
- **Nội dung:**
  - `Team.Services/BaselineImport/ExeGroupListParser.cs`: Bộ parser Excel OpenXML chuyên dụng đọc các file `SU26_EXE101 _ Group List.xlsx` và `FA26_EXE201 _ Group List.xlsx`.
  - Chuẩn hóa tiêu đề cột và tên sinh viên loại bỏ dấu tiếng Việt (`FormD` Unicode decomposition) để đối chiếu chính xác.
  - Tự động tạo nhóm kế thừa EXE201, liên kết với đề tài (`projects`) và gán đúng trưởng nhóm theo thông tin mốc.
  - Web UI (FE-56): Hỗ trợ tải file Group List thực tế trên màn hình W06/W08 với bảng xem trước danh sách nhóm, thành viên và đề tài.

### PR 3: `feat/BE-93-lecturer-import`
- **Mã task:** `BE-93`, `FE-57`, `MB-66`.
- **Nội dung:**
  - **Database Migration:**
    - `deploy/db/02_academic_db.sql` & `deploy/db/migrations/BE-93_lecturer_desired_teams.sql`: Thêm các cột `desired_teams_exe101`, `desired_teams_exe201`, `profile_completed_at` vào bảng `academic.lecturers` kèm ràng buộc kiểm tra.
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

## 3. Tiêu chí kiểm thử và xác nhận chất lượng (Verification Gates)

| Cổng kiểm thử | Lệnh thực thi | Kết quả |
|---|---|---|
| **Backend Build** | `dotnet build backend/ExeTeamMatching.sln` | 0 errors, 0 warnings |
| **Backend Unit Tests** | `dotnet test backend/ExeTeamMatching.sln` | 784/784 passed |
| **Frontend Web Tests** | `npm --prefix web test` | 249/249 passed (36 suites) |
| **Frontend Web Build** | `npm --prefix web run build` | Build thành công (exit code 0) |
| **Mobile Tests** | `flutter test` | 607/607 passed |
| **Mobile Analyzer** | `flutter analyze mobile/` | No issues found! (0 errors, 0 warnings) |
