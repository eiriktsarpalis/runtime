using System;
using System.Text.Json;

class Program
{
    static void Main()
    {
        // Case 1: "10.0" -> Parsed as Big (force big path?)
        // If "10.0" fits in decimal, it goes to small path.
        // small path: decimal.Truncate(10.0m) == 10.0m -> True.
        
        // Need to force Big path.
        // Use a number that doesn't fit in decimal but is an integer?
        // Or disable fast path? I can't easily disable fast path from public API.
        // But "1e30" fits in double but not decimal (decimal max ~7.9e28).
        
        string largeInt = "1e30"; 
        if (JsonNumber.TryParse(largeInt, out JsonNumber jn))
        {
            Console.WriteLine($"Parsed {largeInt}. IsInteger: {jn.IsInteger}");
        }
        else
        {
            Console.WriteLine($"Failed to parse {largeInt}");
        }

        // Case 2: "10.0" via TryParseBig (internal).
        // I can't call internal. 
        // But I can try a number that forces big path but ends in .0
        // "1" followed by 29 zeros, then ".0"
        string hugeWithPoint = "100000000000000000000000000000.0";
        if (JsonNumber.TryParse(hugeWithPoint, out JsonNumber jn2))
        {
             Console.WriteLine($"Parsed {hugeWithPoint}. IsInteger: {jn2.IsInteger}");
        }
    }
}
