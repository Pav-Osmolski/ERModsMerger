using ERModsMerger.Core.Utility;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    internal static class CsvParamImporter
    {
        public static List<ParamRowToMerge> Parse(
            string csvPath,
            string paramKey,
            PARAM targetParam,
            Action<string, LOGTYPE>? log = null)
        {
            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length == 0)
                throw new InvalidDataException($"CSV is empty: {csvPath}");

            string headerLine = lines[0].TrimStart('\uFEFF').Trim();
            char separator = DetectSeparator(headerLine);
            string[] headers = headerLine.Split(separator);

            if (headers.Length == 0 ||
                !string.Equals(headers[0].Trim(), "ID", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"{Path.GetFileName(csvPath)} must start with an ID column.");
            }

            bool includesName = headers.Length > 1 &&
                string.Equals(headers[1].Trim(), "Name", StringComparison.OrdinalIgnoreCase);
            int fieldStart = includesName ? 2 : 1;

            PARAMDEF? def = targetParam.AppliedParamdef ?? targetParam.Rows.FirstOrDefault()?.Def;
            if (def == null)
                throw new InvalidDataException($"No ParamDef is available for {paramKey}.");

            PARAM.Row template = targetParam.Rows.FirstOrDefault() ?? new PARAM.Row(0, string.Empty, def);
            List<HeaderBinding> bindings = BuildBindings(headers, fieldStart, template, log);

            var changes = new List<ParamRowToMerge>();
            var seenIds = new HashSet<int>();

            for (int lineNumber = 2; lineNumber <= lines.Length; lineNumber++)
            {
                string rawLine = lines[lineNumber - 1];
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                string[] values = rawLine.TrimEnd('\r').Split(separator);
                if (values.Length == 0 || string.IsNullOrWhiteSpace(values[0]))
                    continue;

                if (!int.TryParse(values[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rowId))
                {
                    throw new InvalidDataException(
                        $"{Path.GetFileName(csvPath)} line {lineNumber}: invalid row ID '{values[0]}'.");
                }

                if (!seenIds.Add(rowId))
                {
                    throw new InvalidDataException(
                        $"{Path.GetFileName(csvPath)} line {lineNumber}: duplicate row ID {rowId}.");
                }

                List<PARAM.Row> matches = targetParam.Rows.Where(row => row.ID == rowId).ToList();
                if (matches.Count > 1)
                {
                    log?.Invoke(
                        $"{Path.GetFileName(csvPath)} line {lineNumber}: skipped duplicate target row ID {rowId} in {paramKey}",
                        LOGTYPE.WARNING);
                    continue;
                }

                bool isAdded = matches.Count == 0;
                string rowName = includesName && values.Length > 1
                    ? NormalizeName(values[1])
                    : matches.FirstOrDefault()?.Name ?? string.Empty;

                PARAM.Row conversionRow = isAdded
                    ? new PARAM.Row(rowId, rowName, def)
                    : matches[0];

                var change = new ParamRowToMerge(
                    paramKey,
                    rowId,
                    rowName,
                    isAdded ? RowChangeType.Added : RowChangeType.Modified);

                foreach (HeaderBinding binding in bindings)
                {
                    if (binding.CsvIndex >= values.Length)
                        continue;

                    string rawValue = values[binding.CsvIndex].Trim();
                    if (rawValue.Length == 0)
                        continue;

                    if (!TryGetCell(conversionRow, binding.Identity, out PARAM.Cell? cell) || cell == null)
                    {
                        log?.Invoke(
                            $"{Path.GetFileName(csvPath)}: skipped field {binding.Identity.FieldName} because it is not present in the current {paramKey} ParamDef",
                            LOGTYPE.WARNING);
                        continue;
                    }

                    object converted;
                    try
                    {
                        converted = ConvertValue(rawValue, cell.Value, cell.Def.ArrayLength);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException(
                            $"{Path.GetFileName(csvPath)} line {lineNumber}, field {binding.Identity.FieldName}: " +
                            $"could not parse '{rawValue}' as {cell.Value.GetType().Name}. {ex.Message}",
                            ex);
                    }

                    if (!isAdded && ValuesEqual(cell.Value, converted))
                        continue;

                    change.Cells.Add(new ParamCellChange(binding.Identity, converted));
                }

                if (isAdded || change.Cells.Count > 0)
                    changes.Add(change);
            }

            return changes;
        }

        private static List<HeaderBinding> BuildBindings(
            string[] headers,
            int fieldStart,
            PARAM.Row template,
            Action<string, LOGTYPE>? log)
        {
            var result = new List<HeaderBinding>();
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int index = fieldStart; index < headers.Length; index++)
            {
                string fieldName = headers[index].Trim();
                if (fieldName.Length == 0)
                    continue;

                int occurrence = occurrences.TryGetValue(fieldName, out int count) ? count : 0;
                occurrences[fieldName] = occurrence + 1;

                var identity = new CellIdentity(fieldName, occurrence, -1);
                if (!TryGetCell(template, identity, out _))
                {
                    log?.Invoke(
                        $"CSV column '{fieldName}' does not exist in the current ParamDef and will be ignored",
                        LOGTYPE.WARNING);
                    continue;
                }

                result.Add(new HeaderBinding(index, identity));
            }

            return result;
        }

        internal static char DetectSeparator(string header)
        {
            char[] candidates = [',', ';', '\t'];
            char best = candidates
                .OrderByDescending(candidate => header.Count(ch => ch == candidate))
                .First();

            if (!header.Contains(best))
                throw new InvalidDataException("Could not detect CSV separator. Supported separators: comma, semicolon, tab.");

            return best;
        }

        private static object ConvertValue(string raw, object currentValue, int arrayLength)
        {
            Type type = currentValue.GetType();

            if (type == typeof(byte[]))
                return ParseDummy8(raw, arrayLength);

            if (type == typeof(bool))
            {
                if (raw == "1")
                    return true;
                if (raw == "0")
                    return false;
                return bool.Parse(raw);
            }

            if (type == typeof(float))
                return float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (type == typeof(double))
                return double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);

            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture)
                ?? throw new InvalidCastException($"Could not convert '{raw}' to {type.Name}.");
        }

        private static byte[] ParseDummy8(string raw, int expectedLength)
        {
            if (!raw.StartsWith('[') || !raw.EndsWith(']'))
                throw new FormatException("dummy8 values must use Smithbox format [1|2|3].");

            string inner = raw[1..^1];
            string[] parts = inner.Length == 0 ? [] : inner.Split('|');
            if (parts.Length != expectedLength)
                throw new FormatException($"expected {expectedLength} dummy8 values, found {parts.Length}.");

            var bytes = new byte[expectedLength];
            for (int i = 0; i < parts.Length; i++)
                bytes[i] = byte.Parse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture);

            return bytes;
        }

        private static string NormalizeName(string value)
            => string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;

        private static bool TryGetCell(PARAM.Row row, CellIdentity identity, out PARAM.Cell? cell)
        {
            int occurrence = 0;
            foreach (PARAM.Cell candidate in row.Cells)
            {
                if (!string.Equals(candidate.Def.InternalName ?? string.Empty, identity.FieldName, StringComparison.Ordinal))
                    continue;

                if (occurrence == identity.Occurrence)
                {
                    cell = candidate;
                    return true;
                }

                occurrence++;
            }

            cell = null;
            return false;
        }

        private static bool ValuesEqual(object left, object right)
        {
            if (left is byte[] leftBytes && right is byte[] rightBytes)
                return leftBytes.SequenceEqual(rightBytes);

            return Equals(left, right);
        }

        private readonly record struct HeaderBinding(int CsvIndex, CellIdentity Identity);
    }
}
