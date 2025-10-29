using System;
using System.Diagnostics;
using System.Text;

public static class StaticUtilities
{
    public static string FormatStopwatchDuration(Stopwatch stopwatch)
    {
        // Don't attempt to format a null stopwatch, that would be silly
        if (stopwatch == null)
        {
            return "(no stopwatch)";
        }

        TimeSpan t = stopwatch.Elapsed;
        StringBuilder sb = new StringBuilder(); // On review, I have decided that I don't like StringBuilder.

        if (t.Hours > 0)
            sb.Append($"{t.Hours}h ");
        if (t.Minutes > 0)
            sb.Append($"{t.Minutes}m ");
        if (t.Seconds > 0)
            sb.Append($"{t.Seconds}s ");
        if (t.Milliseconds > 0 || sb.Length == 0) // Always show ms if nothing else
            sb.Append($"{t.Milliseconds}ms");

        // Trim trailing space if any and return
        return sb.ToString().TrimEnd();
    }
}
