using System.Text.RegularExpressions;

public sealed class NaturalStringComparer : IComparer<string>
{
    private static readonly Regex _splitRegex =
        new(@"\d+|\D+", RegexOptions.Compiled);

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        MatchCollection xParts = _splitRegex.Matches(x);
        MatchCollection yParts = _splitRegex.Matches(y);

        int count = Math.Min(xParts.Count, yParts.Count);

        for (int i = 0; i < count; i++)
        {
            string a = xParts[i].Value;
            string b = yParts[i].Value;

            bool aIsNum = char.IsDigit(a[0]);
            bool bIsNum = char.IsDigit(b[0]);

            if (aIsNum && bIsNum)
            {
                long na = long.Parse(a);
                long nb = long.Parse(b);

                int numCmp = na.CompareTo(nb);
                if (numCmp != 0)
                {
                    return numCmp;
                }
            }
            else
            {
                int strCmp = string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
                if (strCmp != 0)
                {
                    return strCmp;
                }
            }
        }

        return xParts.Count.CompareTo(yParts.Count);
    }
}