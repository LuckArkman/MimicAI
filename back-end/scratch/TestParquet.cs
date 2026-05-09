using System;
using System.Linq;
using Parquet;

namespace TestScratch;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== INSPECTION OF PARQUETROWGROUPREADER ===");
        var methods = typeof(ParquetRowGroupReader).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var m in methods)
        {
            Console.WriteLine($"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
        }
    }
}
