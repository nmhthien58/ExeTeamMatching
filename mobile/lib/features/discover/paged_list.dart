/// Danh sách phân trang đã tải (các trang ghép lại) cùng trạng thái tải thêm / tải lại – dùng trong `ViewData` của
/// S10 (bài tìm nhóm) và S22 (lịch sử nhóm).
class PagedList<T> {
  const PagedList({required this.items, required this.page, required this.hasMore, this.loading = false, this.loadMoreError});

  final List<T> items;
  final int page;
  final bool hasMore;

  /// Đang tải trang kế hoặc tải lại trang 1 (giữ danh sách cũ trên màn).
  final bool loading;

  /// Lỗi khi tải thêm / tải lại: giữ danh sách, hiện banner "Không tải được trang tiếp theo".
  final Object? loadMoreError;

  PagedList<T> copyWith({bool? loading, Object? loadMoreError}) =>
      PagedList(items: items, page: page, hasMore: hasMore, loading: loading ?? this.loading, loadMoreError: loadMoreError);
}
