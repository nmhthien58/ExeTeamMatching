using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Team.Services.BaselineImport;

public static class BaselineImportFileParser
{
    private static readonly Regex StudentCodeRegex = new(@"^[A-Z]{2,3}[0-9]{6}$", RegexOptions.Compiled);

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
            var cells = SplitCsvLine(line, delimiter);
            if (cells.Any(c => !string.IsNullOrEmpty(c)))
            {
                rawRows.Add((lineIndex, cells));
            }
        }

        return ProcessRawGrid(rawRows, errors);
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString().Trim().Trim('"').Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim().Trim('"').Trim());
        return result;
    }

    private static List<ParsedBaselineRecord> ParseXlsx(Stream stream, List<BaselineImportRowError> errors)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var sharedStrings = LoadSharedStrings(zip);
            var sheetEntries = zip.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sheetEntries.Count == 0)
            {
                errors.Add(new BaselineImportRowError(1, "Không tìm thấy sheet dữ liệu trong file Excel."));
                return [];
            }

            // Find best sheet: prefer sheet named "Group List", "Danh sách", or "EXE"
            ZipArchiveEntry? targetSheet = null;
            var wbEntry = zip.GetEntry("xl/workbook.xml");
            var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (wbEntry != null && relsEntry != null)
            {
                try
                {
                    using var wbStream = wbEntry.Open();
                    var wbDoc = XDocument.Load(wbStream);
                    using var relsStream = relsEntry.Open();
                    var relsDoc = XDocument.Load(relsStream);

                    var rIdToTarget = relsDoc.Descendants().Where(e => e.Name.LocalName == "Relationship")
                        .ToDictionary(
                            e => (string?)e.Attribute("Id") ?? "",
                            e => (string?)e.Attribute("Target") ?? "");

                    XNamespace rNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                    var sheets = wbDoc.Descendants().Where(e => e.Name.LocalName == "sheet")
                        .Select(e => new
                        {
                            Name = (string?)e.Attribute("name") ?? "",
                            RId = (string?)e.Attribute(rNs + "id") ?? (string?)e.Attribute("r:id") ?? ""
                        }).ToList();

                    var match = sheets.FirstOrDefault(s =>
                        s.Name.Contains("Group List", StringComparison.OrdinalIgnoreCase) ||
                        s.Name.Contains("Danh sách", StringComparison.OrdinalIgnoreCase))
                        ?? sheets.FirstOrDefault(s => s.Name.Contains("EXE", StringComparison.OrdinalIgnoreCase));

                    if (match != null && rIdToTarget.TryGetValue(match.RId, out var targetPath))
                    {
                        var cleanPath = "xl/" + targetPath.TrimStart('/');
                        targetSheet = zip.GetEntry(cleanPath) ?? zip.Entries.FirstOrDefault(e => e.FullName.Equals(cleanPath, StringComparison.OrdinalIgnoreCase));
                    }
                }
                catch
                {
                    // Fallback to default
                }
            }

            targetSheet ??= sheetEntries.FirstOrDefault();

            var rawRows = ReadSheetRows(targetSheet!, sharedStrings);

            // If the selected sheet didn't have any rows with MSSV, search other sheets
            if (!rawRows.Any(r => r.Cells.Any(c => NormalizeHeader(c).Contains("mssv"))))
            {
                foreach (var otherSheet in sheetEntries.Where(e => e != targetSheet))
                {
                    var otherRows = ReadSheetRows(otherSheet, sharedStrings);
                    if (otherRows.Any(r => r.Cells.Any(c => NormalizeHeader(c).Contains("mssv"))))
                    {
                        rawRows = otherRows;
                        break;
                    }
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

    private static List<(int RowNumber, List<string> Cells)> ReadSheetRows(ZipArchiveEntry sheetEntry, List<string> sharedStrings)
    {
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
        return rawRows;
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

        // Find header row: look for a row that has a cell containing "mssv" or "student code"
        var headerIndex = -1;
        for (var i = 0; i < rawRows.Count; i++)
        {
            var cells = rawRows[i].Cells;
            if (cells.Any(c =>
            {
                var norm = NormalizeHeader(c);
                return norm.Contains("mssv") || norm.Contains("student code") || norm.Contains("roll number");
            }))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0)
        {
            headerIndex = 0; // Fallback to first row
        }

        var headerRow = rawRows[headerIndex];
        var headers = headerRow.Cells.Select(NormalizeHeader).ToList();

        var sttCol = FindColumnIndex(headers, ["stt", "no", "so thu tu"]);
        var teamCol = FindColumnIndex(headers, ["ten nhom", "ten team", "ma nhom", "nhom", "team", "group"]);
        var projectCol = FindColumnIndex(headers, ["ten du an", "ten de tai", "du an", "de tai", "project"]);
        var descriptionCol = FindColumnIndex(headers, ["mo ta y tuong", "idea description", "mo ta", "description"]);
        var domainCol = FindColumnIndex(headers, ["domain nghien cuu", "linh vuc", "domain"]);
        var leaderCol = FindColumnIndex(headers, ["ho & ten leader", "ho ten leader", "leader", "truong nhom"]);
        var memberCol = FindColumnIndex(headers, ["mssv", "student", "thanh vien", "sinh vien"]);
        var memberNameCol = FindColumnIndex(headers, ["ho & ten", "ho ten", "full name"]);
        if (memberNameCol == leaderCol) memberNameCol = -1;

        if (teamCol < 0 && headers.Count > 0) teamCol = 0;
        if (projectCol < 0 && headers.Count > 1 && teamCol != 1 && descriptionCol != 1 && domainCol != 1) projectCol = 1;
        if (leaderCol < 0 && headers.Count > 2 && teamCol != 2 && projectCol != 2 && descriptionCol != 2 && domainCol != 2) leaderCol = 2;
        if (memberCol < 0 && headers.Count > 3) memberCol = 3;

        var leaderHeader = leaderCol >= 0 && leaderCol < headers.Count ? headers[leaderCol] : "";
        var leaderIsNameColumn = leaderHeader.Contains("ho") || leaderHeader.Contains("ten") || leaderHeader.Contains("name");

        var results = new List<ParsedBaselineRecord>();
        var currentTeamName = "";
        string? currentProjectName = null;
        string? currentDescription = null;
        List<string> currentDomainNames = [];
        var currentLeaderIdentifier = "";

        for (var i = headerIndex + 1; i < rawRows.Count; i++)
        {
            var (rowNum, cells) = rawRows[i];
            var sttVal = sttCol >= 0 ? GetCell(cells, sttCol) : "";
            var teamVal = teamCol >= 0 ? GetCell(cells, teamCol) : "";
            var projVal = projectCol >= 0 ? GetCell(cells, projectCol) : "";
            var descVal = descriptionCol >= 0 ? GetCell(cells, descriptionCol) : "";
            var domVal = domainCol >= 0 ? GetCell(cells, domainCol) : "";
            var leaderVal = leaderCol >= 0 ? GetCell(cells, leaderCol) : "";
            var memberVal = memberCol >= 0 ? GetCell(cells, memberCol) : "";
            var memberNameVal = memberNameCol >= 0 ? GetCell(cells, memberNameCol) : "";

            if (string.IsNullOrWhiteSpace(teamVal) && string.IsNullOrWhiteSpace(projVal) &&
                string.IsNullOrWhiteSpace(leaderVal) && string.IsNullOrWhiteSpace(memberVal))
            {
                continue;
            }

            // Determine if this row starts a new team
            var isNewTeam = false;
            if (sttCol >= 0 && !string.IsNullOrWhiteSpace(sttVal))
            {
                isNewTeam = true;
            }
            else if (!string.IsNullOrWhiteSpace(teamVal) && !teamVal.Equals(currentTeamName, StringComparison.OrdinalIgnoreCase))
            {
                isNewTeam = true;
            }
            else if (string.IsNullOrWhiteSpace(currentTeamName) && !string.IsNullOrWhiteSpace(teamVal))
            {
                isNewTeam = true;
            }

            if (isNewTeam)
            {
                currentTeamName = !string.IsNullOrWhiteSpace(teamVal)
                    ? teamVal
                    : (!string.IsNullOrWhiteSpace(projVal) ? projVal : $"Nhóm {sttVal}");
                currentProjectName = !string.IsNullOrWhiteSpace(projVal) ? projVal : null;
                currentDescription = !string.IsNullOrWhiteSpace(descVal) ? descVal : null;
                currentDomainNames = SplitDomainNames(domVal);
                currentLeaderIdentifier = leaderVal;
            }
            else if (!string.IsNullOrWhiteSpace(leaderVal) && string.IsNullOrWhiteSpace(currentLeaderIdentifier))
            {
                currentLeaderIdentifier = leaderVal;
            }

            if (string.IsNullOrWhiteSpace(currentTeamName))
            {
                errors.Add(new BaselineImportRowError(rowNum, "Dòng có sinh viên nhưng không thuộc nhóm nào."));
                continue;
            }

            var leaderIsFlag = leaderVal.Equals("LEADER", StringComparison.OrdinalIgnoreCase)
                               || leaderVal.Equals("TRUONG NHOM", StringComparison.OrdinalIgnoreCase)
                               || leaderVal.Equals("TRƯỞNG NHÓM", StringComparison.OrdinalIgnoreCase)
                               || leaderVal.Equals("X", StringComparison.OrdinalIgnoreCase)
                               || leaderVal.Equals("1")
                               || leaderVal.Equals("TRUE", StringComparison.OrdinalIgnoreCase);

            var membersInRow = ExtractStudentCodes(memberVal);

            // Format B extra member columns (only when there is no STT column, i.e. single-row-per-team layout)
            if (sttCol < 0 && memberCol >= 0 && cells.Count > memberCol + 1)
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

            // Layout 1: Leader column contains a student code (e.g. SE160001 or INVALID_CODE)
            if (!leaderIsNameColumn && !leaderIsFlag && !string.IsNullOrWhiteSpace(leaderVal))
            {
                var leaderCode = leaderVal.Trim().ToUpperInvariant();
                results.Add(new ParsedBaselineRecord(rowNum, currentTeamName, currentProjectName, currentDescription, currentDomainNames, leaderCode, true));

                foreach (var mCode in membersInRow)
                {
                    if (!mCode.Equals(leaderCode, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new ParsedBaselineRecord(rowNum, currentTeamName, currentProjectName, currentDescription, currentDomainNames, mCode, false));
                    }
                }
            }
            // Layout 2: Merged Group List or Flag-based Leader
            else
            {
                if (membersInRow.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(memberVal))
                    {
                        results.Add(new ParsedBaselineRecord(rowNum, currentTeamName, currentProjectName, currentDescription, currentDomainNames, memberVal.Trim().ToUpperInvariant(), leaderIsFlag));
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
                        var mCode = membersInRow[mIdx];
                        var isLeader = false;

                        if (leaderIsFlag)
                        {
                            isLeader = mIdx == 0;
                        }
                        else if (!string.IsNullOrWhiteSpace(currentLeaderIdentifier))
                        {
                            var cleanLeader = currentLeaderIdentifier.Trim();
                            if (cleanLeader.Equals(mCode, StringComparison.OrdinalIgnoreCase))
                            {
                                isLeader = true;
                            }
                            else if (!string.IsNullOrWhiteSpace(memberNameVal) &&
                                     RemoveDiacritics(cleanLeader).Equals(RemoveDiacritics(memberNameVal), StringComparison.OrdinalIgnoreCase))
                            {
                                isLeader = true;
                            }
                        }

                        results.Add(new ParsedBaselineRecord(rowNum, currentTeamName, currentProjectName, currentDescription, currentDomainNames, mCode, isLeader));
                    }
                }
            }
        }

        // For real Group List merged files (sttCol >= 0 and leaderIsNameColumn):
        // If a team ended up with no leader due to diacritics / empty leader name, default the first member to leader.
        if (sttCol >= 0 && leaderIsNameColumn)
        {
            var byTeam = results.GroupBy(r => r.TeamName, StringComparer.OrdinalIgnoreCase).ToList();
            var adjustedResults = new List<ParsedBaselineRecord>();
            foreach (var group in byTeam)
            {
                var list = group.ToList();
                if (!list.Any(r => r.IsLeader) && list.Count > 0)
                {
                    adjustedResults.Add(list[0] with { IsLeader = true });
                    adjustedResults.AddRange(list.Skip(1));
                }
                else
                {
                    adjustedResults.AddRange(list);
                }
            }
            return adjustedResults;
        }

        return results;
    }

    private static List<string> SplitDomainNames(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private static List<string> ExtractStudentCodes(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
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

    private static string NormalizeHeader(string header) =>
        RemoveDiacritics(header);

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace("đ", "d");
    }
}
