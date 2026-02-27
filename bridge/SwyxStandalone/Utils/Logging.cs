namespace SwyxStandalone.Utils;

/// <summary>
/// Logging auf stderr — stdout ist NUR für JSON-RPC reserviert.
/// </summary>
public static class Logging
{
    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public static void Debug(string message) =>
        Console.Error.WriteLine($"[{Now()} DBG] {message}");

    public static void Info(string message) =>
        Console.Error.WriteLine($"[{Now()} INF] {message}");

    public static void Warn(string message) =>
        Console.Error.WriteLine($"[{Now()} WRN] {message}");

    public static void Error(string message) =>
        Console.Error.WriteLine($"[{Now()} ERR] {message}");
}
