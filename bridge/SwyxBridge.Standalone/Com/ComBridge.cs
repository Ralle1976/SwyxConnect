using System;
using System.Reflection;
using System.Runtime.InteropServices;
using SwyxBridge.Standalone.Utils;

namespace SwyxBridge.Standalone.Com
{
    /// <summary>
    /// COM-Brücke zu CLMgr für Telefonie-Operationen.
    /// Nutzt dynamic + Type.InvokeMember (late binding via IDispatch) — bewiesenermaßen sicher
    /// für Out-of-Process-COM-Server (v1.6.0-Stand).
    ///
    /// WICHTIG: Alle Methoden müssen auf dem STA-Thread aufgerufen werden!
    /// </summary>
    public sealed class ComBridge : IDisposable
    {
        private object _clmgr; // COM-Object — hält die CLMgr-Session am Leben
        private bool _disposed;

        public bool IsConnected => _clmgr != null;

        public ComBridge(object comObject)
        {
            _clmgr = comObject ?? throw new ArgumentNullException(nameof(comObject));
        }

        public object GetCom()
        {
            if (_clmgr == null)
                throw new InvalidOperationException("COM nicht verbunden.");
            return _clmgr;
        }

        // --- Session Status ---

        public bool IsLoggedIn()
        {
            try { return (int)GetProp("DispIsLoggedIn") != 0; }
            catch { return false; }
        }

        public string GetCurrentUser()
        {
            try { return (string)GetProp("DispGetCurrentUser"); }
            catch { return null; }
        }

        public string GetCurrentServer()
        {
            try { return (string)GetProp("DispGetCurrentServer"); }
            catch { return null; }
        }

        // --- Line Management ---

        public int GetLineCount()
        {
            try { return (int)GetProp("DispNumberOfLines"); }
            catch { return 0; }
        }

        public int GetSelectedLineNumber()
        {
            try { return (int)GetProp("DispSelectedLineNumber"); }
            catch { return 0; }
        }

        public void SetNumberOfLines(int count)
        {
            Logging.Info($"ComBridge: SetNumberOfLines({count})");
            try { Invoke("DispSetNumberOfLines", count); }
            catch (Exception ex) { Logging.Warn($"ComBridge: SetNumberOfLines fehlgeschlagen: {ex.Message}"); }
        }

        // --- Call Control ---

        public void Dial(string number)
        {
            Logging.Info($"ComBridge: Dial('{number}')");
            try
            {
                Invoke("DispSimpleDialEx3", number, 0, 0, "");
                Logging.Info("ComBridge: DispSimpleDialEx3 erfolgreich.");
            }
            catch (Exception ex)
            {
                Logging.Warn($"ComBridge: DispSimpleDialEx3 fehlgeschlagen: {ex.Message}, versuche Fallback...");
                // Fallback: DispSelectedLine.DispDial
                try
                {
                    var sel = GetProp("DispSelectedLine");
                    if (sel != null)
                    {
                        sel.GetType().InvokeMember("DispDial",
                            BindingFlags.InvokeMethod, null, sel, new object[] { number });
                        Logging.Info("ComBridge: Fallback DispDial erfolgreich.");
                    }
                }
                catch (Exception ex2)
                {
                    Logging.Error($"ComBridge: Dial fehlgeschlagen: {ex2.Message}");
                }
            }
        }

        public void Hangup()
        {
            Logging.Info("ComBridge: Hangup()");
            // Method 1: DispSelectedLine.DispHookOn
            try
            {
                var sel = GetProp("DispSelectedLine");
                if (sel != null)
                {
                    sel.GetType().InvokeMember("DispHookOn",
                        BindingFlags.InvokeMethod, null, sel, null);
                    Logging.Info("ComBridge: Hangup via DispSelectedLine.DispHookOn.");
                    return;
                }
            }
            catch (Exception ex) { Logging.Warn($"ComBridge: DispSelectedLine.DispHookOn fehlgeschlagen: {ex.Message}"); }
            // Method 2: GetCom().DispHookOn
            try { Invoke("DispHookOn"); Logging.Info("ComBridge: Hangup via DispHookOn."); }
            catch (Exception ex) { Logging.Warn($"ComBridge: DispHookOn fehlgeschlagen: {ex.Message}"); }
        }

        public void HookOff(int lineId)
        {
            try
            {
                var line = InvokeGetLine(lineId);
                line?.GetType().InvokeMember("DispHookOff",
                    BindingFlags.InvokeMethod, null, line, null);
            }
            catch (Exception ex) { Logging.Warn($"ComBridge: HookOff({lineId}): {ex.Message}"); }
        }

        public void HookOn(int lineId)
        {
            try
            {
                var line = InvokeGetLine(lineId);
                line?.GetType().InvokeMember("DispHookOn",
                    BindingFlags.InvokeMethod, null, line, null);
            }
            catch (Exception ex) { Logging.Warn($"ComBridge: HookOn({lineId}): {ex.Message}"); }
        }

        public void Hold(int lineId)
        {
            try
            {
                var line = InvokeGetLine(lineId);
                line?.GetType().InvokeMember("DispHold",
                    BindingFlags.InvokeMethod, null, line, null);
            }
            catch (Exception ex) { Logging.Warn($"ComBridge: Hold({lineId}): {ex.Message}"); }
        }

        // --- Line State Queries ---

        public object GetLineState(int lineId)
        {
            try
            {
                var line = InvokeGetLine(lineId);
                if (line == null) return null;

                int stateInt = (int)line.GetType().InvokeMember("DispState",
                    BindingFlags.GetProperty, null, line, null);
                string callerName = (string)(line.GetType().InvokeMember("DispCallerName",
                    BindingFlags.GetProperty, null, line, null) ?? "");
                string callerNumber = (string)(line.GetType().InvokeMember("DispCallerNumber",
                    BindingFlags.GetProperty, null, line, null) ?? "");

                return new
                {
                    id = lineId,
                    state = MapLineState(stateInt),
                    callerName,
                    callerNumber,
                    isSelected = (lineId == GetSelectedLineNumber())
                };
            }
            catch (Exception ex)
            {
                Logging.Warn($"ComBridge: GetLineState({lineId}): {ex.Message}");
                return new { id = lineId, state = "Inactive", callerName = "", callerNumber = "", isSelected = false };
            }
        }

        public object GetAllLines()
        {
            int count = GetLineCount();
            if (count == 0)
            {
                SetNumberOfLines(2);
                count = GetLineCount();
            }

            var lines = new System.Collections.Generic.List<object>();
            int selected = GetSelectedLineNumber();
            for (int i = 0; i < count; i++)
                lines.Add(GetLineState(i));
            return new { lines };
        }

        // --- Helpers ---

        private object GetProp(string name)
        {
            return _clmgr.GetType().InvokeMember(name,
                BindingFlags.GetProperty, null, _clmgr, null);
        }

        private void Invoke(string name, params object[] args)
        {
            _clmgr.GetType().InvokeMember(name,
                BindingFlags.InvokeMethod, null, _clmgr, args);
        }

        private object InvokeGetLine(int lineId)
        {
            try
            {
                return _clmgr.GetType().InvokeMember("DispGetLine",
                    BindingFlags.InvokeMethod, null, _clmgr, new object[] { lineId });
            }
            catch
            {
                // Fallback: DispSelectedLine
                try { return GetProp("DispSelectedLine"); } catch { return null; }
            }
        }

        private static string MapLineState(int stateInt) => stateInt switch
        {
            0 => "Inactive", 1 => "HookOffInternal", 2 => "HookOffExternal",
            3 => "Ringing", 4 => "Dialing", 5 => "Alerting", 6 => "Knocking",
            7 => "Busy", 8 => "Active", 9 => "OnHold",
            10 => "ConferenceActive", 11 => "ConferenceOnHold", 12 => "Terminated",
            13 => "Transferring", 14 => "Disabled", 15 => "DirectCall",
            _ => "Inactive"
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_clmgr != null)
            {
                try { Marshal.FinalReleaseComObject(_clmgr); } catch { }
                _clmgr = null;
            }
        }
    }
}
