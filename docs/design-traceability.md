# Đối chiếu thiết kế Lab 2 → app (PRM393 Lab 3 §3, §4)

> MB-57, cập nhật 05/11/2026. Nguồn thiết kế: Figma `03 Final UI` (https://www.figma.com/design/tX0K0MdkTKt5wJcvWnPbxr), [flutter-handoff.md](ux/flutter-handoff.md), [user-flow.md](ux/user-flow.md), [DESIGN.md](ux/DESIGN.md). Kiến trúc và ViewModel: [architecture.md](architecture.md). Route khai báo ở `mobile/lib/app/router.dart`, `navigation.dart` (tab), `feature_routes.dart` (màn cấp 2).
>
> **Trạng thái:** ✅ khớp thiết kế · ⚠️ khác thiết kế, có lý do ở mục 4 · ⏳ chưa có màn thật.
> **Figma:** frame A03, header các tab và thanh kỹ năng **chưa vẽ lại** theo app (việc còn lại của MB-57); cho tới khi cập nhật, mục 4 là bản ghi chính thức của các chỗ khác.

## 1. Màn hình

Nguồn dữ liệu: mọi API đi qua API Gateway (`/api/...`, JWT của Identity); Service là lớp `*Api` của feature (Dio).

### Chung (A)

| Mã | Màn | Route | Widget (View) | ViewModel | Nguồn dữ liệu | TT |
|---|---|---|---|---|---|---|
| A01 | Splash / giới thiệu | `/` | `SplashScreen` | `SplashViewModel` | `AuthController`, `OnboardingStore` (secure storage) | ✅ |
| A02 | Giới thiệu 3 trang | `/gioi-thieu` | `OnboardingScreen` | `OnboardingViewModel` | `OnboardingStore` | ✅ |
| A03 | Đăng nhập Google | `/dang-nhap` | `LoginScreen` | `LoginViewModel` | Google Sign-In → Firebase Auth → `POST /api/auth/google` | ⚠️ D1, D2 |
| A04 | Tài khoản chưa có trong danh sách | `/tai-khoan-chua-co` | `NotInListScreen` | `AuthController` | – | ✅ |
| A05 | Hoàn thiện hồ sơ | `ProfileGate` trước `/sv` | `CompleteProfileScreen` + `ProfileForm` | `ProfileFormViewModel` | `ProfileApi` (`/api/students/me`) | ⚠️ D5 |
| A06 | Thông báo | `/sv/thong-bao`, `/gv/thong-bao`, `/mentor/thong-bao` | `NotificationsScreen` | `NotificationsViewModel`, `UnreadCountViewModel` (badge) | `NotificationsApi` (`/api/notifications`) | ⚠️ D3 |
| A07 | Cài đặt | `/sv/cai-dat`, `/gv/…/cai-dat`, `/mentor/cai-dat` | `SettingsScreen` | `SettingsViewModel` | `SettingsApi` | ✅ |

### Sinh viên (S)

| Mã | Màn | Route | Widget | ViewModel | Nguồn dữ liệu | TT |
|---|---|---|---|---|---|---|
| S01 | Trang chủ | `/sv` (tab) | `HomeScreen` | `homeProvider`, `brandHeaderProvider` | `HomeApi`, `/api/semesters/current`, `/api/semesters/current/phase` | ⚠️ D3, D4 |
| S02 | Hồ sơ của tôi | `/sv/ho-so` (tab) | `MyProfileScreen` | `myProfileProvider` | `ProfileApi` | ⚠️ D3 |
| S03 | Sửa hồ sơ | `/sv/ho-so/sua` | `EditProfileScreen` + `ProfileForm` | `ProfileFormViewModel` | `ProfileApi` | ⚠️ D5 |
| S04 | Hồ sơ sinh viên khác | `/sv/sinh-vien/:id` | `StudentProfileScreen` | `studentProfileProvider` | `ProfileApi` | ✅ |
| S05 | Khám phá – nhóm đang tuyển | `/sv/kham-pha` (tab) | `DiscoverScreen` › `TeamsTab` | `TeamsViewModel` | `DiscoverApi` (`GET /api/teams`) | ⚠️ D3 |
| S06 | Bộ lọc (sheet) | trong S05 | `FilterSheet` | – (trạng thái giao diện) | `domainsProvider`, `specializationsProvider` | ✅ |
| S07 | Nhóm gợi ý | `/sv/kham-pha/goi-y` | `SuggestedTeamsScreen` | `suggestedTeamsProvider` (Remote Config `suggestions_max_items`) | `SuggestionsApi` → Matching gRPC | ✅ |
| S08 | Chi tiết nhóm | `/sv/kham-pha/nhom/:teamId` | `TeamDetailScreen` | `teamDetailProvider` | `DiscoverApi` (`GET /api/teams/{id}`) | ✅ |
| S09 | Gửi yêu cầu tham gia (sheet) | trong S08 | `JoinRequestSheet` | `JoinRequestViewModel` | `DiscoverApi` (`POST …/join-requests`) | ✅ |
| S10 | Sinh viên tìm nhóm | tab 2 của S05 | `SeekingPostsTab` | `SeekingPostsViewModel` | `PostsApi` | ✅ |
| S11 | Đăng bài tìm nhóm | `/sv/kham-pha/tim-nhom` | `SeekingPostFormScreen` | `SeekingPostFormViewModel` | `PostsApi` | ✅ |
| S12 | Lời mời / yêu cầu đã gửi | `/sv/loi-moi` | `InvitationsScreen` | `ReceivedInvitationsViewModel`, `SentJoinRequestsViewModel` | `InvitationsApi` | ✅ |
| S13 | Tạo nhóm | `/sv/nhom/tao` | `TeamFormScreen` | `TeamFormViewModel` | `MyTeamApi` (`POST /api/teams`) | ✅ |
| S14 | Chọn domain (sheet) | trong S13, S16 | `_DomainSheet` | – | `domainsProvider` | ✅ |
| S15 | Nhóm của tôi | `/sv/nhom` (tab) | `MyTeamScreen` | `myTeamProvider` | `MyTeamApi` | ⚠️ D3 |
| S16 | Sửa nhóm | `/sv/nhom/sua` | `EditTeamScreen` | `TeamFormViewModel` | `MyTeamApi` | ✅ |
| S17 | Thành viên | `/sv/nhom/thanh-vien` | `MembersScreen` | `MembersViewModel` | `MyTeamApi` | ✅ |
| S18 | Gợi ý thành viên | `/sv/nhom/goi-y` | `SuggestedMembersScreen` | `SuggestedMembersViewModel`, `InviteViewModel` | `SuggestionsApi` | ✅ |
| S19 | Tìm và mời sinh viên | `/sv/nhom/moi` | `InviteStudentsScreen` | `InviteStudentsViewModel`, `InviteViewModel` | `SuggestionsApi` | ✅ |
| S20 | Yêu cầu tham gia / lời mời đã gửi | `/sv/nhom/yeu-cau` | `RequestsScreen` | `JoinRequestsViewModel`, `SentInvitationsViewModel` | `MyTeamApi` | ✅ |
| S21 | Bài tuyển thành viên | `/sv/nhom/bai-tuyen` | `RecruitmentPostScreen` | `RecruitmentPostViewModel` | `PostsApi` | ✅ |
| S22 | Lịch sử nhóm | `/sv/nhom/lich-su` | `TeamHistoryScreen` | `TeamHistoryViewModel` | `TeamHistoryApi` | ✅ |
| S23–S25 | Nguyện vọng giảng viên, chi tiết, giảng viên của nhóm | `/sv/nhom/giang-vien…` | `LecturerPreferencesScreen`, `LecturerDetailScreen`, `TeamLecturerScreen` | `LecturerPreferencesViewModel`, `LecturerSearchViewModel`, `lecturerDetailProvider` | `LecturersApi` | ✅ |
| S26 | Yêu cầu đổi nhóm | `/sv/nhom/doi-nhom` (redirect) | `ChangeRequestListScreen` | `ChangeRequestListViewModel` | `ChangeRequestsApi` | ✅ |
| S27–S28 | Tạo yêu cầu đổi nhóm | `…/doi-nhom/tao` | `CreateChangeRequestScreen` | `CreateChangeRequestViewModel` | `ChangeRequestsApi` | ✅ |
| S29 | Chi tiết yêu cầu đổi nhóm | `…/doi-nhom/:id` | `ChangeRequestDetailScreen` | `changeRequestDetailProvider` | `ChangeRequestsApi` | ✅ |
| S30 | Đợt đã khóa | `/sv/nhom/da-khoa` | `RoundClosedScreen` | `myTeamProvider` | `MyTeamApi` | ✅ |
| S31 | Được ghép tự động | `/sv/nhom/ghep-tu-dong` | `AutoAssignedScreen` | `myTeamProvider` | `MyTeamApi` | ✅ |

### Dự án / workspace (C) – dùng chung cho sinh viên, giảng viên, mentor

| Mã | Màn | Route | Widget | ViewModel | Nguồn dữ liệu | TT |
|---|---|---|---|---|---|---|
| C01 | Dự án – tổng quan | `/sv/du-an` (tab sau lập nhóm), `/gv/du-an/:id` | `ProjectOverviewScreen` | `ProjectOverviewViewModel` | `CollabApi` | ⚠️ D3 |
| C02 | Tin nhắn – hội thoại | `…/tin-nhan`, `/gv/tin-nhan`, `/mentor/tin-nhan` (tab) | `ConversationsScreen`, `AdvisorConversationsScreen` | `AdvisorConversationsViewModel` | `MessagesApi` | ⚠️ D3 |
| C03 | Kênh tin nhắn | `…/tin-nhan/:channelId` | `ChannelScreen` | `ChannelViewModel`, `ThreadViewModel` | `MessagesApi`, SignalR `CollabHub` | ✅ |
| C04 | Bảng việc | `…/viec` | `TaskBoardScreen` | `TaskBoardViewModel`, `TaskFormViewModel` | `CollabApi` | ✅ |
| C05 | Chi tiết việc / bài nộp | `…/viec/:taskId` | `TaskDetailScreen` | `TaskDetailViewModel`, `ProjectFilePickerViewModel` | `CollabApi` | ✅ |
| C06 | File dự án | `…/file` | `FilesScreen` | `FilesViewModel`, `FileDetailViewModel` | `CollabApi` (S3) | ✅ |
| C07 | Lịch họp | `…/lich-hop` | `MeetingsScreen` | `meetingListProvider` | `MeetingsApi` | ✅ |
| C08 | Đề xuất / chi tiết họp | `…/lich-hop/:meetingId` | `MeetingDetailScreen` | `MeetingDetailViewModel`, `ProposeMeetingViewModel` | `MeetingsApi` | ✅ |
| C09 | Lịch họp hằng tuần | `/sv/nhom/lich-hop`, `/gv/lich-hop` | `TeamScheduleScreen`, `LecturerScheduleScreen` | `TeamScheduleViewModel`, `LecturerScheduleViewModel` | `MeetingScheduleApi`, `CollabHub` | ✅ |

### Giảng viên (L), Mentor (M)

| Mã | Màn | Route | Widget | ViewModel | Nguồn dữ liệu | TT |
|---|---|---|---|---|---|---|
| L01 | Trang chủ giảng viên | `/gv` (tab) | `LecturerHomeScreen` | `LecturerHomeViewModel` | `LecturerHomeApi` (`/api/advisor/overview`, checkpoint, lịch) | ⚠️ D3 |
| L02, L03 | Yêu cầu hướng dẫn | – | – | – | – | Bỏ (user-flow 1.1a: ghép tự động thay cho chấp nhận/từ chối) |
| L04 | Nhóm tôi hướng dẫn | `/gv/nhom`, `/mentor/nhom` (tab) | `AdvisedTeamsScreen` | `AdvisedTeamsViewModel` | `LecturerHomeApi` | ⚠️ D3 |
| L05 | Chi tiết nhóm + Checkpoint | `/gv/nhom/:teamId`, `/mentor/nhom/:teamId` | `AdvisedTeamDetailScreen` | `AdvisedTeamDetailViewModel`, `CheckpointViewModel` | `LecturerHomeApi` | ✅ |
| L06 | Hồ sơ giảng viên | `/gv/…/ho-so` | `LecturerProfileScreen` | `LecturerProfileViewModel` | `LecturerProfileApi` | ✅ |
| L07 | Đề tài | `/gv/de-tai`, `/mentor/de-tai` (tab) | `TopicInterestsScreen`, `TopicDetailScreen` | `TopicInterestsViewModel`, `TopicDetailViewModel` | `LecturerHomeApi` | ⚠️ D3 |
| – | Lịch (tab giảng viên EXE201) | `/gv/lich` (tab) | `ScreenPlaceholder` | – | – | ⏳ chưa có màn riêng; lịch họp xem ở C09 `/gv/lich-hop` |
| M01 | Trang chủ mentor | `/mentor` (tab) | `MentorHomeScreen` | `MentorHomeViewModel` | `LecturerHomeApi` | ⚠️ D3 |

### Web Bộ môn (W)

Cột Widget là component React; nguồn dữ liệu qua `web/src/shared/api`. Màn này thêm sau Lab 2 nên chưa có frame Figma.

| Mã | Màn | Route | Component | Hook dữ liệu | Nguồn dữ liệu | TT |
|---|---|---|---|---|---|---|
| W30 | Theo dõi lỗi ứng dụng (FE-50) | `/admin/loi-ung-dung` | `ClientErrorsPage`, `DailyErrorChart`, `ClientErrorDrawer` | `useClientErrorSummary`, `useClientErrors` | `GET /api/admin/client-errors`, `/summary` (BE-87); app gửi qua `POST /api/client-errors` (MB-61); link sang Crashlytics console | ⚠️ D11 |

## 2. Luồng Lab 2

| Luồng | Các bước (màn) | Lỗi / nhánh phụ | Đi được trong app | Test |
|---|---|---|---|---|
| F1 – Sinh viên chưa có nhóm tham gia nhóm | A03 → A05 → S01 → S05/S07 → S08 → S09 → S12 | E1 email ngoài danh sách → A04; hết đợt → hộp nhắc `round-closed` | ✅ (thử trên điện thoại thật 05/11) | `login_test`, `discover_test`, `teams_view_model_test` |
| F2 – Trưởng nhóm EXE201 đổi thành viên | S15 → S26 → S27 → S28 → S29 | Lý do < 10 ký tự; gửi lỗi giữ nội dung | ✅ | `change_requests_test`, `change_request*_view_model_test` |
| F3 – Giảng viên (bản 4: ghép tự động + Checkpoint) | L01 → L04 → L05 (Checkpoint) / L07 | Ngày Checkpoint tương lai bị chặn; lỗi tải có Thử lại | ✅ (widget test; chưa thử tài khoản giảng viên trên máy thật) | `advisor_mobile_screens_test`, `lecturer_*_view_model_test` |
| F4–F9 (bổ sung) | C07/C08, S23–S25, C05, C06, C09, L05 | xem user-flow 4a | ✅ | test theo feature |

## 3. Component Lab 2 → widget

| Component (DESIGN.md / Figma `05 Components`) | Widget | File |
|---|---|---|
| Token màu, chữ, khoảng cách, bo góc | `AppTheme.light()`, `AppSpacing`, `AppRadius`, `AppColors`, `AppMotion` | `core/theme/` |
| Button/Primary (+ trạng thái đang gửi) | `LoadingButton` (thêm `leading` cho logo Google) | `shared/widgets/states.dart` |
| Loading / Empty / Error / Forbidden | `SkeletonList`, `EmptyState`, `ErrorState`, `ErrorBanner`, `ForbiddenState` | `shared/widgets/states.dart` |
| TeamRequirementBar (chấm thành viên + điều kiện) | `TeamRequirementBar` | `shared/widgets/team_requirement_bar.dart` |
| DeadlineBanner | `DeadlineBanner` | `shared/widgets/deadline_banner.dart` |
| SkillSlider (1–5) | `SkillSlider` | `features/profile/profile_form.dart` |
| Thẻ nhóm (người ngoài / gợi ý) | `TeamListCard`, `TeamSuggestionCard`, `FitChip`, `DomainChips` | `features/discover/`, `features/suggestions/` |
| Thanh tab theo vai trò | `RoleShell` (Material `NavigationBar`) | `app/role_shell.dart` |
| App bar | `BrandAppBar` (mới, D3) | `shared/widgets/brand_app_bar.dart` |
| Logo ứng dụng | `AppLogo` (mới, D1) | `shared/widgets/app_logo.dart` |

## 4. Khác thiết kế (deviation)

| # | Chỗ khác | Thiết kế Lab 2 | App | Lý do | Ngày / PR |
|---|---|---|---|---|---|
| D1 | A03 logo | Spec A03 có "logo và tên ứng dụng ở nửa trên", "nút có icon Google" nhưng bản code đầu không có | Logo `AppLogo` (FPT Orange, icon nhóm của A01); logo G chính thức (bộ asset Sign in with Google, Light, tròn) ở đầu nút | Khớp lại spec; quy định thương hiệu Google bắt dùng logo G nguyên bản trên nền trắng | 04/11, #142 |
| D2 | A03 hủy đăng nhập | "Về mặc định, không báo lỗi" | Không banner lỗi, nhưng hiện SnackBar "Bạn đã hủy đăng nhập…" | Lab 3 §6: "cancelled sign-in shows a message" | 04/11, #142 |
| D3 | App bar các tab | Tiêu đề chữ trơn ("Trang chủ", "Khám phá"…); S01, S05, S15, C02 không có app bar | `BrandAppBar` chung: logo, tên màn, dòng "môn · học kỳ", nút của màn giữ nguyên; màn mở bằng push hiện Back thay logo | Góp ý trưởng nhóm 05/11: header trống, thiếu điểm nhấn; đồng bộ nhận diện giữa các tab (tham khảo DailyMate) | 05/11, #150 |
| D4 | S01 header | Lời chào "Chào …" | Chip "Còn n ngày" (hạn lập nhóm), lời chào đậm, ngày + "Tuần x/y", vạch nhấn tiến độ học kỳ (có nhãn đọc màn hình) | Như D3; cho sinh viên biết ngay còn bao lâu và đang ở đâu trong học kỳ | 05/11, #150 |
| D5 | SkillSlider (A05, S03) | Thanh 5 nấc | Khi kéo nút đi liền theo tay, thả tay mới làm tròn 1–5; lúc không kéo vẫn 5 nấc cho TalkBack | Kéo nhảy nấc bị giật (góp ý khi thử trên máy thật 04/11) | 04/11, #142 |
| D6 | SafeArea | – (Figma không thể hiện tai thỏ / camera) | Màn tab không có app bar riêng được bọc `SafeArea` / `BrandAppBar` | Lời chào S01, S05, S15 bị đè dưới thanh trạng thái trên máy thật | 04/11, #142 → thay bằng D3 ở #150 |
| D7 | Chuyển màn | Prototype Figma: chuyển tức thì | Đổi tab: fade through 200 ms (mờ dần + phóng 98% → 100%); màn cấp 2: FadeForwards (Android); vạch tiến độ chạy dần; tắt hết khi máy bật giảm chuyển động | Góp ý trưởng nhóm 05/11: chuyển tab cho mượt; DESIGN.md 4.5 cho phép chuyển động ≤ 300 ms | 05/11, #150 |
| D8 | Thẻ "Nhóm phù hợp với bạn" (S01) | Thẻ rộng hết hàng | (sửa lỗi) thẻ co theo nội dung → đã kéo rộng hết hàng | Lỗi code, không phải đổi thiết kế; ghi để truy vết | 05/11, #150 |
| D9 | L02, L03 | Có màn chấp nhận / từ chối yêu cầu hướng dẫn | Bỏ | Đổi nghiệp vụ 20/10 sang ghép giảng viên tự động (design-decisions Phần E) | 20/10 |
| D10 | Web Bộ môn (W01, W02, W25 và bảng dữ liệu) | Khung web như mobile: sidebar trắng một danh sách, header trơn, thẻ phẳng | Sidebar nền mực chia nhóm (Học vụ · Nhóm và ghép · Vận hành), header kính mờ có ngày; W02 có thẻ hero (đếm ngược hạn đợt, vòng tiến độ) và thẻ số liệu đếm lên; W01 hai cột (panel giới thiệu + thẻ đăng nhập); bóng nhiều lớp, skeleton shimmer, trang mờ dần khi đổi route, tắt khi giảm chuyển động | Góp ý trưởng nhóm 05/11: web nhìn như bản mobile phóng to; web chỉ phục vụ Bộ môn (góp ý giảng viên) nên thiết kế lại cho màn hình rộng. Không ảnh hưởng app mobile | 05/11, FE-49 |
| D11 | Web Sinh viên / Giảng viên / Mentor (khung FE-32, S01 web) | Khung web giống mobile: sidebar trắng, tiêu đề chữ trơn, S01 là các thẻ xếp dọc | Cùng ngôn ngữ với web Bộ môn (D10): sidebar / rail nền mực có vạch cam, thẻ giai đoạn cuối sidebar (môn, hạn lập nhóm), header kính mờ có ngày, chuyển trang mờ dần; S01 web có thẻ hero (lời chào, việc nên làm, nút, đếm ngược hạn lập nhóm), hàng thẻ số liệu, lưới gợi ý 3 cột. S05/S10 web (FE-52): tab dạng viên thuốc, bộ lọc S06 nằm cố định cột trái từ 1280 px (màn hẹp vẫn là drawer), banner "Gợi ý cho bạn" / "Đăng bài tìm nhóm" nền mực, lọc chuyên ngành bằng chip, thẻ nhóm có ô chữ cái đầu và "Xem nhóm". Giữ bề rộng DESIGN.md 8.2 (sidebar 240, nội dung 720 / 1200); app mobile không đổi | Góp ý trưởng nhóm 05/11: web sinh viên nhìn như mobile phóng to | 05/11, FE-51, FE-52 |
| D11 | Web Bộ môn (W30) | Không có màn này trong thiết kế Lab 2 | Thêm W30 theo giao diện FE-49, lấy số liệu từ báo cáo app gửi về backend | Giảng viên muốn Bộ môn xem kết quả Crashlytics trên web; Firebase không có API đọc Crashlytics miễn phí (BigQuery export cần gói Blaze) | – |
