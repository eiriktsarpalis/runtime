using System;

class Test {
    static void Main() {
        decimal val = 100000000000000000000m; // 10^20
        Console.WriteLine("decimal value: {0}", val);
        
        try {
            UInt128 result1 = (UInt128)(ulong)val;
            Console.WriteLine("Cast through ulong: {0}", result1);
        } catch (Exception ex) {
            Console.WriteLine("Error: {0}", ex.Message);
        }
        
        try {
            UInt128 result2 = (UInt128)val;
            Console.WriteLine("Direct cast: {0}", result2);
        } catch (Exception ex) {
            Console.WriteLine("Error: {0}", ex.Message);
        }
    }
}
