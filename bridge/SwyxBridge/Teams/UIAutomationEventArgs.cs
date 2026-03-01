namespace SwyxBridge.Teams
{
    public class UIAutomationEventArgs : EventArgs
    {
        public UIAutomationEventArgs(int eventId) { EventId = eventId; }
        public int EventId { get; set; }
    }
}
