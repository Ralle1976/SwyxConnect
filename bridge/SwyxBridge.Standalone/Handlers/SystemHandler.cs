using System;
using System.Text.Json;
using SwyxBridge.Standalone.Com;
using SwyxBridge.Standalone.JsonRpc;
using SwyxBridge.Standalone.Utils;

namespace SwyxBridge.Standalone.Handlers
{
    /// <summary>
    /// System/Session-Handler: getStatus, getSystemInfo, setNumberOfLines.
    /// Login selbst passiert in Program.cs via WcfLoginService — nicht via JSON-RPC.
    /// </summary>
    public sealed class SystemHandler
    {
        private readonly WcfLoginService _login;
        private readonly ComBridge _com;

        public SystemHandler(WcfLoginService login, ComBridge com)
        {
            _login = login;
            _com = com;
        }

        public bool CanHandle(string method) => method switch
        {
            "getStatus" or "getSystemInfo" => true,
            _ => false
        };

        public void Handle(JsonRpcRequest req)
        {
            try
            {
                object result = req.method switch
                {
                    "getStatus" => GetStatus(),
                    "getSystemInfo" => GetSystemInfo(),
                    _ => throw new InvalidOperationException($"Unbekannte Methode: {req.method}")
                };

                if (req.id.HasValue)
                    JsonRpcEmitter.EmitResponse(req.id.Value, result);
            }
            catch (Exception ex)
            {
                Logging.Error($"SystemHandler: {req.method} fehlgeschlagen: {ex.Message}");
                if (req.id.HasValue)
                    JsonRpcEmitter.EmitError(req.id.Value, JsonRpcConstants.InternalError, ex.Message);
            }
        }

        private object GetStatus()
        {
            return new
            {
                isAuthenticated = _login.IsLoggedIn,
                username = _login.UserName,
                userId = _login.UserId,
                server = _com.GetCurrentServer(),
                comConnected = _com.IsConnected
            };
        }

        private object GetSystemInfo()
        {
            return new
            {
                isLoggedIn = _login.IsLoggedIn,
                isCtiMaster = false, // wir sind nie CTI-Master (wie SwyxIt!)
                numberOfLines = _com.GetLineCount(),
                selectedLine = _com.GetSelectedLineNumber(),
                server = _com.GetCurrentServer(),
                user = _com.GetCurrentUser(),
                version = "1.7.0-standalone"
            };
        }
    }
}
