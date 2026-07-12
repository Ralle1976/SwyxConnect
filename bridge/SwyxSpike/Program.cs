using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SwyxSpike;

/// <summary>
/// Wave 0 Spike: Minimaler COM-Konnektivitätstest.
/// Beweist, dass .NET 8 mit Swyx COM Events funktioniert.
/// EXIT CRITERIA: Event feuert → weiter mit .NET 8.
///                Event feuert nicht → Fallback auf .NET Framework 4.8.
/// </summary>
static class Program
{
    // CRITICAL: Static field to prevent GC from collecting the event sink.
    // COM holds a reference, but GC doesn't know about COM references.
    private static dynamic? _clmgr;
    private static Action<int, int>? _eventDelegate;

    [STAThread]
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Console.Error.WriteLine("[SPIKE] SwyxSpike startet...");
        Console.Error.WriteLine("[SPIKE] Versuche COM-Objekt CLMgr.ClientLineMgr zu erstellen...");

        try
        {
            var comType = Type.GetTypeFromProgID("CLMgr.ClientLineMgr");
            if (comType == null)
            {
                Console.Error.WriteLine("[SPIKE] FEHLER: ProgID 'CLMgr.ClientLineMgr' nicht gefunden.");
                Console.Error.WriteLine("[SPIKE] Ist SwyxIt! installiert?");
                return;
            }

            _clmgr = Activator.CreateInstance(comType);
            Console.Error.WriteLine("[SPIKE] COM-Objekt erfolgreich erstellt!");
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80070005))
        {
            Console.Error.WriteLine("[SPIKE] E_ACCESSDENIED: Keine Berechtigung.");
            Console.Error.WriteLine("[SPIKE] Läuft SwyxIt! als gleicher Benutzer?");
            return;
        }
        catch (COMException ex)
        {
            Console.Error.WriteLine($"[SPIKE] COM-Fehler: 0x{ex.HResult:X8} - {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SPIKE] Unerwarteter Fehler: {ex.GetType().Name} - {ex.Message}");
            return;
        }

        // Event-Handler registrieren (static delegate für GC-Schutz)
        _eventDelegate = OnLineMgrNotification;
        try
        {
            ((dynamic)_clmgr!).PubOnLineMgrNotification += _eventDelegate;
            Console.Error.WriteLine("[SPIKE] Event-Sink registriert. Warte auf Events...");
            Console.Error.WriteLine("[SPIKE] Starte einen Anruf in SwyxIt! oder ändere den Status.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SPIKE] Event-Registrierung fehlgeschlagen: {ex.Message}");
            Console.Error.WriteLine("[SPIKE] .NET 8 COM Events funktionieren NICHT. Fallback auf .NET Fx 4.8.");
            return;
        }

        // Message Pump starten — ohne diese werden COM Events nie dispatched
        Console.Error.WriteLine("[SPIKE] Message Pump läuft. Ctrl+C zum Beenden.");
        Application.Run(new ApplicationContext());
    }

    /// <summary>
    /// Event-Handler für PubOnLineMgrNotification.
    /// Gibt empfangene Events als JSON auf stdout aus.
    /// </summary>
    private static void OnLineMgrNotification(int msg, int param)
    {
        string eventName = msg switch
        {
            0 => "lineStateChanged",
            1 => "lineSelectionChanged",
            2 => "lineDetailsChanged",
            3 => "lineDetailsChangedEx",
            4 => "configChanged",
            5 => "speedDialNotification",
            6 => "groupNotification",
            7 => "chatMessage",
            _ => $"unknown_{msg}"
        };

        // JSON auf stdout — das ist der Beweis dass Events funktionieren
        Console.WriteLine($"{{\"event\":\"{eventName}\",\"msg\":{msg},\"param\":{param}}}");
        Console.Error.WriteLine($"[SPIKE] ✅ EVENT EMPFANGEN: {eventName} (msg={msg}, param={param})");
    }
}
