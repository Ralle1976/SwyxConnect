using System.Text.Json;
using SwyxStandalone.Utils;

namespace SwyxStandalone.JsonRpc;

/// <summary>
/// Liest stdin zeilenweise auf einem Background-Thread.
/// Parst JSON-RPC Requests und dispatched sie via SynchronizationContext
/// auf den STA-Thread (wo COM lebt).
/// </summary>
public sealed class JsonRpcServer
{
    private readonly StaDispatcher _sta;
    private readonly Action<JsonRpcRequest> _handler;
    private volatile bool _running = true;

    public JsonRpcServer(StaDispatcher sta, Action<JsonRpcRequest> handler)
    {
        _sta = sta;
        _handler = handler;
    }

    /// <summary>
    /// Startet die Lese-Schleife. MUSS auf einem Background-Thread laufen!
    /// </summary>
    public void Run()
    {
        Logging.Info("JsonRpcServer: stdin-Reader gestartet.");

        try
        {
            using var reader = Console.In;
            while (_running)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    Logging.Info("JsonRpcServer: stdin geschlossen (EOF). Beende.");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                JsonRpcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<JsonRpcRequest>(line, JsonRpcConstants.SerializerOptions);
                }
                catch (JsonException ex)
                {
                    Logging.Warn($"JsonRpcServer: Ungültiges JSON: {ex.Message}");
                    continue;
                }

                if (request == null || string.IsNullOrEmpty(request.Method))
                {
                    Logging.Warn("JsonRpcServer: Leerer Request ignoriert.");
                    continue;
                }

                // Dispatch auf den STA-Thread via Post (asynchron, kein Deadlock-Risiko)
                var req = request;
                _sta.Post(() =>
                {
                    try
                    {
                        _handler(req);
                    }
                    catch (Exception ex)
                    {
                        Logging.Error($"Handler-Fehler für '{req.Method}': {ex.Message}");
                        if (req.Id.HasValue)
                        {
                            JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.InternalError, ex.Message);
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Logging.Error($"JsonRpcServer: Fataler Fehler: {ex.Message}");
        }
        finally
        {
            Logging.Info("JsonRpcServer: Beendet.");
        }
    }

    public void Stop()
    {
        _running = false;
    }
}
