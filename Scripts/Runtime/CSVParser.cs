using UnityEngine;

public class CSVParser
{
    private static char[] lineSeparators = new char[] { '\n' };
    private static char[] columnSeparators = new char[] { ',' };

    public static string[,] GetCSVTable(string csvText)
    {
        // Get array of lines (get all rows)
        string[] lines = csvText.Split(lineSeparators, System.StringSplitOptions.RemoveEmptyEntries);

        // Find maximum number of columns
        int columns = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string[] row = lines[i].Split(columnSeparators);
            columns = Mathf.Max(columns, row.Length);
        }

        // Create 2D table
        string[,] table = new string[lines.Length, columns];
        for (int y = 0; y < lines.Length; y++)
        {
            string[] row = lines[y].Split(columnSeparators);
            for (int x = 0; x < row.Length; x++)
            {
                table[y, x] = row[x];
            }
        }

        return table;
    }
}
