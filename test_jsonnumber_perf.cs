using System;
using System.Diagnostics;
using System.Text.Json;

public class Program
{
    public static void Main()
    {
        // Create a large number: 1 followed by 50,000 zeros.
        // 50,000 zeros results in ~1562 uints.
        // DivRem over 1562 uints, done 50,000 times.
        // 1500 * 50000 = 75,000,000 operations. Should be fast but measurable.
        // Let's try 100,000 zeros.
        
        int zeroCount = 100000;
        string hugeNumber = "1" + new string('0', zeroCount);
        
        Stopwatch sw = Stopwatch.StartNew();
        JsonNumber n1 = JsonNumber.Parse(hugeNumber);
        sw.Stop();
        Console.WriteLine($"Parse time: {sw.ElapsedMilliseconds}ms");
        
        sw.Restart();
        // Normalize is called during Equals
        bool equals = n1.Equals(n1);
        sw.Stop();
        Console.WriteLine($"Equals(self) time: {sw.ElapsedMilliseconds}ms");
        
        // Also check creation via creating another one
        JsonNumber n2 = JsonNumber.Parse(hugeNumber);
        sw.Restart();
        equals = n1.Equals(n2);
        sw.Stop();
        Console.WriteLine($"Equals(other) time: {sw.ElapsedMilliseconds}ms");
    }
}
