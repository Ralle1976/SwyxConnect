using System.Text.Json;

namespace SwyxBridge.JsonRpc;

/// <summary>
/// Schreibt JSON-RPC Nachrichten auf stdout.
/// Thread-safe durch Lock auf dem Writer.
/// WICHTIG: stdout = NUR JSON-RPC. Alles andere → stderr (Logging).
/// </summary>
public static class JsonRpcEmitter
{
    private static readonly object _lock = new();

    public static void EmitEvent(string method, object? @params = null)
    {
        var msg = new { jsonrpc = "2.0", method, @params };
        WriteLine(msg);
    }

    public static void EmitResponse(int id, object? result)
    {
        var msg = new { jsonrpc = "2.0", id, result };
        WriteLine(msg);
    }

    public static void EmitError(int id, int code, string message)
    {
        var msg = new { jsonrpc = "2.0", id, error = new { code, message } };
        WriteLine(msg);
    }

    private static void WriteLine(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonRpcConstants.SerializerOptions);
        lock (_lock)
        {
            Console.Out.WriteLine(json);
            Console.Out.Flush();
        }
    }
}
