using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Team.Services.BaselineImport;

public static class BaselineImportFileParser
{
    private static readonly Regex StudentCodeRegex = new(@"^[A-Z]{2}[0-9]{6}$", RegexOptions.Compiled);

    public static List<ParsedBaselineRecord> Parse(Stream stream, string fileName, List<BaselineImportRowError> errors)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".xlsx")
        {
            return ParseXlsx(stream, errors);
        }
        if (ext is ".csv" or ".txt")
        {
            return ParseCsv(stream, errors);
        }

        errors.Add(new BaselineImportRowError(1, "Định dạng file không được hỗ trợ. Chỉ chấp nhận file .xlsx hoặc .csv."));
        return [];
    }

    private static List<ParsedBaselineRecord> ParseCsv(Stream stream, List<BaselineImportRowError> errors)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var rawRows = new List<(int RowNumber, List<string> Cells)>();
        var lineIndex = 0;
        while (reader.ReadLine() is { } line)
        {
            lineIndex++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var delimiter = line.Contains('\t') ? '\t' : (line.Contains(';') ? ';' : ',');
            var cells = line.Split(delimiter).Select(c => c.Trim().Trim('"').Trim()).ToList();
            if (cells.Any(c => !string.IsNullOrEmpty(c)))
            {
                rawRows.Add((lineIndex, cells));
            }
        }

        return ProcessRawGrid(rawRows, errors);
    }

    private static List<ParsedBaselineRecord> ParseXlsx(Stream stream, List<BaselineImportRowError> errors)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var sharedStrings = LoadSharedStrings(zip);
            var sheetEntry = zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase));
            if (sheetEntry is null)
            {
                errors.Add(new BaselineImportRowError(1, "Không tìm thấy sheet dữ liệu trong file Excel."));
                return [];
            }

            using var sheetStream = sheetEntry.Open();
            var doc = XDocument.Load(sheetStream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            var rows = doc.Descendants(ns + "row").ToList();
            if (rows.Count == 0)
            {
                rows = doc.Descendants("row").ToList();
            }

            var rawRows = new List<(int RowNumber, List<string> Cells)>();
            foreach (var rowEl in rows)
            {
                var rAttr = (string?)rowEl.Attribute("r");
                var rowNumber = int.TryParse(rAttr, out var rn) ? rn : rawRows.Count + 1;

                var cellEls = rowEl.Elements().Where(e => e.Name.LocalName == "c").ToList();
                var cellsByCol = new SortedDictionary<int, string>();

                foreach (var cEl in cellEls)
                {
                    var cellRef = (string?)cEl.Attribute("r") ?? "";
                    var colIndex = ColumnNameToIndex(GetColumnLetters(cellRef));

                    var cellType = (string?)cEl.Attribute("t");
                    var val = "";

                    if (cellType == "s")
                    {
                        var vEl = cEl.Elements().FirstOrDefault(e => e.Name.LocalName == "v");
                        if (vEl is not null && int.TryParse(vEl.Value, out var sIndex) && sIndex < sharedStrings.Count)
                        {
                            val = sharedStrings[sIndex];
                        }
                    }
                    else if (cellType == "inlineStr")
                    {
                        var tEl = cEl.Descendants().FirstOrDefault(e => e.Name.LocalName == "t");
                        val = tEl?.Value ?? "";
                    }
                    else
                    {
                        var vEl = cEl.Elements().FirstOrDefault(e => e.Name.LocalName == "v");
                        val = vEl?.Value ?? "";
                    }

                    cellsByCol[colIndex] = val.Trim();
                }

                if (cellsByCol.Count == 0) continue;
                var maxCol = cellsByCol.Keys.Max();
                var cellList = new List<string>(maxCol + 1);
                for (var i = 0; i <= maxCol; i++)
                {
                    cellList.Add(cellsByCol.GetValueOrDefault(i, ""));
                }

                if (cellList.Any(c => !string.IsNullOrEmpty(c)))
                {
                    rawRows.Add((rowNumber, cellList));
                }
            }

            return ProcessRawGrid(rawRows, errors);
        }
        catch (Exception ex)
        {
            errors.Add(new BaselineImportRowError(1, "Không thể đọc file Excel: " + ex.Message));
            return [];
        }
    }

    private static List<string> LoadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];

        using var s = entry.Open();
        var doc = XDocument.Load(s);
        return doc.Descendants().Where(e => e.Name.LocalName == "si")
            .Select(si => string.Concat(si.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value)))
            .ToList();
    }

    private static string GetColumnLetters(string cellRef) =>
        new(cellRef.TakeWhile(char.IsLetter).ToArray());

    private static int ColumnNameToIndex(string colName)
    {
        if (string.IsNullOrEmpty(colName)) return 0;
        var index = 0;
        foreach (var ch in colName.ToUpperInvariant())
        {
            index = index * 26 + (ch - 'A' + 1);
        }
        return Math.Max(0, index - 1);
    }

    private static List<ParsedBaselineRecord> ProcessRawGrid(List<(int RowNumber, List<string> Cells)> rawRows, List<BaselineImportRowError> errors)
    {
        if (rawRows.Count == 0)
        {
            errors.Add(new BaselineImportRowError(1, "File trống."));
            return [];
        }

        var headerRow = rawRows[0];
        var headers = headerRow.Cells.Select(NormalizeHeader).ToList();

        var teamCol = FindColumnIndex(headers, ["nhom", "team", "group", "ma nhom", "ten nhom"]);
        var projectCol = FindColumnIndex(headers, ["du an", "project", "de tai", "ten du an", "ten de tai"]);
        var leaderCol = FindColumnIndex(headers, ["leader", "truong nhom", "mssv leader", "mssv truong nhom"]);
        var memberCol = FindColumnIndex(headers, ["mssv", "student", "thanh vien", "mssv thanh vien", "sinh vien"]);

        if (teamCol < 0) teamCol = 0;
        if (projectCol < 0 && headers.Count > 1) projectCol = 1;
        if (leaderCol < 0 && headers.Count > 2) leaderCol = 2;
        if (memberCol < 0 && headers.Count > 3) memberCol = 3;

        var results = new List<ParsedBaselineRecord>();

        for (var i = 1; i < rawRows.Count; i++)
        {
            var (rowNum, cells) = rawRows[i];
            var teamName = GetCell(cells, teamCol);
            var projectName = projectCol >= 0 ? GetCell(cells, projectCol) : null;
            var leaderVal = leaderCol >= 0 ? GetCell(cells, leaderCol) : "";
            var memberVal = memberCol >= 0 ? GetCell(cells, memberCol) : "";

            if (string.IsNullOrWhiteSpace(teamName) && string.IsNullOrWhiteSpace(leaderVal) && string.IsNullOrWhiteSpace(memberVal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(teamName))
            {
                errors.Add(new BaselineImportRowError(rowNum, "Thiếu tên nhóm."));
                continue;
            }

            var leaderIsFlag = leaderVal.Equals("LEADER", StringComparison.OrdinalIgnoreCase)
                               || leaderVal.Equals("TRUONG NHOM", StringComparison.OrdinalIgnoreCase)
                               || leaderVal.Equals("TRƯỞNG NHÓM", StringComparison.OrdinalIgnoreCase)
                               || leaderVal.Equals("X", StringComparison.OrdinalIgnoreCase)
                               || leaderVal.Equals("1")
                               || leaderVal.Equals("TRUE", StringComparison.OrdinalIgnoreCase);

            var membersInRow = ExtractStudentCodes(memberVal);

            // Also check extra columns for members (Format B: columns 4, 5, 6...)
            if (memberCol >= 0 && cells.Count > memberCol + 1)
            {
                for (var col = memberCol + 1; col < cells.Count; col++)
                {
                    var extraCell = cells[col].Trim().ToUpperInvariant();
                    if (!string.IsNullOrEmpty(extraCell))
                    {
                        membersInRow.AddRange(ExtractStudentCodes(extraCell));
                    }
                }
            }

            if (!leaderIsFlag && !string.IsNullOrWhiteSpace(leaderVal))
            {
                var leaderCode = leaderVal.ToUpperInvariant();
                results.Add(new ParsedBaselineRecord(rowNum, teamName, projectName, leaderCode, true));

                foreach (var mCode in membersInRow)
                {
                    if (!mCode.Equals(leaderCode, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new ParsedBaselineRecord(rowNum, teamName, projectName, mCode, false));
                    }
                }
            }
            else
            {
                // leaderVal is a flag or empty
                if (membersInRow.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(memberVal))
                    {
                        results.Add(new ParsedBaselineRecord(rowNum, teamName, projectName, memberVal.ToUpperInvariant(), leaderIsFlag));
                    }
                    else
                    {
                        errors.Add(new BaselineImportRowError(rowNum, "Dòng không có MSSV thành viên."));
                    }
                }
                else
                {
                    for (var mIdx = 0; mIdx < membersInRow.Count; mIdx++)
                    {
                        var isLeader = leaderIsFlag && mIdx == 0;
                        results.Add(new ParsedBaselineRecord(rowNum, teamName, projectName, membersInRow[mIdx], isLeader));
                    }
                }
            }
        }

        return results;
    }

    private static List<string> ExtractStudentCodes(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();
    }

    private static string GetCell(List<string> cells, int index) =>
        index >= 0 && index < cells.Count ? cells[index].Trim() : "";

    private static int FindColumnIndex(List<string> headers, string[] matchKeys)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var h = headers[i];
            if (matchKeys.Any(k => h.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }
        return -1;
    }

    private static string NormalizeHeader(string header)
    {
        var normalized = header.Trim().ToLowerInvariant();
        normalized = normalized.Replace("đ", "d");
        return normalized;
    }
}
