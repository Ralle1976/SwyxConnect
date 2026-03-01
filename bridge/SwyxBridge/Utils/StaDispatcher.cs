namespace SwyxBridge.Utils;

/// <summary>
/// Fängt den SynchronizationContext des STA-Threads ein
/// und ermöglicht Marshalling von Background-Threads auf den STA-Thread.
/// MUSS auf dem STA-Thread instanziiert werden!
/// </summary>
public sealed class StaDispatcher
{
    private readonly SynchronizationContext _ctx;

    public StaDispatcher()
    {
        // WindowsFormsSynchronizationContext wird automatisch installiert
        // wenn UseWindowsForms=true und [STAThread] gesetzt ist
        _ctx = SynchronizationContext.Current
            ?? throw new InvalidOperationException(
                "StaDispatcher muss auf dem STA-Thread erstellt werden (nach Application-Initialisierung).");
    }

    /// <summary>
    /// Asynchron auf den STA-Thread posten. Kehrt sofort zurück.
    /// </summary>
    public void Post(Action action)
    {
        _ctx.Post(_ => action(), null);
    }

    /// <summary>
    /// Synchron auf den STA-Thread marshallen. Blockiert bis fertig.
    /// WARNUNG: Nur von Background-Threads aufrufen! Auf dem STA-Thread = Deadlock!
    /// </summary>
    public void Send(Action action)
    {
        _ctx.Send(_ => action(), null);
    }
}
