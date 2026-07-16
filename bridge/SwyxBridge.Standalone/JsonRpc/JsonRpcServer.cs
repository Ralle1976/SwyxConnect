using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using SwyxBridge.Standalone.Utils;

namespace SwyxBridge.Standalone.JsonRpc
{
    /// <summary>
    /// Liest stdin (line-delimited JSON-RPC) im Background-Thread,
    /// posted jeden Request auf den STA-Thread zur Ausführung.
    /// </summary>
    public sealed class JsonRpcServer
    {
        private readonly StaDispatcher _sta;
        private readonly Action<JsonRpcRequest> _dispatch;
        private readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        private volatile bool _stopping;

        public void Stop() { _stopping = true; }

        public JsonRpcServer(StaDispatcher sta, Action<JsonRpcRequest> dispatch)
        {
            _sta = sta;
            _dispatch = dispatch;
        }

        public void Run()
        {
            Logging.Info("JsonRpcServer: Lese von stdin...");
            string line;
            // Console.In.ReadLine ist blockierend — OK für Background-Thread.
            while ((line = Console.In.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                HandleLine(line);
            }
            Logging.Info("JsonRpcServer: stdin geschlossen.");
        }

        private void HandleLine(string line)
        {
            JsonRpcRequest req;
            try
            {
                req = JsonSerializer.Deserialize<JsonRpcRequest>(line, _opts);
                if (req == null || string.IsNullOrEmpty(req.method))
                {
                    Logging.Warn($"JsonRpcServer: ungültige Nachricht: {line}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"JsonRpcServer: Parse-Fehler: {ex.Message}");
                return;
            }

            // Auf STA-Thread posten (COM-Calls müssen auf STA).
            _sta.Post(_ => SafeDispatch(req), null);
        }

        private void SafeDispatch(JsonRpcRequest req)
        {
            try
            {
                _dispatch(req);
            }
            catch (Exception ex)
            {
                Logging.Error($"Dispatch fehlgeschlagen für '{req.method}': {ex.Message}");
                if (req.id.HasValue)
                    JsonRpcEmitter.EmitError(req.id.Value, JsonRpcConstants.InternalError, ex.Message);
            }
        }
    }
}
