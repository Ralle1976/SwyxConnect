using System.Text.Json;
using System.Threading;
using SwyxBridge.Standalone.Utils;

namespace SwyxBridge.Standalone.JsonRpc
{
    /// <summary>
    /// Thread-safe JSON-RPC output auf stdout. Zwischen Writes darf nichts anderes auf stdout.
    /// </summary>
    public static class JsonRpcEmitter
    {
        private static readonly object _lock = new object();
        private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public static void EmitResponse(int id, object result)
        {
            var msg = new JsonRpcResponse { id = id, result = result };
            Write(msg);
        }

        public static void EmitError(int id, int code, string message)
        {
            var msg = new JsonRpcResponse
            {
                id = id,
                error = new JsonRpcError { code = code, message = message }
            };
            Write(msg);
        }

        public static void EmitEvent(string method, object @params)
        {
            var msg = new JsonRpcEvent { method = method, @params = @params };
            Write(msg);
        }

        private static void Write(object msg)
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(msg, msg.GetType(), _opts);
                System.Console.Out.WriteLine(json);
                System.Console.Out.Flush();
            }
        }
    }
}
