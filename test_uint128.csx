using System;

decimal val = 100000000000000000000m;
Console.WriteLine($"decimal value: {val}");

try {
    UInt128 result1 = (UInt128)(ulong)val;
    Console.WriteLine($"Cast through ulong: {result1}");
} catch (Exception ex) {
    Console.WriteLine($"Error casting through ulong: {ex.Message}");
}

try {
    UInt128 result2 = (UInt128)val;
    Console.WriteLine($"Direct cast: {result2}");
} catch (Exception ex) {
    Console.WriteLine($"Error direct cast: {ex.Message}");
}
