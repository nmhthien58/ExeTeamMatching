String _two(int v) => v.toString().padLeft(2, '0');

/// `18:00 · 17/09/2026` (DESIGN.md mục 9).
String formatDateTime(DateTime d) {
  final l = d.toLocal();
  return '${_two(l.hour)}:${_two(l.minute)} · ${_two(l.day)}/${_two(l.month)}/${l.year}';
}

/// "vừa xong", "15 phút trước", "2 giờ trước", "3 ngày trước".
String formatRelative(DateTime d, {DateTime? now}) {
  final diff = (now ?? DateTime.now()).difference(d);
  if (diff.inMinutes < 1) return 'vừa xong';
  if (diff.inMinutes < 60) return '${diff.inMinutes} phút trước';
  if (diff.inHours < 24) return '${diff.inHours} giờ trước';
  if (diff.inDays < 30) return '${diff.inDays} ngày trước';
  final l = d.toLocal();
  return '${_two(l.day)}/${_two(l.month)}/${l.year}';
}

/// "820 KB", "2,4 MB".
String formatSize(int bytes) {
  if (bytes < 1024) return '$bytes B';
  if (bytes < 1024 * 1024) return '${(bytes / 1024).round()} KB';
  return '${(bytes / (1024 * 1024)).toStringAsFixed(1).replaceAll('.', ',')} MB';
}

/// Hai chữ cái đầu cho avatar chữ.
String initials(String name) {
  final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) return '?';
  if (parts.length == 1) return parts.first.substring(0, parts.first.length.clamp(1, 2)).toUpperCase();
  return (parts[parts.length - 2][0] + parts.last[0]).toUpperCase();
}

enum DueTone { neutral, warning, error }

/// Chip hạn của thẻ việc (DESIGN.md 6.3): "Hôm nay", "Ngày mai", "Còn 2 ngày", "Trễ 1 ngày".
(String, DueTone) dueLabel(DateTime due, {DateTime? now, bool? overdue}) {
  final n = (now ?? DateTime.now()).toLocal();
  final d = due.toLocal();
  final days = DateTime(d.year, d.month, d.day).difference(DateTime(n.year, n.month, n.day)).inDays;
  if (overdue ?? d.isBefore(n)) return (days >= 0 ? 'Trễ hạn' : 'Trễ ${-days} ngày', DueTone.error);
  if (days == 0) return ('Hôm nay', DueTone.warning);
  if (days == 1) return ('Ngày mai', DueTone.warning);
  return ('Còn $days ngày', days <= 2 ? DueTone.warning : DueTone.neutral);
}
