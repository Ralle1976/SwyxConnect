using System;
using System.Threading;

namespace SwyxBridge.Standalone.Utils
{
    /// <summary>
    /// Wrapper um den STA-SynchronizationContext (von WinForms installiert).
    /// Erlaubt Background-Threads, Code auf dem STA-Thread auszuführen.
    /// </summary>
    public sealed class StaDispatcher
    {
        private readonly SynchronizationContext _ctx;

        public StaDispatcher()
        {
            _ctx = SynchronizationContext.Current
                ?? throw new InvalidOperationException("SynchronizationContext.Current is null — must be created on STA thread.");
        }

        public void Post(SendOrPostCallback action, object state = null)
        {
            _ctx.Post(action, state);
        }
    }
}
