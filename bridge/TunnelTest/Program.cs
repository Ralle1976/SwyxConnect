using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Net.Sockets;
using System.Windows.Forms;
using Microsoft.Win32;

class Program
{
    static readonly Guid CLSID = new("F8E552F8-4C00-11D3-80BC-00105A653379");
    static object? comObj;

    [STAThread]
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== TunnelTest v7: RegisterUserEx mit echten Parametern ===");
        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        // Registry-Werte lesen
        Console.WriteLine("--- Registry-Werte ---");
        string regPath = @"Software\Swyx\SwyxIt!\CurrentVersion\Options";
        string pbxServer = "";
        string pbxUser = "";
        string publicAuthServer = "";
        string publicServer = "";
        int connectorUsage = 0;
        int trustedAuth = 0;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(regPath);
            if (key != null)
            {
                pbxServer = key.GetValue("PbxServer", "")?.ToString() ?? "";
                pbxUser = key.GetValue("PbxUser", "")?.ToString() ?? "";
                publicAuthServer = key.GetValue("PublicAuthServerName", "")?.ToString() ?? "";
                publicServer = key.GetValue("PublicServerName", "")?.ToString() ?? "";
                connectorUsage = Convert.ToInt32(key.GetValue("ConnectorUsage", 0));
                trustedAuth = Convert.ToInt32(key.GetValue("TrustedAuthentication", 0));

                Console.WriteLine($"    PbxServer           = '{pbxServer}'");
                Console.WriteLine($"    PbxUser             = '{pbxUser}'");
                Console.WriteLine($"    PublicAuthServer     = '{publicAuthServer}'");
                Console.WriteLine($"    PublicServer         = '{publicServer}'");
                Console.WriteLine($"    ConnectorUsage      = {connectorUsage}");
                Console.WriteLine($"    TrustedAuthentication= {trustedAuth}");
            }
            else
            {
                Console.WriteLine("    WARNUNG: Registry-Key nicht gefunden!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Registry-Fehler: {ex.Message}");
        }
        Console.WriteLine();

        // CoCreateInstance
        Console.Write("CoCreateInstance... ");
        var type = Type.GetTypeFromCLSID(CLSID);
        comObj = Activator.CreateInstance(type!);
        Console.WriteLine("OK");
        PrintFullStatus("nach CoCreateInstance");

        // Hidden Form fuer Message-Pump
        var form = new Form { Text = "TunnelTest", ShowInTaskbar = false, WindowState = FormWindowState.Minimized };
        form.Show();
        form.Hide();
        uint hwnd = (uint)form.Handle.ToInt32();
        uint threadId = (uint)AppDomain.GetCurrentThreadId();
        Console.WriteLine($"HWND=0x{hwnd:X}, ThreadId={threadId}");
        Console.WriteLine();

        // RegisterMessageTarget
        Console.WriteLine(">>> RegisterMessageTarget");
        try
        {
            dynamic d = comObj!;
            d.DispRegisterMessageTarget((int)hwnd, (int)threadId);
            Console.WriteLine("    OK");
        }
        catch (Exception ex) { Console.WriteLine($"    {ex.GetType().Name}: {ex.Message}"); }
        PumpAndWait(1);
        Console.WriteLine();

        // =============================================
        // TEST A: RegisterUserEx mit PbxServer + Windows Auth (Mode 0)
        // =============================================
        Console.WriteLine($">>> TEST A: RegisterUserEx(\"{pbxServer}\", \"\", \"\", \"\", 0, 0) -- Windows Auth mit PbxServer");
        bool testA_ok = TryRegisterUserEx(pbxServer, "", "", "", 0, 0);
        PumpAndWait(5);
        PrintFullStatus("nach TEST A");
        Console.WriteLine();

        if (!testA_ok)
        {
            // =============================================
            // TEST B: RegisterUserEx mit PbxServer + PbxUser + Mode 0
            // =============================================
            Console.WriteLine($">>> TEST B: RegisterUserEx(\"{pbxServer}\", \"\", \"{pbxUser}\", \"\", 0, 0) -- Win Auth + User");
            bool testB_ok = TryRegisterUserEx(pbxServer, "", pbxUser, "", 0, 0);
            PumpAndWait(5);
            PrintFullStatus("nach TEST B");
            Console.WriteLine();

            if (!testB_ok)
            {
                // =============================================
                // TEST C: RegisterUserEx mit PbxServer + PbxUser + Mode 1
                // =============================================
                Console.WriteLine($">>> TEST C: RegisterUserEx(\"{pbxServer}\", \"\", \"{pbxUser}\", \"\", 1, 0) -- PW Auth (empty PW)");
                bool testC_ok = TryRegisterUserEx(pbxServer, "", pbxUser, "", 1, 0);
                PumpAndWait(5);
                PrintFullStatus("nach TEST C");
                Console.WriteLine();

                if (!testC_ok)
                {
                    // =============================================
                    // TEST D: RegisterUserEx mit PbxServer + Mode 2 (Federated)
                    // =============================================
                    Console.WriteLine($">>> TEST D: RegisterUserEx(\"{pbxServer}\", \"\", \"{pbxUser}\", \"\", 2, 0) -- Federated Auth");
                    bool testD_ok = TryRegisterUserEx(pbxServer, "", pbxUser, "", 2, 0);
                    PumpAndWait(5);
                    PrintFullStatus("nach TEST D");
                    Console.WriteLine();

                    if (!testD_ok)
                    {
                        // =============================================
                        // TEST E: RegisterUserEx mit 127.0.0.1 (lokaler Tunnel) + Mode 0
                        // SwyxIt! zeigte Server=127.0.0.1 wenn verbunden
                        // =============================================
                        Console.WriteLine(">>> TEST E: RegisterUserEx(\"127.0.0.1\", \"\", \"\", \"\", 0, 0) -- lokaler Tunnel");
                        bool testE_ok = TryRegisterUserEx("127.0.0.1", "", "", "", 0, 0);
                        PumpAndWait(5);
                        PrintFullStatus("nach TEST E");
                        Console.WriteLine();

                        if (!testE_ok)
                        {
                            // =============================================
                            // TEST F: Unbekannte Modi 3-5
                            // =============================================
                            for (int mode = 3; mode <= 5; mode++)
                            {
                                Console.WriteLine($">>> TEST F{mode}: RegisterUserEx(\"{pbxServer}\", \"\", \"{pbxUser}\", \"\", {mode}, 0)");
                                bool ok = TryRegisterUserEx(pbxServer, "", pbxUser, "", mode, 0);
                                PumpAndWait(3);
                                PrintFullStatus($"nach Mode {mode}");
                                Console.WriteLine();
                                if (ok) break;
                            }
                        }
                    }
                }
            }
        }

        // =============================================
        // LANGZEIT-WATCH: 60 Sek
        // =============================================
        Console.WriteLine(">>> LANGZEIT: 60 Sek Monitoring...");
        for (int i = 0; i < 60; i++)
        {
            Application.DoEvents();
            Thread.Sleep(1000);
            Application.DoEvents();

            bool cds = CheckPort(9094);
            bool sip = CheckPort(5060);
            bool ipc = CheckPort(12042);

            string line = $"    [{i + 1:D2}s] CDS={(cds ? "JA" : "---")} SIP={(sip ? "JA" : "---")} IPC={(ipc ? "JA" : "---")}";
            try
            {
                dynamic d = comObj!;
                line += $" LoggedIn={d.DispIsLoggedIn} ServerUp={d.DispIsServerUp}";
                try { string s = d.DispGetCurrentServer; if (!string.IsNullOrEmpty(s)) line += $" Srv={s}"; } catch { }
                try { string u = d.DispGetCurrentUser; if (!string.IsNullOrEmpty(u)) line += $" Usr={u}"; } catch { }
            }
            catch { }
            Console.WriteLine(line);

            if (cds && sip)
            {
                Console.WriteLine();
                Console.WriteLine("    *** TUNNEL AKTIV! ***");
                break;
            }
        }

        Console.WriteLine();
        PrintFullStatus("FINAL");
        Console.WriteLine();

        // Aufraeumen
        if (comObj != null)
        {
            try { Marshal.ReleaseComObject(comObj); } catch { }
        }
    }

    static bool TryRegisterUserEx(string server, string backup, string user, string pw, int authMode, int ctiMaster)
    {
        try
        {
            dynamic d = comObj!;
            d.RegisterUserEx(server, backup, user, pw, authMode, ctiMaster, out string usernames);
            Console.WriteLine($"    OK! Usernames = '{usernames}'");
            return true;
        }
        catch (COMException ex)
        {
            Console.WriteLine($"    COM 0x{ex.ErrorCode:X8}: {ex.Message}");
            // Prüfe ob CLMgr noch lebt
            try { dynamic d2 = comObj!; int su = d2.DispIsServerUp; }
            catch (COMException ex2) when (ex2.ErrorCode == unchecked((int)0x800706BA))
            {
                Console.WriteLine("    !!! CLMgr ABGESTUERZT -- breche ab !!!");
                Environment.Exit(1);
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    static void PrintFullStatus(string label)
    {
        try
        {
            dynamic d = comObj!;
            bool cds = CheckPort(9094);
            bool sip = CheckPort(5060);
            bool ipc = CheckPort(12042);
            Console.WriteLine($"  STATUS {label}:");
            Console.WriteLine($"    LoggedIn={d.DispIsLoggedIn} ServerUp={d.DispIsServerUp} CDS={(cds ? "JA" : "---")} SIP={(sip ? "JA" : "---")} IPC={(ipc ? "JA" : "---")}");
            try { Console.WriteLine($"    Server='{d.DispGetCurrentServer}' User='{d.DispGetCurrentUser}'"); } catch { }
            try { Console.WriteLine($"    AuthMode={d.DispGetCurrentAuthMode} Lines={d.DispNumberOfLines}"); } catch { }
            try { Console.WriteLine($"    CloudConnStatus={d.CloudConnectorStatus} CloudConnSrv='{d.CloudConnectorServer}'"); } catch { }
        }
        catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x800706BA))
        {
            Console.WriteLine($"  STATUS {label}: CLMgr NICHT VERFUEGBAR (0x800706BA)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  STATUS {label}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    static void PumpAndWait(int seconds)
    {
        for (int i = 0; i < seconds; i++)
        {
            Application.DoEvents();
            Thread.Sleep(1000);
            Application.DoEvents();
        }
    }

    static bool CheckPort(int port)
    {
        try { using var t = new TcpClient(); t.Connect("127.0.0.1", port); t.Close(); return true; }
        catch { return false; }
    }
}
