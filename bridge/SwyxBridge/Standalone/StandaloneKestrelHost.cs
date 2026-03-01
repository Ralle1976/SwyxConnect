using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwyxBridge.Utils;

namespace SwyxBridge.Standalone;

/// <summary>
/// Eigener Kestrel-Host als Ersatz für ComSocketService.
/// Repliziert die 13 DI-Registrierungen des Originals,
/// aber ersetzt IClientLineManagerProvider durch SIP-basierten Provider.
/// </summary>
public sealed class StandaloneKestrelHost : IDisposable
{
    private IWebHost? _host;
    private readonly SipClientConfig _config;
    private readonly SipLineManagerProvider _lineManagerProvider;
    private readonly StubEventDistributor _eventDistributor;
    private int _actualPort;
    private bool _disposed;

    public int ActualPort => _actualPort;
    public bool IsRunning => _host != null;
    public SipLineManagerProvider LineManagerProvider => _lineManagerProvider;

    public StandaloneKestrelHost(SipClientConfig config)
    {
        _config = config;
        _lineManagerProvider = new SipLineManagerProvider(config);
        _eventDistributor = new StubEventDistributor();
    }

    public async Task<int> StartAsync()
    {
        if (_host != null) throw new InvalidOperationException("Bereits gestartet.");

        _actualPort = _config.KestrelPort > 0 ? _config.KestrelPort : GetRandomAvailablePort();
        Logging.Info($"StandaloneKestrelHost: Starte auf Port {_actualPort}...");

        var builder = new WebHostBuilder()
            .UseKestrel(options => options.Listen(IPAddress.Loopback, _actualPort))
            .ConfigureLogging(logging => { logging.ClearProviders(); logging.SetMinimumLevel(LogLevel.Warning); })
            .ConfigureServices(services =>
            {
                services.AddSingleton(this);
                services.AddSingleton(_config);
                services.AddSingleton<IClientConfig>(_config);
                services.AddSingleton<ILineManagerProvider>(_lineManagerProvider);
                services.AddSingleton<ICdsRestApi>(new StubCdsRestApi(_config));
                services.AddSingleton<IConnectionTokenStore>(new StubConnectionTokenStore());
                services.AddSingleton<ISwyxItHubBackend>(new StubSwyxItHubBackend());
                services.AddSingleton<IEventDistributor>(_eventDistributor);
                services.AddRouting();
                services.AddSignalR(o => { o.EnableDetailedErrors = true; o.KeepAliveInterval = TimeSpan.FromSeconds(15); });
                services.AddCors(o => o.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
            })
            .Configure(app =>
            {
                app.UseCors("AllowAll");
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapHub<SwyxConnectHub>("/hubs/swyxit");
                    endpoints.MapHub<ComSocketCompatHub>("/hubs/comsocket");
                    endpoints.MapGet("/api/health", async ctx =>
                    {
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            status = "ok", port = _actualPort,
                            hubPaths = new[] { "/hubs/swyxit", "/hubs/comsocket" }
                        }));
                    });
                    endpoints.MapGet("/api/status", async ctx =>
                    {
                        ctx.Response.ContentType = "application/json";
                        LineInfo[] lines = Array.Empty<LineInfo>();
                        _lineManagerProvider.DoWithLineManager(lm => lines = lm.GetAllLines());
                        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            server = _config.ServerAddress, user = _config.Username, lines
                        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                    });
                });
            })
            .SuppressStatusMessages(true);

        _host = builder.Build();
        _eventDistributor.SetServiceProvider(_host.Services);
        _lineManagerProvider.OnLineNotification += OnLineNotification;

        await _host.StartAsync();
        Logging.Info($"StandaloneKestrelHost: Läuft auf http://localhost:{_actualPort}");
        return _actualPort;
    }

    public async Task StopAsync()
    {
        if (_host == null) return;
        _lineManagerProvider.OnLineNotification -= OnLineNotification;
        try { await _host.StopAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) { Logging.Warn($"StandaloneKestrelHost Stop: {ex.Message}"); }
        finally { _host.Dispose(); _host = null; }
    }

    private void OnLineNotification(object? sender, LineNotificationEventArgs e)
    {
        if (e.Line == null) return;
        LineInfo[] allLines = Array.Empty<LineInfo>();
        _lineManagerProvider.DoWithLineManager(lm => allLines = lm.GetAllLines());
        JsonRpc.JsonRpcEmitter.EmitEvent("lineStateChanged", new { lines = allLines });
    }

    private static int GetRandomAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lineManagerProvider.OnLineNotification -= OnLineNotification;
        _host?.Dispose();
        _lineManagerProvider.Dispose();
    }
}
