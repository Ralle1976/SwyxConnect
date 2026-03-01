using SwyxBridge.Utils;
namespace SwyxBridge.Teams
{
    public static class Logger
    {
        public static void Log(string message) => Logging.Info($"[TeamsConnector] {message}");
        public static void Log(string format, params object[] args)
        {
            try { Logger.Log(string.Format(format, args)); }
            catch (Exception ex) { Logger.Log("Exception: " + ex.ToString()); }
        }
        public static void Log(Exception ex) => Logging.Error($"[TeamsConnector] {ex}");
    }
}
