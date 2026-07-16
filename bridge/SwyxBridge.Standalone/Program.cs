using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using SwyxBridge.Standalone.Com;
using SwyxBridge.Standalone.Handlers;
using SwyxBridge.Standalone.JsonRpc;
using SwyxBridge.Standalone.Utils;

namespace SwyxBridge.Standalone
{
    /// <summary>
    /// SwyxBridge Standalone v1.7.0 — 100% SwyxIt!-freie Telefonie.
    ///
    /// Architektur:
    ///   1. CLMgr.exe (via COM-Activation automatisch gestartet)
    ///   2. WCF-Login via LibManagerLogin.LoginWithCredentials auf 127.0.0.1:9094
    ///   3. COM-Calls für Telefonie (Dial, Lines, etc.)
    ///
    /// Bewiesen (2026-07-16): Funktioniert OHNE SwyxIt!.
    /// SwyxIt! ist NICHT mehr erforderlich.
    /// </summary>
    static class Program
    {
        private static WcfLoginService _login;
        private static ComBridge _com;
        private static CallHandler _callHandler;
        private static SystemHandler _systemHandler;
        private static JsonRpcServer _rpcServer;
        private static System.Timers.Timer _heartbeat;

        [STAThread]
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
            Logging.Info("SwyxBridge Standalone v1.7.0 startet...");

            // CLI-Argumente parsen (--user, --password)
            ParseArgs(args, out string cliUser, out string cliPassword);

            // .env lesen
            ReadEnv(out string envUser, out string envPassword);

            // Prefer CLI args, fallback to .env
            string user = !string.IsNullOrEmpty(cliUser) ? cliUser : envUser;
            string password = !string.IsNullOrEmpty(cliPassword) ? cliPassword : envPassword;

            // WinForms STA-SynchronizationContext installieren
            WindowsFormsSynchronizationContext.AutoInstall = true;
            var appCtx = new ApplicationContext();

            StaDispatcher sta = null;
            var initTimer = new System.Windows.Forms.Timer { Interval = 1 };
            initTimer.Tick += (s, e) =>
            {
                initTimer.Stop();
                initTimer.Dispose();
                try
                {
                    sta = new StaDispatcher();
                    InitializeBridge(sta, user, password);
                }
                catch (Exception ex)
                {
                    Logging.Error($"Bridge-Initialisierung fehlgeschlagen: {ex.Message}");
                    JsonRpcEmitter.EmitEvent("error", new { message = ex.Message });
                }
            };
            initTimer.Start();

            // Heartbeat (5s)
            int uptime = 0;
            _heartbeat = new System.Timers.Timer(5000);
            _heartbeat.Elapsed += (s, e) =>
            {
                uptime += 5;
                JsonRpcEmitter.EmitEvent("heartbeat", new { uptime = uptime });
            };
            _heartbeat.Start();

            Logging.Info("Message Pump startet...");
            Application.Run(appCtx);

            Logging.Info("SwyxBridge beendet.");
            _heartbeat?.Stop();
            _rpcServer?.Stop();
            _login?.Logout();
            _com?.Dispose();
        }

        private static void InitializeBridge(StaDispatcher sta, string user, string password)
        {
            Logging.Info("=== Bridge-Initialisierung ===");

            // Schritt 1: CLMgr starten / attachen + WCF-Port abwarten
            Logging.Info("Schritt 1: CLMgr-Startup...");
            object comObj;
            try
            {
                comObj = CLMgrStartupService.EnsureClMgrAndCreateCom();
            }
            catch (Exception ex)
            {
                Logging.Error($"CLMgr-Startup fehlgeschlagen: {ex.Message}");
                JsonRpcEmitter.EmitEvent("bridgeState", new { state = "disconnected", error = ex.Message });
                StartJsonRpcServer(sta); // trotzdem Server starten, damit Client Fehler sieht
                return;
            }
            _com = new ComBridge(comObj);
            Logging.Info($"Schritt 1: ✅ CLMgr verbunden (LoggedIn={_com.IsLoggedIn()}, User='{_com.GetCurrentUser()}')");

            // Schritt 2: Login via WCF (nur falls Credentials vorhanden)
            Logging.Info("Schritt 2: WCF-Login...");
            _login = new WcfLoginService();

            bool loginOk = false;
            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password))
            {
                // Versuche Login falls CLMgr noch nicht eingeloggt ist
                if (!_com.IsLoggedIn())
                {
                    loginOk = _login.Login(user, password);
                }
                else
                {
                    // CLMgr bereits eingeloggt (z.B. SwyxIt! war zuvor aktiv)
                    Logging.Info($"Schritt 2: CLMgr bereits eingeloggt als '{_com.GetCurrentUser()}' — Login übersprungen.");
                    loginOk = true;
                    _login.MarkAlreadyLoggedIn(_com.GetCurrentUser());
                }
            }
            else
            {
                Logging.Warn("Schritt 2: Keine Credentials in .env oder CLI — überspringe Login.");
            }

            // Schritt 3: Handler erstellen
            Logging.Info("Schritt 3: Handler erstellen...");
            _callHandler = new CallHandler(_com);
            _systemHandler = new SystemHandler(_login, _com);

            // Schritt 4: Status emit + Server starten
            if (loginOk)
            {
                Logging.Info($"✅ Bridge bereit (User={_com.GetCurrentUser()}, Server={_com.GetCurrentServer()}).");
                JsonRpcEmitter.EmitEvent("bridgeState", new
                {
                    state = "connected",
                    server = _com.GetCurrentServer(),
                    username = _com.GetCurrentUser(),
                    mode = "standalone-v1.7"
                });
            }
            else
            {
                Logging.Warn("⚠️ Login fehlgeschlagen oder übersprungen — Bridge bereit aber nicht eingeloggt.");
                JsonRpcEmitter.EmitEvent("bridgeState", new
                {
                    state = "ready",
                    info = "Login fehlgeschlagen. Bridge läuft, aber Telefonie ist nicht aktiv."
                });
            }

            StartJsonRpcServer(sta);
        }

        private static void StartJsonRpcServer(StaDispatcher sta)
        {
            _rpcServer = new JsonRpcServer(sta, DispatchRequest);
            var serverThread = new Thread(_rpcServer.Run)
            {
                IsBackground = true,
                Name = "JsonRpcServer"
            };
            serverThread.Start();
            Logging.Info("JsonRpcServer-Thread gestartet.");
        }

        private static void DispatchRequest(JsonRpcRequest req)
        {
            if (_systemHandler?.CanHandle(req.method) == true)
                _systemHandler.Handle(req);
            else if (_callHandler?.CanHandle(req.method) == true)
                _callHandler.Handle(req);
            else
            {
                Logging.Warn($"Unbekannte Methode: {req.method}");
                if (req.id.HasValue)
                    JsonRpcEmitter.EmitError(req.id.Value, JsonRpcConstants.MethodNotFound,
                        $"Methode '{req.method}' nicht gefunden.");
            }
        }

        private static void ParseArgs(string[] args, out string user, out string password)
        {
            user = null;
            password = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--user":
                    case "--username":
                        user = args[++i]; break;
                    case "--password":
                    case "--pass":
                        password = args[++i]; break;
                }
            }
        }

        private static void ReadEnv(out string user, out string password)
        {
            user = null;
            password = null;
            string[] candidates = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SwyxConnect", ".env"),
                ".env"
            };
            var envPath = Array.Find(candidates, File.Exists);
            if (envPath == null) return;

            var env = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(envPath))
            {
                var t = line.Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith("#")) continue;
                int eq = t.IndexOf('=');
                if (eq < 0) continue;
                env[t.Substring(0, eq).Trim()] = t.Substring(eq + 1).Trim();
            }
            env.TryGetValue("SWYX_USERNAME", out user);
            env.TryGetValue("SWYX_PASSWORD", out password);
        }
    }
}
