using System.Reflection;
using NCalc;

Console.WriteLine("Expression assembly: " + typeof(Expression).Assembly.FullName);
foreach (var t in typeof(Expression).Assembly.GetTypes().OrderBy(t => t.FullName))
{
    if (t.FullName?.Contains("Function", StringComparison.OrdinalIgnoreCase) == true
        || t.FullName?.Contains("Handler", StringComparison.OrdinalIgnoreCase) == true
        || t.FullName?.Contains("Args", StringComparison.OrdinalIgnoreCase) == true
        || t.Name == "Expression")
        DumpType(t);
}

var syncAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "NCalc.Sync");
if (syncAsm != null)
{
    Console.WriteLine("Sync assembly types:");
    foreach (var t in syncAsm.GetTypes().OrderBy(t => t.FullName))
        if (t.Namespace?.StartsWith("NCalc") == true) DumpType(t);
}

static void DumpType(Type t)
{
    Console.WriteLine(t.FullName);
    foreach (var e in t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        Console.WriteLine($"  event {e.Name}: {e.EventHandlerType}");
    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        Console.WriteLine($"  prop {p.Name}: {p.PropertyType.Name}");
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        Console.WriteLine($"  method {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
}
