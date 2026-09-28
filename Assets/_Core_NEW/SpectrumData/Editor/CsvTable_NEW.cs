using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Reads the small CSV tables in SpectrumData/Tables. EDITOR ONLY, PURE C#.
///
/// The format is deliberately plain so an astronomer can edit a table in a text editor
/// or a spreadsheet without touching code:
///
///   * lines starting with # are comments, blank lines are skipped
///   * the first remaining line is the header
///   * the LAST column may contain commas (it is always a free-text note or source), so
///     each row is split into exactly as many fields as the header has
///   * numbers use '.' as the decimal point regardless of the machine's locale
///
/// Any problem throws with the file name and line number, because a silently misread
/// table would bake a wrong universe and nothing downstream could tell.
/// </summary>
public sealed class CsvTable_NEW
{
    public readonly string Name;
    public readonly string[] Header;
    public readonly List<string[]> Rows = new List<string[]>();
    readonly List<int> _lineNumbers = new List<int>();

    public CsvTable_NEW(string name, string text)
    {
        Name = name;

        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            if (Header == null)
            {
                Header = Split(line, int.MaxValue);
                continue;
            }

            string[] fields = Split(line, Header.Length);
            if (fields.Length < Header.Length)
            {
                // A missing trailing note is fine; a missing value is not — caught at read time.
                Array.Resize(ref fields, Header.Length);
                for (int f = 0; f < fields.Length; f++) if (fields[f] == null) fields[f] = "";
            }

            Rows.Add(fields);
            _lineNumbers.Add(i + 1);
        }

        if (Header == null) throw new FormatException(name + ": no header row");
    }

    static string[] Split(string line, int count)
    {
        string[] parts = line.Split(new[] { ',' }, count);
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }

    /// <summary>Index of a header column, or throws.</summary>
    public int Column(string name)
    {
        for (int i = 0; i < Header.Length; i++)
            if (string.Equals(Header[i], name, StringComparison.OrdinalIgnoreCase)) return i;

        throw new FormatException(Name + ": no column '" + name + "' (have: " + string.Join(", ", Header) + ")");
    }

    /// <summary>Parse a number from a row, with the line number in the error.</summary>
    public double Number(int row, int column)
    {
        string s = Rows[row][column];
        double v;
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            throw new FormatException(Name + " line " + _lineNumbers[row] + ": '" + s + "' is not a number (column " + Header[column] + ")");

        return v;
    }

    public string Text(int row, int column)
    {
        return Rows[row][column];
    }

    /// <summary>For key,value tables: the value of a key as a number, or throws.</summary>
    public double Value(string key)
    {
        int k = Column("key");
        int v = Column("value");

        for (int r = 0; r < Rows.Count; r++)
            if (string.Equals(Rows[r][k], key, StringComparison.OrdinalIgnoreCase)) return Number(r, v);

        throw new FormatException(Name + ": missing key '" + key + "'");
    }
}
