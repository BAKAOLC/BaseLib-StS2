using System.Collections;
using System.Text;

namespace BaseLib.Extensions;

public static class IEnumerableExtensions
{
    public static string AsReadable<T>(this IEnumerable<T> enumerable, string separator = ",")
    {
        return string.Join(separator, enumerable);
    }
    public static string AsReadable(this IEnumerable enumerable, string separator = ",")
    {
        return string.Join(separator, enumerable);
    }
    public static string NumberedLines<T>(this IEnumerable<T> enumerable)
    {
        StringBuilder sb = new();
        int line = 0;
        foreach (var item in enumerable)
        {
            sb.Append(line).Append(": ").Append(item).AppendLine();
            ++line;
        }
        return sb.ToString();
    }
}
