using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SwyxBridge.Standalone.Utils;

namespace SwyxBridge.Standalone.Com
{
    /// <summary>
    /// Stellt sicher, dass CLMgr.exe läuft und der WCF-Login-Service auf 127.0.0.1:9094 bereit ist.
    ///
    /// CLMgr ist ein Out-of-Process-COM-Server. Wenn unsere Bridge die COM-ProgID aktiviert,
    /// startet der Windows SCM (Service Control Manager) CLMgr.exe automatisch.
    /// Danach hostet CLMgr intern einen WCF-Service auf 127.0.0.1:9094 — DORT läuft der Login.
    /// </summary>
    public static class CLMgrStartupService
    {
        private const string ProgId = "CLMgr.ClientLineMgr";
        private const string LoopbackHost = "127.0.0.1";
        private const int WcfPort = 9094;
        private const int MaxWaitSeconds = 60;

        /// <summary>
        /// Stellt CLMgr-Verfügbarkeit sicher und gibt das COM-Objekt zurück.
        /// 1. Falls CLMgr schon läuft → COM-Objekt direkt aktivieren
        /// 2. Sonst COM-Activation → SCM startet CLMgr
        /// 3. CLMgr-Initialisierung triggern (DispInit + LoginDeviceType=0)
        /// 4. Warten bis WCF-Port 9094 erreichbar ist
        ///
        /// WICHTIG: Kein SwyxIt! als Booster! 100% ohne Original.
        /// </summary>
        public static object EnsureClMgrAndCreateCom()
        {
            // 1. Status-Check
            bool clmgrWasRunning = Process.GetProcessesByName("CLMgr").Length > 0;
            Logging.Info($"CLMgrStartupService: CLMgr war {(clmgrWasRunning ? "bereits aktiv" : "NICHT aktiv — wird via COM-Activation gestartet")}.");

            // 2. COM-Objekt erstellen (startet CLMgr via SCM falls nicht vorhanden)
            Logging.Info("CLMgrStartupService: Aktiviere COM-ProgID 'CLMgr.ClientLineMgr'...");
            var comType = Type.GetTypeFromProgID(ProgId);
            if (comType == null)
            {
                throw new InvalidOperationException(
                    $"ProgID '{ProgId}' nicht gefunden. Ist das Swyx Client SDK installiert?");
            }

            object com;
            try
            {
                com = Activator.CreateInstance(comType);
                Logging.Info("CLMgrStartupService: COM-Objekt erstellt.");
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80070005))
            {
                throw new UnauthorizedAccessException(
                    "Zugriff verweigert (E_ACCESSDENIED). CLMgr läuft möglicherweise unter anderem Benutzer.", ex);
            }

            // 3. Falls CLMgr noch nicht eingeloggt → Initialisierung triggern
            if (!clmgrWasRunning)
            {
                // Credentials aus .env lesen (für RegisterUserEx)
                string cliUser = null, cliPassword = null, cliServer = null;
                ReadEnvCredentials(out cliServer, out cliUser, out cliPassword);
                if (string.IsNullOrEmpty(cliServer)) cliServer = "127.0.0.1";
                TriggerClMgrInitialization(com, cliServer, cliUser ?? "", cliPassword ?? "");
            }

            // 4. Warte bis WCF-Service auf 127.0.0.1:9094 bereit ist
            WaitForWcfPort();

            return com;
        }

        /// <summary>
        /// Triggert CLMgr-Initialisierung (VERSUCH). Bewiesen: OHNE SwyxIt! reicht das
        /// allein nicht aus, um Port 9094 zu öffnen — weitere RE nötig.
        ///
        /// Aus CLMgr.exe-Strings extrahiert: CLMgr hat EIGENEN Login-Code ("Create LibManager",
        /// "LoginWithCurrentWindowsAccount succeeded"). Dieser wird vermutlich über
        /// DispInit(server) + RegisterUserEx(server, ...) getriggert.
        /// </summary>
        private static void TriggerClMgrInitialization(object com, string server, string user, string password)
        {
            Logging.Info($"CLMgrStartupService: Triggere CLMgr-Initialisierung (server='{server}')...");

            // Step 1: DispInit(server) — mit echtem Servernamen, nicht leer
            try
            {
                com.GetType().InvokeMember("DispInit",
                    BindingFlags.InvokeMethod, null, com, new object[] { server });
                Logging.Info($"CLMgrStartupService: DispInit('{server}') aufgerufen.");
            }
            catch (Exception ex) { Logging.Warn($"CLMgrStartupService: DispInit fehlgeschlagen: {ex.Message}"); }

            // Step 2: LoginDeviceType = 0 setzen
            try
            {
                var cfg = com.GetType().InvokeMember("DispClientConfig",
                    BindingFlags.GetProperty, null, com, null);
                if (cfg != null)
                {
                    cfg.GetType().InvokeMember("LoginDeviceType",
                        BindingFlags.SetProperty, null, cfg, new object[] { 0 });
                    Logging.Info("CLMgrStartupService: LoginDeviceType = 0 gesetzt.");
                }
            }
            catch (Exception ex) { Logging.Warn($"CLMgrStartupService: LoginDeviceType-Set fehlgeschlagen: {ex.Message}"); }

            // Step 3: RegisterUserEx(server, backup="", user, pass, authMode=1, ctiMaster=0, out usernames)
            // ctiMaster=0 wie SwyxIt! (Registry RunAsCtiMaster=0)
            // Letzter Parameter ist out string — wir übergeben null und lesen args[6] danach.
            try
            {
                object[] args = new object[] { server, "", user, password, 1, 0, null };
                com.GetType().InvokeMember("RegisterUserEx",
                    BindingFlags.InvokeMethod, null, com, args);
                Logging.Info($"CLMgrStartupService: RegisterUserEx aufgerufen. Usernames='{args[6]}'.");
            }
            catch (Exception ex) { Logging.Warn($"CLMgrStartupService: RegisterUserEx fehlgeschlagen: {ex.Message}"); }

            // Step 4: Audio devices binden
            try
            {
                var cfg = com.GetType().InvokeMember("DispClientConfig",
                    BindingFlags.GetProperty, null, com, null);
                if (cfg != null)
                {
                    string hfPlayback = (string)(cfg.GetType().InvokeMember("HandsfreeDevice",
                        BindingFlags.GetProperty, null, cfg, null) ?? "");
                    if (!string.IsNullOrEmpty(hfPlayback))
                    {
                        com.GetType().InvokeMember("DispHandsfreeDevice",
                            BindingFlags.SetProperty, null, com, new object[] { hfPlayback });
                    }
                    Logging.Info($"CLMgrStartupService: Audio devices gebunden (HF='{hfPlayback}').");
                }
            }
            catch (Exception ex) { Logging.Warn($"CLMgrStartupService: Audio-Bind fehlgeschlagen: {ex.Message}"); }

            // Step 5: 2 Sekunden für async Plugin-Loading
            Logging.Info("CLMgrStartupService: Warte 2s auf Plugin-Loading...");
            Thread.Sleep(2000);

            Logging.Info("CLMgrStartupService: Initialisierung abgeschlossen.");
        }

        /// <summary>
        /// Wartet bis 127.0.0.1:9094 einen TCP-Verbindung akzeptiert.
        /// Gibt true zurück wenn bereit, false bei Timeout.
        /// </summary>
        public static bool WaitForWcfPort()
        {
            Logging.Info($"CLMgrStartupService: Warte auf WCF-Service {LoopbackHost}:{WcfPort}...");
            for (int i = 0; i < MaxWaitSeconds; i++)
            {
                if (IsPortOpen(LoopbackHost, WcfPort, timeoutMs: 500))
                {
                    Logging.Info($"CLMgrStartupService: WCF-Service bereit nach {i}s.");
                    return true;
                }
                Thread.Sleep(1000);
                if (i > 0 && i % 5 == 0)
                    Logging.Info($"CLMgrStartupService: ... noch nicht bereit ({i}s)");
            }
            Logging.Error($"CLMgrStartupService: WCF-Service nach {MaxWaitSeconds}s nicht bereit!");
            return false;
        }

        /// <summary>
        /// Schneller TCP-Port-Check.
        /// </summary>
        public static bool IsPortOpen(string host, int port, int timeoutMs = 1000)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var ar = client.BeginConnect(host, port, null, null);
                    bool success = ar.AsyncWaitHandle.WaitOne(timeoutMs);
                    if (success && client.Connected)
                    {
                        client.EndConnect(ar);
                        return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Liest Credentials aus .env (für RegisterUserEx-Aufruf).
        /// </summary>
        private static void ReadEnvCredentials(out string server, out string user, out string password)
        {
            server = null; user = null; password = null;
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
            env.TryGetValue("SWYX_SERVER", out server);
            env.TryGetValue("SWYX_USERNAME", out user);
            env.TryGetValue("SWYX_PASSWORD", out password);
        }
    }
}
