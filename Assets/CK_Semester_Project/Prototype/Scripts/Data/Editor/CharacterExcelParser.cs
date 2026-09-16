using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CK.SemesterProject.Data.Editor
{
    /// <summary>Character_DT의 영문 헤더/타입/데이터 행을 읽습니다. Excel 실행이나 외부 DLL이 필요 없습니다.</summary>
    public static class CharacterExcelParser
    {
        private static readonly XNamespace SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly string[] Headers =
        {
            "character_id", "character_name", "character_name_e", "character_hp", "character_speed",
            "base_movement_speed", "running_movement_speed", "max_overheat_energy",
            "base_critical_chance", "character_critical_damage", "character_evasion_rate", "base_memory"
        };

        public static CharacterData[] Parse(string path)
        {
            // Excel이 파일을 열어 둔 상태에서도 마지막으로 저장된 데이터를 읽습니다.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Parse(stream);
        }

        public static CharacterData[] Parse(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
            var strings = new List<string>();
            if (archive.GetEntry("xl/sharedStrings.xml") != null)
            {
                strings.AddRange(ReadXml(archive, "xl/sharedStrings.xml").Descendants(SheetNs + "si")
                    .Select(item => string.Concat(item.Descendants(SheetNs + "t").Select(text => text.Value))));
            }

            XDocument workbook = ReadXml(archive, "xl/workbook.xml");
            XDocument relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
            CharacterData[] result = null;
            foreach (XElement sheet in workbook.Descendants(SheetNs + "sheet"))
            {
                string relationId = (string)sheet.Attribute(RelationNs + "id");
                XElement relation = relationships.Root.Elements()
                    .SingleOrDefault(item => (string)item.Attribute("Id") == relationId);
                if (relation == null || (string)relation.Attribute("TargetMode") == "External")
                    throw new InvalidDataException("워크시트 연결 정보를 읽을 수 없습니다.");

                string target = (string)relation.Attribute("Target");
                string entryPath = new Uri(new Uri("http://xlsx/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                XElement[] rows = ReadXml(archive, entryPath).Descendants(SheetNs + "row").ToArray();
                for (int index = 0; index < rows.Length; index++)
                {
                    Dictionary<string, string> cells = ReadCells(rows[index], strings);
                    if (!cells.Values.Contains("character_id") || !cells.Values.Contains("character_name")) continue;
                    if (result != null) throw new InvalidDataException("캐릭터 데이터 시트가 둘 이상입니다.");
                    string context = (string)sheet.Attribute("name");
                    var columns = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var cell in cells)
                    {
                        if (!Headers.Contains(cell.Value)) continue;
                        if (columns.ContainsKey(cell.Value)) throw Error(context, rows[index], cell.Key, "중복 헤더입니다.");
                        columns.Add(cell.Value, cell.Key);
                    }

                    foreach (string header in Headers)
                        if (!columns.ContainsKey(header)) throw Error(context, rows[index], header, "필수 컬럼이 없습니다.");
                    if (index + 1 >= rows.Length) throw new InvalidDataException(context + ": 타입 행이 없습니다.");
                    ValidateTypes(ReadCells(rows[index + 1], strings), columns, context, rows[index + 1]);
                    result = ReadCharacters(rows.Skip(index + 2), strings, columns, context);
                    break;
                }
            }

            return result ?? throw new InvalidDataException("캐릭터 영문 헤더 행을 찾을 수 없습니다.");
        }

        private static CharacterData[] ReadCharacters(IEnumerable<XElement> rows, List<string> strings,
            Dictionary<string, string> columns, string sheet)
        {
            var characters = new List<CharacterData>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement row in rows)
            {
                Dictionary<string, string> cells = ReadCells(row, strings);
                if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;
                string Text(string header)
                {
                    if (!cells.TryGetValue(columns[header], out string value) || string.IsNullOrWhiteSpace(value))
                        throw Error(sheet, row, header, "필수 값이 비어 있습니다.");
                    return value;
                }

                uint Unsigned(string header)
                {
                    string value = Text(header);
                    if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)
                        || number < 0 || number > uint.MaxValue || decimal.Truncate(number) != number)
                        throw Error(sheet, row, header, "0~4294967295 범위의 정수가 필요합니다: " + value);
                    return (uint)number;
                }

                float Number(string header)
                {
                    string value = Text(header);
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                        || float.IsNaN(number) || float.IsInfinity(number) || number < 0)
                        throw Error(sheet, row, header, "유한한 0 이상의 숫자가 필요합니다: " + value);
                    return number;
                }

                string id = Text("character_id");
                if (!ids.Add(id)) throw Error(sheet, row, "character_id", "중복 ID: " + id);
                characters.Add(new CharacterData(id, Text("character_name"), Text("character_name_e"),
                    Unsigned("character_hp"), Unsigned("character_speed"), Unsigned("base_movement_speed"),
                    Unsigned("running_movement_speed"), Unsigned("max_overheat_energy"),
                    Number("base_critical_chance"), Number("character_critical_damage"),
                    Number("character_evasion_rate"), Unsigned("base_memory")));
            }

            if (characters.Count == 0) throw new InvalidDataException(sheet + ": 캐릭터 데이터가 없습니다.");
            return characters.ToArray();
        }

        private static void ValidateTypes(Dictionary<string, string> cells, Dictionary<string, string> columns,
            string sheet, XElement row)
        {
            for (int i = 0; i < Headers.Length; i++)
            {
                string expected = i < 3 ? "string" : i >= 8 && i <= 10 ? "float" : "unsigned int";
                cells.TryGetValue(columns[Headers[i]], out string actual);
                // 현재 기획 시트에 있는 fioat 오타도 float로 해석합니다.
                if (actual == "fioat") actual = "float";
                if (actual != expected) throw Error(sheet, row, Headers[i], "타입은 " + expected + "여야 합니다.");
            }
        }

        private static Dictionary<string, string> ReadCells(XElement row, List<string> strings)
        {
            var cells = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XElement cell in row.Elements(SheetNs + "c"))
            {
                string address = (string)cell.Attribute("r");
                if (string.IsNullOrEmpty(address)) throw new InvalidDataException("셀 주소가 없습니다.");
                string column = new string(address.TakeWhile(char.IsLetter).ToArray());
                string type = (string)cell.Attribute("t");
                string value = (string)cell.Element(SheetNs + "v") ?? string.Empty;
                if (type == "e") throw new InvalidDataException(address + ": Excel 오류 값: " + value);
                if (cell.Element(SheetNs + "f") != null && string.IsNullOrEmpty(value))
                    throw new InvalidDataException(address + ": 수식 계산 결과가 없습니다. Excel에서 계산 후 저장하세요.");
                if (type == "s")
                {
                    if (!int.TryParse(value, out int index) || index < 0 || index >= strings.Count)
                        throw new InvalidDataException(address + ": 문자열 참조가 잘못되었습니다.");
                    value = strings[index];
                }
                else if (type == "inlineStr")
                {
                    value = string.Concat(cell.Descendants(SheetNs + "t").Select(text => text.Value));
                }
                cells.Add(column, value.Trim());
            }
            return cells;
        }

        private static XDocument ReadXml(ZipArchive archive, string path)
        {
            ZipArchiveEntry entry = archive.GetEntry(path)
                ?? throw new InvalidDataException("xlsx 내부 파일이 없습니다: " + path);
            using Stream stream = entry.Open();
            using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            return XDocument.Load(reader);
        }

        private static InvalidDataException Error(string sheet, XElement row, string column, string message)
            => new InvalidDataException($"{sheet}, {row.Attribute("r")?.Value}행, {column}: {message}");
    }
}
