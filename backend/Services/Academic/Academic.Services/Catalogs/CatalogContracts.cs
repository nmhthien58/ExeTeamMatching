namespace Academic.Services.Catalogs;

// Schema của academic.yaml (tag Catalogs). JSON camelCase.

/// <summary>Schema <c>CatalogItem</c>.</summary>
public sealed record CatalogItem(int Id, string Code, string Name, string? Description, bool IsActive, int? MajorId = null);

/// <summary>
/// Schema <c>CatalogInput</c>. <see cref="IsActive"/> bỏ trống: thêm mới thì true, sửa thì giữ nguyên.
/// Mã lớp không có cột tên / mô tả nên hai trường này được bỏ qua; chỉ domain lưu mô tả.
/// </summary>
public sealed record CatalogInput(string? Code, string? Name, string? Description, bool? IsActive, int? MajorId = null);
