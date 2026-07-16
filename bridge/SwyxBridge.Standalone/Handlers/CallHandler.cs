using System;
using System.Text.Json;
using SwyxBridge.Standalone.Com;
using SwyxBridge.Standalone.JsonRpc;
using SwyxBridge.Standalone.Utils;

namespace SwyxBridge.Standalone.Handlers
{
    /// <summary>
    /// Behandelt Anruf-bezogene JSON-RPC Methoden.
    /// </summary>
    public sealed class CallHandler
    {
        private readonly ComBridge _com;

        public CallHandler(ComBridge com) { _com = com; }

        public bool CanHandle(string method) => method switch
        {
            "dial" or "hangup" or "hold" or "getLines" or "getLineState" or "setNumberOfLines" => true,
            _ => false
        };

        public void Handle(JsonRpcRequest req)
        {
            try
            {
                object result = req.method switch
                {
                    "dial" => HandleDial(req.@params),
                    "hangup" => HandleHangup(req.@params),
                    "hold" => HandleHold(req.@params),
                    "getLines" => _com.GetAllLines(),
                    "getLineState" => _com.GetLineState(GetInt(req.@params, "lineId")),
                    "setNumberOfLines" => HandleSetLines(req.@params),
                    _ => throw new InvalidOperationException($"Unbekannte Methode: {req.method}")
                };

                if (req.id.HasValue)
                    JsonRpcEmitter.EmitResponse(req.id.Value, result ?? new { ok = true });
            }
            catch (Exception ex)
            {
                Logging.Error($"CallHandler: {req.method} fehlgeschlagen: {ex.Message}");
                if (req.id.HasValue)
                    JsonRpcEmitter.EmitError(req.id.Value, JsonRpcConstants.ComError, ex.Message);
            }
        }

        private object HandleDial(JsonElement? p)
        {
            var number = GetString(p, "number") ?? throw new ArgumentException("Parameter 'number' fehlt.");
            _com.Dial(number);
            return new { ok = true };
        }

        private object HandleHangup(JsonElement? p)
        {
            _com.Hangup();
            return new { ok = true };
        }

        private object HandleHold(JsonElement? p)
        {
            int lineId = GetInt(p, "lineId");
            _com.Hold(lineId);
            return new { ok = true };
        }

        private object HandleSetLines(JsonElement? p)
        {
            int count = GetInt(p, "count");
            _com.SetNumberOfLines(count);
            return new { ok = true };
        }

        private static string GetString(JsonElement? p, string key)
        {
            if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var v))
                return v.GetString();
            return null;
        }

        private static int GetInt(JsonElement? p, string key)
        {
            if (p?.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(key, out var v))
                return v.GetInt32();
            throw new ArgumentException($"Parameter '{key}' fehlt.");
        }
    }
}
