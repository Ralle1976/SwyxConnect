/*
 * TeamsConnector
 * 
 * MIT License
 * 
 * Copyright (C) 2023 by Simon Baer
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.Runtime.InteropServices;
using Microsoft.Office.Uc;
using Microsoft.Win32;
using SwyxBridge.Teams.Automation;

namespace SwyxBridge.Teams
{
    public class TeamsClient : IDisposable
    {
        private string version = "15.0.0.0";
        private UCOfficeIntegration teamsOfficeIntegration;
        private bool modalityAudioVideoAvailable;
        private Client ucClient;
        private bool _disposed;
        private string selfUri;
        private Contact selfContact;
        private ContactSubscription? subscription;
        private Availability currentPresenceAvailability = Availability.Unknown;
        private string currentPresenceActivityId = "Unknown";
        private const int Running = 2;
        private const string ClientVersionLegacy = "Teams";
        private const string ClientVersion2023 = "MsTeams";
        private TeamsClientVersion clientVersion;
        private Automation.Automation automation;

        public TeamsClient()
        {
            try
            {
#pragma warning disable CA1416
                string? clientName = Registry.GetValue("HKEY_CURRENT_USER\\Software\\IM Providers", "DefaultIMApp", null) as string;
#pragma warning restore CA1416
                switch (clientName)
                {
                    case ClientVersionLegacy: clientVersion = TeamsClientVersion.Legacy; break;
                    case ClientVersion2023: clientVersion = TeamsClientVersion.New2023; break;
                    default: clientVersion = TeamsClientVersion.Unknown; break;
                }
                if (clientVersion == TeamsClientVersion.Unknown)
                {
                    Logger.Log("Teams is not the default IM client.");
                    throw new TeamsConnectorException("Teams is not the default IM client.");
                }
#pragma warning disable CA1416
                int upAndRunning = (int)(Registry.GetValue($"HKEY_CURRENT_USER\\Software\\IM Providers\\{clientName}", "UpAndRunning", 0) ?? 0);
#pragma warning restore CA1416
                if (upAndRunning != Running)
                {
                    Logger.Log("Teams is not running.");
                    throw new TeamsConnectorException("Teams is not running.");
                }
                switch (clientVersion)
                {
                    case TeamsClientVersion.Legacy:
                        teamsOfficeIntegration = (UCOfficeIntegration)new TeamsOfficeIntegration();
                        break;
                    case TeamsClientVersion.New2023:
                        teamsOfficeIntegration = (UCOfficeIntegration)new TeamsOfficeIntegration2023();
                        break;
                    default:
                        throw new TeamsConnectorException("Unexpected error.");
                }
                if (teamsOfficeIntegration.GetAuthenticationInfo(version) != "<authenticationinfo>")
                {
                    Logger.Log("IM provider does not support Office Integration.");
                    throw new TeamsConnectorException("IM provider does not support Office Integration.");
                }
                ucClient = (Client)(dynamic)teamsOfficeIntegration.GetInterface(version, OIInterface.oiInterfaceILyncClient);
                _ = ucClient.ContactManager;
                Logger.Log($"Client state: {ucClient.State}");
                selfUri = ucClient.Uri;
                Logger.Log($"Self URI: {selfUri}");
                selfContact = ucClient.ContactManager.GetContactByUri(selfUri);
                modalityAudioVideoAvailable = selfContact.CanStart(ModalityTypes.ucModalityAudioVideo);
                automation = new Automation.Automation();
            }
            catch (COMException ex)
            {
                Logger.Log($"Cannot connect to Teams: {ex.Message}");
                throw new TeamsConnectorException($"Cannot connect to Teams: {ex.Message}", ex);
            }
            catch (Exception ex2)
            {
                Logger.Log(ex2);
                throw;
            }
        }

        public string? CurrentUser => selfUri;
        public bool IsConnected => !_disposed && ucClient != null && ucClient.State == ClientState.ucClientStateSignedIn;

        public event EventHandler<PresenceChangedEventArgs>? PresenceChanged;
        public event EventHandler<IncomingCallEventArgs> IncomingCall
        {
            add { automation.IncomingCall += value; }
            remove { automation.IncomingCall -= value; }
        }

        public void CreatePresenceSubscription()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TeamsClient));
            if (subscription == null)
            {
                Logger.Log($"Self Contact E-Mail: {(dynamic)selfContact.GetContactInformation(ContactInformationType.ucPresencePrimaryEmailAddress)}");
                selfContact.OnContactInformationChanged += OnContactInformationChanged;
                subscription = ucClient.ContactManager.CreateSubscription();
                ContactInformationType[] contactInformationTypes = new ContactInformationType[] { ContactInformationType.ucPresenceAvailability, ContactInformationType.ucPresenceActivityId };
                subscription.AddContact(selfContact);
                subscription.Subscribe(ContactSubscriptionRefreshRate.ucSubscriptionFreshnessHigh, contactInformationTypes);
                Logger.Log("Contact information change event subscribed.");
            }
        }

        public void DeletePresenceSubscription()
        {
            if (subscription != null)
            {
                selfContact.OnContactInformationChanged -= OnContactInformationChanged;
                subscription.Unsubscribe();
                subscription = null;
                Logger.Log("Contact information change event unsubscribed.");
            }
        }

        public Availability GetAvailability()
        {
            if (!_disposed)
                return (Availability)(int)(dynamic)selfContact.GetContactInformation(ContactInformationType.ucPresenceAvailability);
            return Availability.Unknown;
        }

        public string GetActivity()
        {
            if (!_disposed)
                return (string)(dynamic)selfContact.GetContactInformation(ContactInformationType.ucPresenceActivityId);
            return "Unknown";
        }

        public bool MakeCall(string phoneNumber)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TeamsClient));
            Logger.Log($"Trying to make a call to \"{phoneNumber}\"...");
            try
            {
                if (IsConnected && modalityAudioVideoAvailable)
                {
                    IAutomation automation = (IAutomation)(dynamic)teamsOfficeIntegration.GetInterface(version, OIInterface.oiInterfaceIAutomation);
                    string[] participantUris = new string[] { phoneNumber };
                    automation.StartConversation(AutomationModalities.uiaConversationModeAudio, participantUris, null, null);
                    return true;
                }
                Logger.Log("Connection to Teams or modality does not allow to make calls!");
            }
            catch (Exception ex) { Logger.Log("Exception: " + ex.ToString()); }
            return false;
        }

        public bool SetAvailability(Availability availability)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TeamsClient));
            if (clientVersion == TeamsClientVersion.New2023)
            {
                var mainWin = automation.GetWindowByTitle(".*Microsoft Teams");
                if (mainWin == null) { Logger.Log("No Teams window found!"); return false; }
                Logger.Log($"Main window: hwnd={mainWin.CurrentNativeWindowHandle}, Name={mainWin.CurrentName} Class={mainWin.CurrentClassName} Type={mainWin.CurrentControlType}");
                var profilButton = mainWin.FindFirst(Automation.ControlType.Button, null, "Ihr Profilbild");
                profilButton?.Expand();
                var profilMenu = mainWin.FindFirst(Automation.ControlType.Window, null, "Profilmenü", 500);
                if (profilMenu != null)
                {
                    var status = profilMenu.FindFirst(Automation.ControlType.MenuItem, "fui-MenuItem", null, 500);
                    status?.Expand();
                }
                var statusMenu = mainWin.FindFirst(Automation.ControlType.Menu, null, "Status ändern", 500);
                if (statusMenu != null)
                {
                    var newStatusItem = statusMenu.FindFirst(Automation.ControlType.RadioButton, null, "presenceText", 500);
                    if (newStatusItem == null || !newStatusItem.Invoke()) { Logger.Log("Invoke on new status failed!"); return false; }
                }
                else { Logger.Log("Menu element to change status not found!"); return false; }
                if (profilButton == null) profilButton = mainWin.FindFirst(Automation.ControlType.Button, null, "Ihr Profilbild");
                profilButton?.Collapse();
                return true;
            }
            else
            {
                Logger.Log("'SetAvailability' is only available with the new (2023) Teams client!");
                return false;
            }
        }

        private void OnContactInformationChanged(IContact contact, IContactInformationChangedEventData e)
        {
            Logger.Log($"Presence event: Contact information changed for {contact.Uri}");
            int availabilityNumber = 0;
            string activityText = string.Empty;
            for (int i = 0; i < e.ChangedContactInformation.Length; i++)
            {
                Logger.Log(i + ": " + e.ChangedContactInformation[i].ToString() + " new value " + (dynamic)contact.GetContactInformation(e.ChangedContactInformation[i]));
                switch (e.ChangedContactInformation[i])
                {
                    case ContactInformationType.ucPresenceAvailability:
                        availabilityNumber = (int)(dynamic)contact.GetContactInformation(e.ChangedContactInformation[i]);
                        break;
                    case ContactInformationType.ucPresenceActivityId:
                        activityText = (string)(dynamic)contact.GetContactInformation(e.ChangedContactInformation[i]);
                        break;
                }
            }
            currentPresenceActivityId = ((activityText != string.Empty) ? activityText : currentPresenceActivityId);
            currentPresenceAvailability = ((availabilityNumber != 0) ? (Availability)availabilityNumber : currentPresenceAvailability);
            PresenceChanged?.Invoke(this, new PresenceChangedEventArgs(currentPresenceAvailability, currentPresenceActivityId));
        }

        ~TeamsClient() { Dispose(disposing: false); }
        public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }
        private void Dispose(bool disposing)
        {
            if (_disposed || !disposing) return;
            lock (this) { DeletePresenceSubscription(); automation?.Dispose(); _disposed = true; }
        }
    }
}
