import type { Shell } from '@/shared/auth/AuthProvider'

/**
 * Mục điều hướng của từng khung (user-flow.md mục 0, DESIGN.md 8.1–8.2). `screen` là mã màn hình,
 * `owner` là người làm (AGENTS.md mục 2) – để mỗi người biết route nào thuộc mình.
 */
export interface NavItem {
  to: string
  label: string
  icon: string
  screen: string
  owner: string
  /** Khớp chính xác đường dẫn (mục trang chủ). */
  end?: boolean
  /** Nhóm trong sidebar Bộ môn (FE-49). */
  group?: string
}

export const adminNav: NavItem[] = [
  { to: '/admin', label: 'Tổng quan', icon: 'dashboard', screen: 'W02', owner: 'Nguyên', end: true },
  { to: '/admin/hoc-ky', label: 'Học kỳ và đợt', icon: 'calendar_month', screen: 'W03–W04', owner: 'Nam', group: 'Học vụ' },
  { to: '/admin/import', label: 'Import sinh viên', icon: 'upload_file', screen: 'W06–W08', owner: 'Nam', group: 'Học vụ' },
  { to: '/admin/sinh-vien', label: 'Sinh viên', icon: 'school', screen: 'W09–W10', owner: 'Nam', group: 'Học vụ' },
  { to: '/admin/giang-vien', label: 'Giảng viên', icon: 'co_present', screen: 'W11', owner: 'Nam', group: 'Học vụ' },
  { to: '/admin/mentor', label: 'Mentor', icon: 'support_agent', screen: 'W27', owner: 'Nam', group: 'Học vụ' },
  { to: '/admin/nhom', label: 'Nhóm', icon: 'groups', screen: 'W15–W16', owner: 'Cường', group: 'Nhóm và ghép' },
  { to: '/admin/doi-nhom', label: 'Yêu cầu đổi nhóm', icon: 'swap_horiz', screen: 'W17–W18', owner: 'Thành', group: 'Nhóm và ghép' },
  { to: '/admin/ghep-giang-vien', label: 'Ghép giảng viên', icon: 'how_to_reg', screen: 'W19', owner: 'Thiên', group: 'Nhóm và ghép' },
  { to: '/admin/ghep-mentor', label: 'Ghép mentor', icon: 'handshake', screen: 'W28', owner: 'Thiên', group: 'Nhóm và ghép' },
  { to: '/admin/chua-co-nhom', label: 'Chưa có nhóm', icon: 'person_search', screen: 'W20–W22', owner: 'Nguyên', group: 'Nhóm và ghép' },
  { to: '/admin/de-tai', label: 'Đề tài qua các kỳ', icon: 'history_edu', screen: 'W29', owner: 'Thành', group: 'Nhóm và ghép' },
  { to: '/admin/thong-bao', label: 'Thông báo chung', icon: 'campaign', screen: 'W23', owner: 'Thành', group: 'Vận hành' },
  { to: '/admin/nhat-ky', label: 'Nhật ký', icon: 'receipt_long', screen: 'W24', owner: 'Cường', group: 'Vận hành' },
  { to: '/admin/loi-ung-dung', label: 'Theo dõi lỗi ứng dụng', icon: 'bug_report', screen: 'W30', owner: 'Nguyên', group: 'Vận hành' },
  { to: '/admin/danh-muc', label: 'Danh mục', icon: 'category', screen: 'W12–W14', owner: 'Nam', group: 'Vận hành' },
  { to: '/admin/cau-hinh', label: 'Cấu hình', icon: 'settings', screen: 'W05', owner: 'Nam', group: 'Vận hành' },
]

export interface ShellContext {
  /** Sinh viên: môn của mình đang lập nhóm. Giảng viên: có môn đang lập nhóm. */
  isTeamFormation: boolean
  /** Mentor: đã tới tuần bắt đầu mentor. */
  isMentoringWindow: boolean
}

/** Tab bar đổi theo giai đoạn (user-flow.md mục 0). Tối đa 5 mục. */
export function roleNav(shell: Exclude<Shell, 'admin'>, ctx: ShellContext): NavItem[] {
  switch (shell) {
    case 'student':
      return [
        { to: '/sv', label: 'Trang chủ', icon: 'home', screen: 'S01', owner: 'Cường', end: true },
        ctx.isTeamFormation
          ? { to: '/sv/kham-pha', label: 'Khám phá', icon: 'travel_explore', screen: 'S05', owner: 'Cường' }
          : { to: '/sv/du-an', label: 'Dự án', icon: 'rocket_launch', screen: 'C01', owner: 'Nguyên' },
        { to: '/sv/nhom', label: 'Nhóm', icon: 'groups', screen: 'S15', owner: 'Cường' },
        // Từ 06/11/2026 (MB-62, FE-53): Tin nhắn thành mục riêng, Hồ sơ (S02) mở từ menu tài khoản – design-traceability.md D12.
        { to: '/sv/tin-nhan', label: 'Tin nhắn', icon: 'forum', screen: 'C02', owner: 'Cường' },
        { to: '/sv/thong-bao', label: 'Thông báo', icon: 'notifications', screen: 'A06', owner: 'Thành' },
      ]
    case 'lecturer':
      return ctx.isTeamFormation
        ? [
            { to: '/gv', label: 'Trang chủ', icon: 'home', screen: 'L01', owner: 'Thành', end: true },
            { to: '/gv/de-tai', label: 'Đề tài', icon: 'lightbulb', screen: 'L07', owner: 'Thành' },
            { to: '/gv/nhom', label: 'Nhóm', icon: 'groups', screen: 'L04', owner: 'Thành' },
            { to: '/gv/tin-nhan', label: 'Tin nhắn', icon: 'forum', screen: 'C02', owner: 'Cường' },
            { to: '/gv/thong-bao', label: 'Thông báo', icon: 'notifications', screen: 'A06', owner: 'Thành' },
          ]
        : [
            { to: '/gv', label: 'Trang chủ', icon: 'home', screen: 'L01', owner: 'Thành', end: true },
            { to: '/gv/nhom', label: 'Nhóm', icon: 'groups', screen: 'L04', owner: 'Thành' },
            { to: '/gv/lich', label: 'Lịch', icon: 'event', screen: 'C07', owner: 'Nam' },
            { to: '/gv/tin-nhan', label: 'Tin nhắn', icon: 'forum', screen: 'C02', owner: 'Cường' },
            { to: '/gv/thong-bao', label: 'Thông báo', icon: 'notifications', screen: 'A06', owner: 'Thành' },
          ]
    case 'mentor':
      return ctx.isMentoringWindow
        ? [
            { to: '/mentor', label: 'Trang chủ', icon: 'home', screen: 'M01', owner: 'Thành', end: true },
            { to: '/mentor/nhom', label: 'Nhóm', icon: 'groups', screen: 'L04', owner: 'Thành' },
            { to: '/mentor/tin-nhan', label: 'Tin nhắn', icon: 'forum', screen: 'C02', owner: 'Cường' },
            { to: '/mentor/thong-bao', label: 'Thông báo', icon: 'notifications', screen: 'A06', owner: 'Thành' },
          ]
        : [
            { to: '/mentor', label: 'Trang chủ', icon: 'home', screen: 'M01', owner: 'Thành', end: true },
            { to: '/mentor/de-tai', label: 'Đề tài', icon: 'lightbulb', screen: 'L07', owner: 'Thành' },
            { to: '/mentor/thong-bao', label: 'Thông báo', icon: 'notifications', screen: 'A06', owner: 'Thành' },
          ]
  }
}
