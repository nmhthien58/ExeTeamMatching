using Academic.Repositories.Catalogs;
using Academic.Services.Catalogs;
using BuildingBlocks.Errors;

namespace Academic.Tests;

/// <summary>Kiểm tra dữ liệu danh mục theo CHECK của DB script (BE-21).</summary>
public sealed class CatalogRulesTests
{
    [Fact]
    public void Code_is_trimmed_and_upper_cased()
    {
        var row = CatalogRules.Validate(CatalogKind.Specializations, new(" se_ai ", " Trí tuệ nhân tạo ", "bỏ qua", null), 0, true);

        Assert.Equal(new CatalogRow(0, "SE_AI", "Trí tuệ nhân tạo", null, true), row);
    }

    [Theory]
    [InlineData(CatalogKind.Specializations, "S")]
    [InlineData(CatalogKind.Specializations, "SE-AI")]
    [InlineData(CatalogKind.Domains, "D19")]
    [InlineData(CatalogKind.Domains, "D00")]
    [InlineData(CatalogKind.ClassCodes, "SE")]
    [InlineData(CatalogKind.ClassCodes, "SE_1801")]
    public void Invalid_code_is_rejected(CatalogKind kind, string code)
    {
        var ex = Assert.Throws<RequestValidationException>(() => CatalogRules.Validate(kind, new(code, "Tên", null, null), 0, true));

        Assert.Contains("code", ex.Errors.Keys);
    }

    [Fact]
    public void Missing_name_and_long_description_are_reported_per_field()
    {
        var ex = Assert.Throws<RequestValidationException>(() =>
            CatalogRules.Validate(CatalogKind.Domains, new("D05", "  ", new string('x', 501), null), 0, true));

        Assert.Equal(["description", "name"], ex.Errors.Keys.Order());
    }

    [Fact]
    public void Class_code_uses_code_as_name_and_ignores_description()
    {
        var row = CatalogRules.Validate(CatalogKind.ClassCodes, new("se1801", null, "bỏ qua", false), 3, false);

        Assert.Equal(new CatalogRow(3, "SE1801", "SE1801", null, false), row);
    }

    [Fact]
    public void Domain_keeps_description()
    {
        var row = CatalogRules.Validate(CatalogKind.Domains, new("d07", "Giáo dục", " Mô tả ", true), 7, true);

        Assert.Equal(new CatalogRow(7, "D07", "Giáo dục", "Mô tả", true), row);
    }
}
