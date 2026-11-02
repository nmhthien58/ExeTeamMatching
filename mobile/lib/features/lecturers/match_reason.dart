String? lecturerMatchReason(String? reason, int? rank) => switch (reason) {
      'MUTUAL' => 'Hai bên cùng chọn',
      'PREFERENCE' => rank == null ? 'Nguyện vọng' : 'Nguyện vọng $rank',
      'LECTURER_INTEREST' => 'Giảng viên quan tâm',
      'PREVIOUS_INSTRUCTOR' => 'Đã hướng dẫn ở EXE101',
      'DOMAIN' => 'Theo chuyên môn',
      'MANUAL' => 'Bộ môn phân công',
      _ => null,
    };
