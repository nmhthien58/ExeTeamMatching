import 'package:flutter/material.dart';

import '../core/auth/session.dart';

/// Khung theo vai trò trên mobile (Admin chỉ dùng web).
enum Shell { student, lecturer, mentor }

/// Sinh viên → khung SV; giảng viên (kể cả làm mentor) → khung GV; chỉ mentor → khung Mentor.
Shell? shellOf(UserInfo user) {
  if (user.roles.contains(Role.lecturer)) return Shell.lecturer;
  if (user.roles.contains(Role.mentor)) return Shell.mentor;
  if (user.roles.contains(Role.student)) return Shell.student;
  return null; // chỉ Admin → không có khung mobile
}

const shellHome = {Shell.student: '/sv', Shell.lecturer: '/gv', Shell.mentor: '/mentor'};

/// Màn mở khi bấm avatar ở góc phải BrandAppBar (MB-62): S02, L06; mentor chưa có màn hồ sơ nên mở Cài đặt.
const shellProfile = {Shell.student: '/sv/ho-so', Shell.lecturer: '/gv/ho-so', Shell.mentor: '/mentor/cai-dat'};

/// Một tab của tab bar. `screen` là mã màn hình, `owner` là người làm (AGENTS.md mục 2).
class NavItem {
  const NavItem(this.path, this.label, this.icon, this.selectedIcon, this.screen, this.owner);

  final String path;
  final String label;
  final IconData icon;
  final IconData selectedIcon;
  final String screen;
  final String owner;
}

/// Tab bar đổi theo giai đoạn (user-flow.md mục 0, MB-05, MB-37).
List<NavItem> roleNav(Shell shell, {required bool isTeamFormation, required bool isMentoringWindow}) => switch (shell) {
      Shell.student => [
          const NavItem('/sv', 'Trang chủ', Icons.home_outlined, Icons.home, 'S01', 'Cường'),
          isTeamFormation
              ? const NavItem('/sv/kham-pha', 'Khám phá', Icons.travel_explore_outlined, Icons.travel_explore, 'S05', 'Cường')
              : const NavItem('/sv/du-an', 'Dự án', Icons.rocket_launch_outlined, Icons.rocket_launch, 'C01', 'Nguyên'),
          const NavItem('/sv/nhom', 'Nhóm', Icons.groups_outlined, Icons.groups, 'S15', 'Cường'),
          // Từ 06/11/2026 (MB-62): Tin nhắn thành tab, Hồ sơ (S02) chuyển lên avatar ở BrandAppBar – design-traceability.md D12.
          const NavItem('/sv/tin-nhan', 'Tin nhắn', Icons.forum_outlined, Icons.forum, 'C02', 'Cường'),
          const NavItem('/sv/thong-bao', 'Thông báo', Icons.notifications_outlined, Icons.notifications, 'A06', 'Thành'),
        ],
      Shell.lecturer => isTeamFormation
          ? const [
              NavItem('/gv', 'Trang chủ', Icons.home_outlined, Icons.home, 'L01', 'Thành'),
              NavItem('/gv/de-tai', 'Đề tài', Icons.lightbulb_outline, Icons.lightbulb, 'L07', 'Thành'),
              NavItem('/gv/nhom', 'Nhóm', Icons.groups_outlined, Icons.groups, 'L04', 'Thành'),
              NavItem('/gv/tin-nhan', 'Tin nhắn', Icons.forum_outlined, Icons.forum, 'C02', 'Cường'),
              NavItem('/gv/thong-bao', 'Thông báo', Icons.notifications_outlined, Icons.notifications, 'A06', 'Thành'),
            ]
          : const [
              NavItem('/gv', 'Trang chủ', Icons.home_outlined, Icons.home, 'L01', 'Thành'),
              NavItem('/gv/nhom', 'Nhóm', Icons.groups_outlined, Icons.groups, 'L04', 'Thành'),
              NavItem('/gv/lich', 'Lịch', Icons.event_outlined, Icons.event, 'C07', 'Nam'),
              NavItem('/gv/tin-nhan', 'Tin nhắn', Icons.forum_outlined, Icons.forum, 'C02', 'Cường'),
              NavItem('/gv/thong-bao', 'Thông báo', Icons.notifications_outlined, Icons.notifications, 'A06', 'Thành'),
            ],
      Shell.mentor => isMentoringWindow
          ? const [
              NavItem('/mentor', 'Trang chủ', Icons.home_outlined, Icons.home, 'M01', 'Thành'),
              NavItem('/mentor/nhom', 'Nhóm', Icons.groups_outlined, Icons.groups, 'L04', 'Thành'),
              NavItem('/mentor/tin-nhan', 'Tin nhắn', Icons.forum_outlined, Icons.forum, 'C02', 'Cường'),
              NavItem('/mentor/thong-bao', 'Thông báo', Icons.notifications_outlined, Icons.notifications, 'A06', 'Thành'),
            ]
          : const [
              NavItem('/mentor', 'Trang chủ', Icons.home_outlined, Icons.home, 'M01', 'Thành'),
              NavItem('/mentor/de-tai', 'Đề tài', Icons.lightbulb_outline, Icons.lightbulb, 'L07', 'Thành'),
              NavItem('/mentor/thong-bao', 'Thông báo', Icons.notifications_outlined, Icons.notifications, 'A06', 'Thành'),
            ],
    };

/// Mọi tab có thể có của khung (cả hai giai đoạn) để đường dẫn cũ không lỗi.
List<NavItem> allTabs(Shell shell) {
  final seen = <String>{};
  return [
    ...roleNav(shell, isTeamFormation: true, isMentoringWindow: false),
    ...roleNav(shell, isTeamFormation: false, isMentoringWindow: true),
  ].where((i) => seen.add(i.path)).toList();
}
