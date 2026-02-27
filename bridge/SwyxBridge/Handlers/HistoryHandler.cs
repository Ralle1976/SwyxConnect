using System.Text.Json;
using SwyxBridge.Com;
using SwyxBridge.JsonRpc;
using SwyxBridge.Utils;

namespace SwyxBridge.Handlers;

/// <summary>
/// Behandelt Anrufverlauf-bezogene JSON-RPC Methoden.
/// </summary>
public sealed class HistoryHandler
{
    private readonly SwyxConnector _connector;

    public HistoryHandler(SwyxConnector connector)
    {
        _connector = connector;
    }

    public bool CanHandle(string method) => method == "getCallHistory";

    public void Handle(JsonRpcRequest req)
    {
        try
        {
            // TODO: Echte Implementierung über ClCallHistCollection
            Logging.Info("HistoryHandler: getCallHistory (Stub)");

            var result = new { entries = Array.Empty<object>() };

            if (req.Id.HasValue)
                JsonRpcEmitter.EmitResponse(req.Id.Value, result);
        }
        catch (Exception ex)
        {
            Logging.Error($"HistoryHandler: fehlgeschlagen: {ex.Message}");
            if (req.Id.HasValue)
                JsonRpcEmitter.EmitError(req.Id.Value, JsonRpcConstants.ComError, ex.Message);
        }
    }
}
