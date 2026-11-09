using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Team.Services.Academic;

namespace Team.Services.BaselineImport;

/// <summary>
/// Ánh xạ tên Domain từ file FAP (sheet Group List / DOMAIN NGHIÊN CỨU) sang mã danh mục D01–D18 của hệ thống (BE-92).
/// Khi Bộ môn EXE hoặc FAP bổ sung / thay đổi cách ghi tên domain, cập nhật bảng từ điển <see cref="AliasMap"/> tại đây.
/// </summary>
public static class BaselineDomainMapper
{
    private static readonly Regex CodeRegex = new(@"^D0?([1-9]|1[0-8])$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Bảng ánh xạ alias / biến thể tên gọi thường gặp của FAP sang mã Domain chuẩn (D01–D18).
    /// Key là chuỗi đã chuẩn hóa (bỏ dấu, lowercase, bỏ ký tự đặc biệt).
    /// </summary>
    private static readonly Dictionary<string, string> AliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // D01 – AI & Data Innovation
        ["d01"] = "D01",
        ["ai & data innovation"] = "D01",
        ["ai and data innovation"] = "D01",
        ["ai & data"] = "D01",
        ["ai and data"] = "D01",
        ["ai"] = "D01",
        ["tri tue nhan tao"] = "D01",
        ["data innovation"] = "D01",
        ["data"] = "D01",

        // D02 – Robotics & Automation
        ["d02"] = "D02",
        ["robotics & automation"] = "D02",
        ["robotics and automation"] = "D02",
        ["robotics"] = "D02",
        ["automation"] = "D02",
        ["tu dong hoa"] = "D02",
        ["robot"] = "D02",

        // D03 – IoT & Smart Systems
        ["d03"] = "D03",
        ["iot & smart systems"] = "D03",
        ["iot and smart systems"] = "D03",
        ["iot"] = "D03",
        ["smart systems"] = "D03",
        ["internet of things"] = "D03",
        ["he thong thong minh"] = "D03",

        // D04 – Software / SaaS / Platform
        ["d04"] = "D04",
        ["software / saas / platform"] = "D04",
        ["software saas platform"] = "D04",
        ["software"] = "D04",
        ["saas"] = "D04",
        ["platform"] = "D04",
        ["phan mem"] = "D04",
        ["ky thuat phan mem"] = "D04",

        // D05 – Computer Foundation & Digital Literacy
        ["d05"] = "D05",
        ["computer foundation & digital literacy"] = "D05",
        ["computer foundation and digital literacy"] = "D05",
        ["computer foundation"] = "D05",
        ["digital literacy"] = "D05",
        ["tin hoc dai cuong"] = "D05",

        // D06 – Semiconductor & Embedded Deep Tech
        ["d06"] = "D06",
        ["semiconductor & embedded deep tech"] = "D06",
        ["semiconductor and embedded deep tech"] = "D06",
        ["semiconductor"] = "D06",
        ["embedded deep tech"] = "D06",
        ["embedded"] = "D06",
        ["deep tech"] = "D06",
        ["ban dan"] = "D06",
        ["vi mach"] = "D06",
        ["vi mach ban dan"] = "D06",

        // D07 – Digital Automotive & Mobility
        ["d07"] = "D07",
        ["digital automotive & mobility"] = "D07",
        ["digital automotive and mobility"] = "D07",
        ["digital automotive"] = "D07",
        ["automotive"] = "D07",
        ["mobility"] = "D07",
        ["o to so"] = "D07",

        // D08 – Cybersecurity & Trust
        ["d08"] = "D08",
        ["cybersecurity & trust"] = "D08",
        ["cybersecurity and trust"] = "D08",
        ["cybersecurity"] = "D08",
        ["trust"] = "D08",
        ["security"] = "D08",
        ["an toan thong tin"] = "D08",
        ["bao mat"] = "D08",

        // D09 – Media & Communication Innovation
        ["d09"] = "D09",
        ["media & communication innovation"] = "D09",
        ["media and communication innovation"] = "D09",
        ["media"] = "D09",
        ["communication"] = "D09",
        ["truyen thong"] = "D09",

        // D10 – Graphic Design / UX / Brand
        ["d10"] = "D10",
        ["graphic design / ux / brand"] = "D10",
        ["graphic design ux brand"] = "D10",
        ["graphic design"] = "D10",
        ["ux"] = "D10",
        ["ui"] = "D10",
        ["ux ui"] = "D10",
        ["brand"] = "D10",
        ["thiet ke do hoa"] = "D10",

        // D11 – Language & Global Market
        ["d11"] = "D11",
        ["language & global market"] = "D11",
        ["language and global market"] = "D11",
        ["language"] = "D11",
        ["global market"] = "D11",
        ["ngon ngu"] = "D11",
        ["tieng anh"] = "D11",
        ["tieng nhat"] = "D11",
        ["tieng trung"] = "D11",
        ["tieng han"] = "D11",

        // D12 – Marketing & Consumer Growth
        ["d12"] = "D12",
        ["marketing & consumer growth"] = "D12",
        ["marketing and consumer growth"] = "D12",
        ["marketing"] = "D12",
        ["consumer growth"] = "D12",
        ["thi truong"] = "D12",

        // D13 – Logistics & Supply Chain Tech
        ["d13"] = "D13",
        ["logistics & supply chain tech"] = "D13",
        ["logistics and supply chain tech"] = "D13",
        ["logistics"] = "D13",
        ["supply chain"] = "D13",
        ["scm"] = "D13",
        ["chuoi cung ung"] = "D13",

        // D14 – Law / LegalTech / Compliance
        ["d14"] = "D14",
        ["law / legaltech / compliance"] = "D14",
        ["law legaltech compliance"] = "D14",
        ["law"] = "D14",
        ["legaltech"] = "D14",
        ["compliance"] = "D14",
        ["luat"] = "D14",
        ["phap ly"] = "D14",

        // D15 – Finance / Fintech / Funding
        ["d15"] = "D15",
        ["finance / fintech / funding"] = "D15",
        ["finance fintech funding"] = "D15",
        ["finance"] = "D15",
        ["fintech"] = "D15",
        ["funding"] = "D15",
        ["tai chinh"] = "D15",

        // D16 – Human Development & Soft Skills
        ["d16"] = "D16",
        ["human development & soft skills"] = "D16",
        ["human development and soft skills"] = "D16",
        ["human development"] = "D16",
        ["soft skills"] = "D16",
        ["ky nang mem"] = "D16",

        // D17 – Culture & Vietnamese Identity
        ["d17"] = "D17",
        ["culture & vietnamese identity"] = "D17",
        ["culture and vietnamese identity"] = "D17",
        ["culture"] = "D17",
        ["vietnamese identity"] = "D17",
        ["ban sac viet"] = "D17",
        ["am nhac dan toc"] = "D17",
        ["van hoa"] = "D17",

        // D18 – Vovinam / Wellness / Discipline
        ["d18"] = "D18",
        ["vovinam / wellness / discipline"] = "D18",
        ["vovinam wellness discipline"] = "D18",
        ["vovinam"] = "D18",
        ["wellness"] = "D18",
        ["discipline"] = "D18",
        ["vo vovinam"] = "D18",
        ["the chat"] = "D18",
    };

    /// <summary>
    /// Đối chiếu danh sách tên domain trong file với danh mục hệ thống.
    /// Trả về danh sách CatalogItem tương ứng (không trùng lặp mã) và danh sách tên không ánh xạ được.
    /// </summary>
    public static (List<CatalogItem> Matched, List<string> Unmapped) ResolveDomains(
        IEnumerable<string>? rawNames,
        IReadOnlyList<CatalogItem> catalogDomains)
    {
        var matched = new List<CatalogItem>();
        var unmapped = new List<string>();
        if (rawNames == null) return (matched, unmapped);

        var byCode = catalogDomains.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in rawNames)
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            var code = MatchToCode(trimmed, catalogDomains);
            if (code != null)
            {
                if (seenCodes.Add(code))
                {
                    if (byCode.TryGetValue(code, out var item))
                    {
                        matched.Add(item);
                    }
                    else
                    {
                        // Fallback nếu catalog DB chưa có ID cụ thể: parse số từ Dxx (1..18)
                        var num = int.TryParse(code[1..], out var n) ? n : 0;
                        matched.Add(new CatalogItem(num, code, trimmed, true));
                    }
                }
            }
            else
            {
                unmapped.Add(trimmed);
            }
        }

        return (matched, unmapped);
    }

    /// <summary>Tìm mã D01–D18 từ một chuỗi thô bất kỳ.</summary>
    public static string? MatchToCode(string input, IReadOnlyList<CatalogItem>? catalogDomains = null)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var clean = input.Trim();

        // 1. Kiểm tra trực tiếp dạng D01 .. D18 hoặc D1 .. D9
        var codeMatch = CodeRegex.Match(clean);
        if (codeMatch.Success)
        {
            var num = int.Parse(codeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            return $"D{num:D2}";
        }

        var normalized = NormalizeKey(clean);

        // 2. Tra cứu trong AliasMap
        if (AliasMap.TryGetValue(normalized, out var mappedCode))
        {
            return mappedCode;
        }

        // 3. Tra cứu theo tên trong catalog
        if (catalogDomains != null)
        {
            foreach (var item in catalogDomains)
            {
                if (NormalizeKey(item.Name).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Code;
                }
            }
        }

        return null;
    }

    /// <summary>Bỏ dấu tiếng Việt, ký tự đặc biệt, chuyển thành chữ thường.</summary>
    public static string NormalizeKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var withoutDiacritics = sb.ToString().Normalize(NormalizationForm.FormC).Replace("đ", "d");
        // Đổi & thành 'and', xóa các ký tự / - _ , . ( ) [ ]
        var cleaned = withoutDiacritics
            .Replace("&", " and ")
            .Replace("/", " ")
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace("(", " ")
            .Replace(")", " ");

        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }
}

