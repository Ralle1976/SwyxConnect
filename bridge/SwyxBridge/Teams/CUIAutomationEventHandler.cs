using Interop.UIAutomationClient;
namespace SwyxBridge.Teams
{
    internal class CUIAutomationEventHandler : IUIAutomationEventHandler
    {
        public event EventHandler<UIAutomationEventArgs>? UIAutomationEvent;
        public void HandleAutomationEvent(IUIAutomationElement sender, int eventId)
        {
            UIAutomationEvent?.Invoke(sender, new UIAutomationEventArgs(eventId));
        }
    }
}
