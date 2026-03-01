namespace SwyxBridge.Teams
{
    public class PresenceChangedEventArgs : EventArgs
    {
        public PresenceChangedEventArgs(Availability availability, string activity)
        {
            Availability = availability;
            Activity = activity;
        }
        public string Activity { get; init; }
        public Availability Availability { get; init; }
    }
}
