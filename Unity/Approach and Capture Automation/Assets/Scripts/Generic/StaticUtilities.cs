using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;

public static class StaticUtilities
{
    // Formats a duration given either a Stopwatch or a float number of seconds
    public static string FormatDuration(Stopwatch stopwatch)
    {
        // Don't attempt to format a null stopwatch, that would be silly
        if (stopwatch == null) { return "(no stopwatch)"; }
        // Use the helper class to format the duration
        return Helpers.FormatDuration_Internal(stopwatch.Elapsed);
    }
    public static string FormatDuration(float seconds)
    {
        // Use the helper class to format the duration
        return Helpers.FormatDuration_Internal(TimeSpan.FromSeconds(seconds));
    }

    // Dumps an array into a .csv file in the local machine's temporary directory
    public static void WriteFloatArrayToTempCsv(
        float[] values,
        int rowWidth,
        string fileName)
    {
        string tempPath = Path.GetTempPath();
        string fullPath = Path.Combine(tempPath, fileName + ".csv");

        using (var writer = new StreamWriter(fullPath))
        {
            for (int i = 0; i < values.Length; i++)
            {
                // Write value
                writer.Write(values[i].ToString(CultureInfo.InvariantCulture));

                bool endOfRow = (i + 1) % rowWidth == 0;
                bool lastValue = i == values.Length - 1;

                if (!lastValue)
                {
                    if (endOfRow)
                        writer.Write('\n');   // row break
                    else
                        writer.Write(',');    // same row
                }
            }
        }

        // Report completion
        UnityEngine.Debug.Log($"LiDAR hitDistances dump generated with {values.Length} elements at: {fullPath}");
    }
    // Returns an array containing the collective field data for all leaf classes in the given nested class structures.
    public static Type[] GetClassTreeLeafTypes(Type[] roots)
    {
        // Create a temporary list and start the recursion
        List<Type> leafFields = new();
        foreach (Type root in roots) { RecurseNestedStructure(root); }
        void RecurseNestedStructure(Type currentNode)
        {
            // Find all sub-types within the root type using reflection. Here, the '|' is a bitwise combination operator.
            foreach (Type nested in currentNode.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                // Check the nested type against our criteria:
                if (nested.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Length == 0 &&
                    !nested.IsAbstract &&
                    !nested.IsSealed)
                {
                    // The type 'nested' is a leaf node in its class structure - add it to the list.
                    leafFields.Add(nested);
                }
                else
                {
                    // The type 'nested' is NOT a leaf node. Recurse deeper.
                    RecurseNestedStructure(nested);
                }
            }
        }
        // Return the temporary list as an array
        return leafFields.ToArray();
    }

    // Converts an array to a string for debugging purposes
    public static string Arr_Str(Array arr)
    {
        // You're not a real programmer if you need to see this function commented
        string t = "\n";
        foreach (var i in arr)
        {
            t += i.ToString() + "\n";
        }
        return t;
    }

    // Helper class to encapsulate internal methods
    private class Helpers
    {
        public static string FormatDuration_Internal(TimeSpan t)
        {
            int days = t.Days;
            int hours = t.Hours;
            int minutes = t.Minutes;
            int secs = t.Seconds;
            int ms = t.Milliseconds;

            if (days > 0)
            {
                return $"{days}d {hours:D2}:{minutes:D2}:{secs:D2}.{ms:D3}";
            }
            else if (hours > 0)
            {
                return $"{hours:D2}:{minutes:D2}:{secs:D2}.{ms:D3}";
            }
            else
            {
                return $"{minutes:D2}:{secs:D2}.{ms:D3}";
            }
        }
    }
}
