using System.Text.Json;
using SwyxStandalone.Com;
using SwyxStandalone.JsonRpc;
using SwyxStandalone.Utils;

namespace SwyxStandalone.Handlers;

public sealed class ChatHandler
{
    private readonly StandaloneConnector _connector;
    private uint _chatReaderId;

    public ChatHandler(StandaloneConnector connector)
    {
        _connector = connector;
        _chatReaderId = 0;
    }

    public bool CanHandle(string method)
    {
        return method is "registerChatReader" or "sendChatMessage" or "readChatMessage" or "unregisterChatReader";
    }

    public void Handle(JsonRpcRequest request)
    {
        try
        {
            object? result = request.Method switch
            {
                "registerChatReader" => HandleRegisterChatReader(request.Params),
                "sendChatMessage" => HandleSendChatMessage(request.Params),
                "readChatMessage" => HandleReadChatMessage(request.Params),
                "unregisterChatReader" => HandleUnregisterChatReader(request.Params),
                _ => throw new InvalidOperationException($"Unknown method: {request.Method}")
            };

            if (request.Id.HasValue)
                JsonRpcEmitter.EmitResponse(request.Id.Value, result ?? new { ok = true });
        }
        catch (Exception ex)
        {
            if (request.Id.HasValue)
                JsonRpcEmitter.EmitError(request.Id.Value, -32603, ex.Message);
        }
    }

    private object HandleRegisterChatReader(JsonElement? param)
    {
        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            _chatReaderId = clmgr.DispRegisterChatMessageReader();
            Logging.Info($"ChatHandler: Chat Reader registriert mit ID={_chatReaderId}");
            return new { ok = true, readerId = _chatReaderId };
        }
        catch (Exception ex)
        {
            Logging.Error($"ChatHandler: RegisterChatReader failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleSendChatMessage(JsonElement? param)
    {
        if (param == null) return new { ok = false, error = "Invalid params" };

        var elem = param.Value;
        var text = elem.TryGetProperty("text", out var t) ? t.GetString() : null;
        var peerName = elem.TryGetProperty("peerName", out var p) ? p.GetString() : null;
        var peerIP = elem.TryGetProperty("peerIP", out var ip) ? ip.GetString() : null;
        var messageId = elem.TryGetProperty("messageId", out var m) ? m.GetInt32() : 0;

        if (string.IsNullOrEmpty(text))
        {
            return new { ok = false, error = "Nachrichtentext erforderlich" };
        }

        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            clmgr.DispSendChatMessage(messageId, text, peerName ?? "", peerIP ?? "");
            Logging.Info($"ChatHandler: Nachricht gesendet an {peerName}");
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Error($"ChatHandler: SendChatMessage failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleReadChatMessage(JsonElement? param)
    {
        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            var messages = clmgr.DispReadChatMessage(_chatReaderId);
            return new { ok = true, messages };
        }
        catch (Exception ex)
        {
            Logging.Error($"ChatHandler: ReadChatMessage failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }

    private object HandleUnregisterChatReader(JsonElement? param)
    {
        var clmgr = _connector.GetCom();
        if (clmgr == null)
        {
            return new { ok = false, error = "COM nicht verbunden" };
        }

        try
        {
            if (_chatReaderId > 0)
            {
                clmgr.DispUnRegisterChatMessageReader(_chatReaderId);
                Logging.Info($"ChatHandler: Chat Reader {_chatReaderId} deregistriert");
                _chatReaderId = 0;
            }
            return new { ok = true };
        }
        catch (Exception ex)
        {
            Logging.Error($"ChatHandler: UnregisterChatReader failed: {ex.Message}");
            return new { ok = false, error = ex.Message };
        }
    }
}