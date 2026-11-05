/** Ngày hôm nay kiểu "thứ năm, 05/11/2026" cho dòng phụ của header (FE-49, FE-51). */
export function todayLabel(): string {
  return new Date().toLocaleDateString('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' })
}
