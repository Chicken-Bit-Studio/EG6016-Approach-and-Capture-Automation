using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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
}
