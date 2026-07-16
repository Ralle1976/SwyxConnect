using System;
using System.Reflection;
using SwyxBridge.Standalone.Utils;

namespace SwyxBridge.Standalone.Com
{
    /// <summary>
    /// Login-Service: Nutzt LibManagerLogin.LoginWithCredentials() aus IpPbxCDSClientLib.dll,
    /// um sich via WCF (net.tcp://127.0.0.1:9094) bei CLMgr einzuloggen.
    ///
    /// BEWIESEN (2026-07-16, LoginPoc): Login funktioniert OHNE SwyxIt!,
    /// solange CLMgr läuft und den WCF-Service hostet.
    ///
    /// Wir nutzen Reflection statt direkter Compile-Time-Bindung, weil die Swyx-DLLs
    /// .NET Framework 4.x sind und typisierte Bindung zu Runtime-Kompatibilitätsproblemen führen kann.
    /// </summary>
    public sealed class WcfLoginService
    {
        private const string LoopbackHost = "127.0.0.1";

        private Type _libManagerLoginType;
        private MethodInfo _loginMethod;
        private object _libManager; // gehalten für Refresh-Token-Logik

        public bool IsLoggedIn { get; private set; }
        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public int UserId { get; private set; }
        public string UserName { get; private set; }

        public WcfLoginService()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Swyx DLLs explizit laden (Private=false im csproj, sonst fehlen sie)
            string swyxDir = @"C:\Program Files (x86)\Swyx\SwyxIt!";
            string[] requiredDlls = {
                "IpPbxTracing.dll",
                "IpPbxWin32.dll",
                "IpPbxCDSSharedLib.dll",
                "IpPbx.Configuration.Model.dll",
                "IpPbx.Web.Model.dll",
                "IpPbxCDSClientLib.dll"
            };
            foreach (var dll in requiredDlls)
            {
                try
                {
                    Assembly.LoadFrom(System.IO.Path.Combine(swyxDir, dll));
                }
                catch (Exception ex) { Logging.Warn($"WcfLoginService: Load {dll} fehlgeschlagen: {ex.Message}"); }
            }

            // LibManagerLogin finden (static class in SWConfigDataClientLib)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("SWConfigDataClientLib.LibManagerLogin");
                if (t != null)
                {
                    _libManagerLoginType = t;
                    Logging.Info($"WcfLoginService: LibManagerLogin gefunden in {asm.GetName().Name}.");
                    break;
                }
            }
            if (_libManagerLoginType == null)
                throw new InvalidOperationException("SWConfigDataClientLib.LibManagerLogin nicht gefunden.");

            // LoginWithCredentials(string, string, string) finden
            _loginMethod = _libManagerLoginType.GetMethod("LoginWithCredentials",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(string) },
                modifiers: null);

            if (_loginMethod == null)
                throw new InvalidOperationException("LoginWithCredentials(string,string,string) nicht gefunden.");
        }

        /// <summary>
        /// Führt WCF-Login via 127.0.0.1:9094 durch.
        /// Voraussetzung: CLMgr läuft und WCF-Service ist erreichbar (CLMgrStartupService).
        /// </summary>
        public bool Login(string user, string password)
        {
            Logging.Info($"WcfLoginService: Login-Versuch für '{user}' via WCF ({LoopbackHost})...");

            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                object result = _loginMethod.Invoke(null, new object[] { LoopbackHost, user, password });
                watch.Stop();
                Logging.Info($"WcfLoginService: Login-Aufruf nach {watch.ElapsedMilliseconds}ms zurück.");

                if (result == null)
                {
                    Logging.Error("WcfLoginService: Result war null.");
                    return false;
                }

                return ParseResult(result, user);
            }
            catch (TargetInvocationException tie)
            {
                Logging.Error($"WcfLoginService: Login-Fehler: {tie.InnerException?.Message ?? tie.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logging.Error($"WcfLoginService: Login-Fehler: {ex.Message}");
                return false;
            }
        }

        private bool ParseResult(object result, string user)
        {
            var type = result.GetType();

            // ResultType lesen
            var rtProp = type.GetProperty("ResultType");
            if (rtProp == null)
            {
                Logging.Error("WcfLoginService: ResultType-Property fehlt.");
                return false;
            }
            var rtValue = rtProp.GetValue(result);
            string rtName = rtValue?.ToString() ?? "?";

            // Standard-Properties extrahieren
            string accessToken = GetStringValue(result, "AccessToken");
            string refreshToken = GetStringValue(result, "RefreshToken");
            int userId = GetIntValue(result, "UserId");
            var libMgr = GetPropertyValue(result, "LibManager");

            Logging.Info($"WcfLoginService: ResultType={rtName}, UserId={userId}, AccessToken.Len={accessToken?.Length ?? 0}");

            if (rtName == "Success" && !string.IsNullOrEmpty(accessToken))
            {
                IsLoggedIn = true;
                AccessToken = accessToken;
                RefreshToken = refreshToken;
                UserId = userId;
                UserName = user;
                _libManager = libMgr;
                Logging.Info($"WcfLoginService: ✅ Login erfolgreich (User={user}, Id={userId}).");
                return true;
            }

            Logging.Error($"WcfLoginService: ❌ Login fehlgeschlagen (ResultType={rtName}).");
            IsLoggedIn = false;
            return false;
        }

        public void Logout()
        {
            IsLoggedIn = false;
            AccessToken = null;
            RefreshToken = null;
            UserId = 0;
            UserName = null;
            _libManager = null;
            Logging.Info("WcfLoginService: Ausgeloggt.");
        }

        /// <summary>
        /// Markiert den Service als eingeloggt, wenn CLMgr bereits eine aktive Session hat
        /// (z.B. weil SwyxIt! vorhin aktiv war). Wir bekommen dann keinen neuen JWT,
        /// aber CLMgr selbst ist authentifiziert.
        /// </summary>
        public void MarkAlreadyLoggedIn(string userName)
        {
            IsLoggedIn = true;
            UserName = userName;
            Logging.Info($"WcfLoginService: Markiert als eingeloggt (vorbestehende Session, User='{userName}').");
        }

        // Reflection-Helfer
        private static string GetStringValue(object obj, string propName)
        {
            var p = obj.GetType().GetProperty(propName);
            return p?.GetValue(obj) as string;
        }

        private static int GetIntValue(object obj, string propName)
        {
            var p = obj.GetType().GetProperty(propName);
            var v = p?.GetValue(obj);
            return v is int i ? i : 0;
        }

        private static object GetPropertyValue(object obj, string propName)
        {
            var p = obj.GetType().GetProperty(propName);
            return p?.GetValue(obj);
        }
    }
}
