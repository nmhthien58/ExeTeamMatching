/// Trạng thái hiển thị của một màn tải dữ liệu (MVVM, Lab 3 §5): đang tải / trống / lỗi / có dữ liệu.
/// ViewModel giữ giá trị này trong state của mình; View dùng `switch` để vẽ đúng widget trạng thái
/// (`SkeletonList`, `EmptyState`, `ErrorState`). Xem docs/architecture.md.
sealed class ViewState<T> {
  const ViewState();

  /// Dữ liệu hiện có (chỉ khi [ViewData]).
  T? get valueOrNull => switch (this) {
        ViewData<T>(:final value) => value,
        _ => null,
      };
}

final class ViewLoading<T> extends ViewState<T> {
  const ViewLoading();
}

final class ViewEmpty<T> extends ViewState<T> {
  const ViewEmpty();
}

final class ViewError<T> extends ViewState<T> {
  const ViewError(this.error);

  /// Thường là `ApiException`; `ErrorState` tự chọn câu thông báo.
  final Object error;
}

final class ViewData<T> extends ViewState<T> {
  const ViewData(this.value);

  final T value;
}
