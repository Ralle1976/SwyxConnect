namespace SwyxBridge.Teams
{
    public class TeamsConnectorException : Exception
    {
        public TeamsConnectorException(string? message) : base(message) { }
        public TeamsConnectorException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
