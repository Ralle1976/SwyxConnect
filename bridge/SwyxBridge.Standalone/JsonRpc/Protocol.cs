using System.Text.Json;

namespace SwyxBridge.Standalone.JsonRpc
{
    // JSON-RPC 2.0 message types (deserialize-tolerant — extra fields ignored).

    public class JsonRpcRequest
    {
        public string jsonrpc { get; set; }
        public int? id { get; set; }
        public string method { get; set; }
        public JsonElement? @params { get; set; }
    }

    public class JsonRpcResponse
    {
        public string jsonrpc { get; set; } = "2.0";
        public int? id { get; set; }
        public object result { get; set; }
        public JsonRpcError error { get; set; }
    }

    public class JsonRpcError
    {
        public int code { get; set; }
        public string message { get; set; }
    }

    public class JsonRpcEvent
    {
        public string jsonrpc { get; set; } = "2.0";
        public string method { get; set; }
        public object @params { get; set; }
    }

    public static class JsonRpcConstants
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;
        public const int ComError = -32000;
        public const int NotConnected = -32001;
    }
}
