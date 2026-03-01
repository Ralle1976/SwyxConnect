namespace SwyxBridge.Teams
{
    public class IncomingCallEventArgs : EventArgs
    {
        public IncomingCallEventArgs(string phoneNumber)
        {
            PhoneNumber = phoneNumber;
        }
        public string PhoneNumber { get; init; }
    }
}
