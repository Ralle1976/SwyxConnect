using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SwyxBridge.Utils;

namespace SwyxBridge.Standalone;

/// <summary>
/// SIP User Agent Wrapper über SIPSorcery v10.0.3.
/// Kapselt SIP REGISTER, INVITE, BYE.
/// </summary>
public sealed class SipUserAgent : IDisposable
{
    private readonly SipClientConfig _config;
    private SIPTransport? _transport;
    private SIPRegistrationUserAgent? _regAgent;
    private bool _isRegistered;
    private bool _disposed;

    public event EventHandler<SipIncomingCallEventArgs>? OnIncomingCall;
    public event EventHandler<bool>? OnRegistrationChanged;
    public bool IsRegistered => _isRegistered;

    public SipUserAgent(SipClientConfig config) { _config = config; }

    public async Task<bool> StartAsync()
    {
        if (string.IsNullOrEmpty(_config.ServerAddress))
        {
            Logging.Warn("SipUserAgent: Kein Server konfiguriert.");
            return false;
        }
        try
        {
            _transport = new SIPTransport();
            _transport.AddSIPChannel(new SIPUDPChannel(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0)));
            _transport.SIPTransportRequestReceived += OnSipRequestReceived;
            Logging.Info($"SipUserAgent: Transport gestartet auf {_transport.GetSIPChannels()[0].ListeningEndPoint}");

            if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
            {
                _regAgent = new SIPRegistrationUserAgent(_transport, _config.Username, _config.Password, _config.SipDomain, 120);
                _regAgent.RegistrationSuccessful += (uri, resp) => { _isRegistered = true; OnRegistrationChanged?.Invoke(this, true); Logging.Info($"SipUserAgent: REGISTER OK ({uri})"); };
                _regAgent.RegistrationFailed += (uri, resp, msg) => { _isRegistered = false; OnRegistrationChanged?.Invoke(this, false); Logging.Warn($"SipUserAgent: REGISTER FAILED — {msg}"); };
                _regAgent.RegistrationRemoved += (uri, resp) => { _isRegistered = false; OnRegistrationChanged?.Invoke(this, false); };
                _regAgent.Start();
                Logging.Info($"SipUserAgent: REGISTER gestartet für {_config.Username}@{_config.SipDomain}");
                await Task.Delay(2000);
            }
            return true;
        }
        catch (Exception ex) { Logging.Error($"SipUserAgent: Start fehlgeschlagen — {ex.Message}"); return false; }
    }

    public async Task<SipCallResult> CallAsync(string number)
    {
        if (_transport == null) return new SipCallResult { Success = false, Reason = "Transport nicht gestartet" };
        try
        {
            var destUri = SIPURI.ParseSIPURI($"sip:{number}@{_config.SipDomain}:{_config.SipPort}");
            var ua = new SIPUserAgent(_transport, null);
            var callResult = await ua.Call(destUri.ToString(), null, null, null);
            if (callResult) { Logging.Info($"SipUserAgent: Call to {number} connected."); return new SipCallResult { Success = true, UserAgent = ua }; }
            else { Logging.Warn($"SipUserAgent: Call to {number} failed."); return new SipCallResult { Success = false, Reason = "Rejected" }; }
        }
        catch (Exception ex) { return new SipCallResult { Success = false, Reason = ex.Message }; }
    }

    public void Stop()
    {
        _regAgent?.Stop(); _regAgent = null;
        if (_transport != null) { _transport.SIPTransportRequestReceived -= OnSipRequestReceived; _transport.Shutdown(); _transport = null; }
        _isRegistered = false;
    }

    private Task OnSipRequestReceived(SIPEndPoint local, SIPEndPoint remote, SIPRequest req)
    {
        if (req.Method == SIPMethodsEnum.INVITE)
        {
            var from = req.Header.From;
            OnIncomingCall?.Invoke(this, new SipIncomingCallEventArgs { CallerName = from?.FromName ?? "", CallerNumber = from?.FromURI?.User ?? "", Request = req });
        }
        return Task.CompletedTask;
    }

    public void Dispose() { if (_disposed) return; _disposed = true; Stop(); }
}

public sealed class SipCallResult { public bool Success { get; init; } public string? Reason { get; init; } public SIPUserAgent? UserAgent { get; init; } }
public sealed class SipIncomingCallEventArgs : EventArgs { public string CallerName { get; init; } = ""; public string CallerNumber { get; init; } = ""; public SIPRequest? Request { get; init; } }
