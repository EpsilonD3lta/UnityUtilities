public class CSVParser
{
    private static char[] lineSeparators = new char[] { '\n' };
    private static char[] columnSeparators = new char[] { ',' };

    public static string[][] GetCSVTable(string csvText)
    {
        // Get array of lines (get all rows)
        string[] lines = csvText.Split(lineSeparators, System.StringSplitOptions.RemoveEmptyEntries);

        // Create 2D jagged table
        string[][] table = new string[lines.Length][];
        for (int y = 0; y < lines.Length; y++)
        {
            table[y] = lines[y].Split(columnSeparators);
        }

        return table;
    }
}
