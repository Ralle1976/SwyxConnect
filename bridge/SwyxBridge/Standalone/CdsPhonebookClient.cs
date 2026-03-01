// CDS WCF Phonebook Client — connects to SwyxWare ConfigDataStore via net.tcp
// Protocol reverse-engineered from IpPbxCDSClientLib.dll decompilation (ilspycmd)
//
// Phonebook endpoint: net.tcp://{host}:9094/ConfigDataStore/CUserPhoneBookEnumImpl.jwt2
// Auth: JWT token injected via CdsJwtEndpointBehavior (same as CdsPhoneClientFacade)
//
// Interface: IUserPhoneBookEnum — decompiled from SWConfigDataClientLib.WSUserPhoneBook
// Key method: getEnumNotForUpdate(TOrderBy[]) → TUserPhoneBookEnum
//   Contains UserPhoneBookEntries[] with m_Name, m_Number, m_UserID per entry.
// Alt method: GetPBXPhonebookElementsEx(...) → returns full phonebook with users, numbers, status etc.

using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Security.Cryptography.X509Certificates;

namespace SwyxBridge.Standalone;

// ──────────────────────────────────────────────────────────
// Data Contracts — exact match of decompiled types
// Namespace: SWConfigDataWSLib.Transport / SWConfigDataWSLib.Server.PhonebookManager
// ──────────────────────────────────────────────────────────

/// <summary>Public-facing entry returned to Electron. NOT a WCF contract.</summary>
public class CdsPhonebookEntry
{
    public int EntryId { get; set; }
    public string? Name { get; set; }
    public string? Number { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public int SiteId { get; set; }
    public int UserId { get; set; }
}

/// <summary>
/// TOrderBy — decompiled from SWConfigDataWSLib.Transport.TOrderBy
/// Used as parameter for getEnum/getEnumNotForUpdate.
/// </summary>
[DataContract(Name = "TOrderBy", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Transport")]
public class CdsTOrderBy : IExtensibleDataObject
{
    [DataMember] public bool ascending { get; set; }
    [DataMember] public string? columnName { get; set; }
    public ExtensionDataObject? ExtensionData { get; set; }
}

/// <summary>
/// TEnum — base class for TUserPhoneBookEnum.
/// Decompiled from SWConfigDataWSLib.Transport.TEnum
/// </summary>
[DataContract(Name = "TEnum", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Transport")]
[KnownType(typeof(CdsTUserPhoneBookEnum))]
public class CdsTEnum : IExtensibleDataObject
{
    public ExtensionDataObject? ExtensionData { get; set; }
}

/// <summary>
/// TUserPhoneBookEnum — decompiled from SWConfigDataWSLib.Transport.UserPhoneBook.TUserPhoneBookEnum
/// Contains UserPhoneBookEntries[] — the actual phonebook data.
/// </summary>
[DataContract(Name = "TUserPhoneBookEnum", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Transport.UserPhoneBook")]
public class CdsTUserPhoneBookEnum : CdsTEnum
{
    [DataMember] public CdsTUserPhoneBookEntry[]? UserPhoneBookEntries { get; set; }
    [DataMember] public CdsTUserPhoneBookEntry[]? UserPhoneBookDeletedEntries { get; set; }
}

/// <summary>
/// TUserPhoneBookEnum.TUserPhoneBookEntry — decompiled from SWConfigDataWSLib.Transport.UserPhoneBook
/// Each entry has: m_Name, m_Number, m_SearchNumber, m_UserID, m_EntryID, m_Hide, m_DataRowId
/// </summary>
[DataContract(Name = "TUserPhoneBookEnum.TUserPhoneBookEntry", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Transport.UserPhoneBook")]
public class CdsTUserPhoneBookEntry : IExtensibleDataObject
{
    [DataMember] public Guid m_DataRowId { get; set; }
    [DataMember] public int m_EntryID { get; set; }
    [DataMember] public bool m_EntryID_Is_Null { get; set; }
    [DataMember] public bool m_Hide { get; set; }
    [DataMember] public bool m_Hide_Is_Null { get; set; }
    [DataMember] public string? m_Name { get; set; }
    [DataMember] public bool m_Name_Is_Null { get; set; }
    [DataMember] public string? m_Number { get; set; }
    [DataMember] public bool m_Number_Is_Null { get; set; }
    [DataMember] public string? m_SearchNumber { get; set; }
    [DataMember] public bool m_SearchNumber_Is_Null { get; set; }
    [DataMember] public int m_UserID { get; set; }
    [DataMember] public bool m_UserID_Is_Null { get; set; }
    public ExtensionDataObject? ExtensionData { get; set; }
}

/// <summary>
/// QualilifiedEntityID — decompiled (note the typo 'Qualilified' is in the original Swyx code!)
/// </summary>
[DataContract(Name = "QualilifiedEntityID", Namespace = "http://schemas.datacontract.org/2004/07/IpPbxCDSSharedServiceLib.CommonModules")]
public class CdsQualilifiedEntityID : IExtensibleDataObject
{
    [DataMember] public int m_iEntityID { get; set; }
    [DataMember] public int m_iSiteID { get; set; }
    [DataMember] public int m_eQualifiedEntityType { get; set; }
    public ExtensionDataObject? ExtensionData { get; set; }
}

/// <summary>
/// TPBXPhonebookDataEntryBase — base for all phonebook data entries.
/// Decompiled from SWConfigDataWSLib.Server.PhonebookManager
/// </summary>
[DataContract(Name = "TPBXPhonebookDataEntryBase", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
[KnownType(typeof(CdsTPBXPhonebookUserData))]
[KnownType(typeof(CdsTPBXPhonebookInternalNumberData))]
[KnownType(typeof(CdsTPBXEditablePhonebookEntryData))]
public class CdsTPBXPhonebookDataEntryBase : IExtensibleDataObject
{
    [DataMember] public CdsQualilifiedEntityID? m_EntityID { get; set; }
    [DataMember] public long m_lRefID { get; set; }
    [DataMember] public long m_lTimeStamp { get; set; }
    public ExtensionDataObject? ExtensionData { get; set; }
}

/// <summary>
/// TPBXPhonebookUserData — internal user data (Name, Description, Email, ChatUserId).
/// Decompiled from SWConfigDataWSLib.Server.PhonebookManager
/// </summary>
[DataContract(Name = "TPBXPhonebookUserData", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXPhonebookUserData : CdsTPBXPhonebookDataEntryBase
{
    [DataMember] public string? m_strChatUserId { get; set; }
    [DataMember] public string? m_strDescription { get; set; }
    [DataMember] public string? m_strEmail { get; set; }
    [DataMember] public string? m_strName { get; set; }
}

/// <summary>
/// TPBXPhonebookInternalNumberData — internal phone number per user.
/// </summary>
[DataContract(Name = "TPBXPhonebookInternalNumberData", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXPhonebookInternalNumberData : CdsTPBXPhonebookDataEntryBase
{
    [DataMember] public bool m_bHide { get; set; }
    [DataMember] public int m_iInternalNumberId { get; set; }
    [DataMember] public string? m_strNumber { get; set; }
}

/// <summary>
/// TPBXEditablePhonebookEntryData — editable/personal phonebook entries.
/// </summary>
[DataContract(Name = "TPBXEditablePhonebookEntryData", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXEditablePhonebookEntryData : CdsTPBXPhonebookDataEntryBase
{
    // Placeholder — we don't need these fields for read-only contact retrieval
}

/// <summary>
/// PhonebookStatus enum — presence status per phonebook user.
/// </summary>
[DataContract(Name = "PhonebookStatus", Namespace = "http://schemas.datacontract.org/2004/07/IpPbxCDSSharedServiceLib.CDS")]
public enum CdsPhonebookStatus
{
    [EnumMember] LoggedOff,
    [EnumMember] LoggedOn,
    [EnumMember] Active,
    [EnumMember] NoStatus,
    [EnumMember] Away,
    [EnumMember] DoNotDisturb
}

/// <summary>
/// TPBXPhonebookStatusEntry2 — status per user.
/// </summary>
[DataContract(Name = "TPBXPhonebookStatusEntry2", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXPhonebookStatusEntry2 : CdsTPBXPhonebookDataEntryBase
{
    [DataMember] public CdsPhonebookStatus m_eStatus { get; set; }
}

/// <summary>
/// TPBXPhonebookRichStatusEntry — rich presence (placeholder).
/// </summary>
[DataContract(Name = "TPBXPhonebookRichStatusEntry", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXPhonebookRichStatusEntry : CdsTPBXPhonebookDataEntryBase
{
}

/// <summary>
/// Remaining stub data contracts needed by GetPBXPhonebookElementsEx out parameters.
/// </summary>
[DataContract(Name = "TPBXPhonebookExternalNumberData", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXPhonebookExternalNumberData : CdsTPBXPhonebookDataEntryBase { }

[DataContract(Name = "TPBXPhonebookPublicNumberData", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXPhonebookPublicNumberData : CdsTPBXPhonebookDataEntryBase { }

[DataContract(Name = "TPBXPhonebookGroupData", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXPhonebookGroupData : CdsTPBXPhonebookDataEntryBase { }

[DataContract(Name = "TPBXPhonebookSiteData", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXPhonebookSiteData : CdsTPBXPhonebookDataEntryBase { }

[DataContract(Name = "TPBXPhonebookUserEmailAddressData", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Server.PhonebookManager")]
public class CdsTPBXPhonebookUserEmailAddressData : CdsTPBXPhonebookDataEntryBase { }

// ──────────────────────────────────────────────────────────
// WCF Service Contract — IUserPhoneBookEnum
// EXACT match of decompiled interface from IpPbxCDSClientLib.dll
// ConfigurationName: "WSUserPhoneBook.IUserPhoneBookEnum"
// ──────────────────────────────────────────────────────────

[ServiceContract(ConfigurationName = "WSUserPhoneBook.IUserPhoneBookEnum")]
public interface ICdsUserPhoneBookEnum
{
    [OperationContract(Action = "http://tempuri.org/IUserPhoneBookEnum/Ping", ReplyAction = "http://tempuri.org/IUserPhoneBookEnum/PingResponse")]
    void Ping();

    [OperationContract(Action = "http://tempuri.org/IUserPhoneBookEnum/GetCurrentUserID", ReplyAction = "http://tempuri.org/IUserPhoneBookEnum/GetCurrentUserIDResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IUserPhoneBookEnum/GetCurrentUserIDTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    int GetCurrentUserID();

    [OperationContract(Action = "http://tempuri.org/IUserPhoneBookEnum/GetCurrentUserName", ReplyAction = "http://tempuri.org/IUserPhoneBookEnum/GetCurrentUserNameResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IUserPhoneBookEnum/GetCurrentUserNameTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    int GetCurrentUserName(out string UserName);

    [OperationContract(Action = "http://tempuri.org/IUserPhoneBookEnum/getEnumNotForUpdate", ReplyAction = "http://tempuri.org/IUserPhoneBookEnum/getEnumNotForUpdateResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IUserPhoneBookEnum/getEnumNotForUpdateTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    CdsTUserPhoneBookEnum getEnumNotForUpdate(CdsTOrderBy[] orderByList);

    [OperationContract(Action = "http://tempuri.org/IUserPhoneBookEnum/getEnum", ReplyAction = "http://tempuri.org/IUserPhoneBookEnum/getEnumResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IUserPhoneBookEnum/getEnumTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    CdsTUserPhoneBookEnum getEnum(CdsTOrderBy[] orderByList);

    [OperationContract(Action = "http://tempuri.org/IUserPhoneBookEnum/GetPBXPhonebookElementsEx", ReplyAction = "http://tempuri.org/IUserPhoneBookEnum/GetPBXPhonebookElementsExResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IUserPhoneBookEnum/GetPBXPhonebookElementsExTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    [return: MessageParameter(Name = "aEntryList")]
    CdsTPBXEditablePhonebookEntryData[] GetPBXPhonebookElementsEx(
        out CdsTPBXPhonebookExternalNumberData[] aExternalNumberEntryList,
        out CdsTPBXPhonebookInternalNumberData[] aInternalNumberListEntryList,
        out CdsTPBXPhonebookPublicNumberData[] aPublicNumberEntryList,
        out CdsTPBXPhonebookUserData[] aUserEntryList,
        out CdsTPBXPhonebookGroupData[] aGroupEntryList,
        out CdsTPBXPhonebookSiteData[] aSiteList,
        out CdsTPBXPhonebookUserEmailAddressData[] aUserEmailAddressList,
        out long[] aDeletedEntries,
        out CdsTPBXPhonebookStatusEntry2[] aStatusEntries,
        out CdsTPBXPhonebookRichStatusEntry[] aRichStatusEntries,
        out long lServerTimeStamp,
        out long lServerProcessRef,
        int iUserID, bool bAllEntries, bool bStatus, bool bRichStatus, long lClientTimestamp);

    [OperationContract(Action = "http://tempuri.org/IUserPhoneBookEnum/GetLocalSiteID", ReplyAction = "http://tempuri.org/IUserPhoneBookEnum/GetLocalSiteIDResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IUserPhoneBookEnum/GetLocalSiteIDTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    int GetLocalSiteID();
}

// ──────────────────────────────────────────────────────────
// CDS Phonebook WCF Client
// ──────────────────────────────────────────────────────────

public sealed class CdsPhonebookClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _accessToken;
    private readonly string _username;
    private ChannelFactory<ICdsUserPhoneBookEnum>? _channelFactory;

    public CdsPhonebookClient(string host, int port, string accessToken, string username)
    {
        _host = host;
        _port = port;
        _accessToken = accessToken;
        _username = username;
    }

    /// <summary>
    /// Build a WCF ChannelFactory matching the CUserPhoneBookEnumImpl binding.
    /// Endpoint: net.tcp://{host}:{port}/ConfigDataStore/CUserPhoneBookEnumImpl.jwt2
    ///   SecurityMode = Transport
    ///   TcpClientCredentialType = None
    ///   EndpointIdentity = DNS "IpPbx"
    ///   JWT token injected via custom SOAP header.
    /// </summary>
    private ChannelFactory<ICdsUserPhoneBookEnum> GetOrCreateFactory()
    {
        if (_channelFactory != null)
            return _channelFactory;

        var binding = new NetTcpBinding
        {
            MaxReceivedMessageSize = int.MaxValue,
            OpenTimeout = TimeSpan.FromMilliseconds(7500),
            SendTimeout = TimeSpan.FromSeconds(30),
            ReceiveTimeout = TimeSpan.FromSeconds(30),
            CloseTimeout = TimeSpan.FromSeconds(5),
        };
        binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
        binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
        binding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;

        // Same binding as CdsPhoneClientFacade (jwt2 mode)
        binding.Security.Mode = SecurityMode.Transport;
        binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
        binding.Security.Transport.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;

        var endpointUri = new Uri($"net.tcp://{_host}:{_port}/ConfigDataStore/CUserPhoneBookEnumImpl.jwt2");
        var endpointAddress = new EndpointAddress(endpointUri, new DnsEndpointIdentity("IpPbx"));

        _channelFactory = new ChannelFactory<ICdsUserPhoneBookEnum>(binding, endpointAddress);

        // Accept any server certificate (matches CLMgr's custom validator)
        _channelFactory.Credentials.ServiceCertificate.SslCertificateAuthentication =
            new System.ServiceModel.Security.X509ServiceCertificateAuthentication
            {
                CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.Custom,
                RevocationMode = X509RevocationMode.NoCheck,
                CustomCertificateValidator = new AcceptAllCertificateValidator()
            };

        // Inject JWT token as custom SOAP header on every call
        _channelFactory.Endpoint.EndpointBehaviors.Add(new CdsJwtEndpointBehavior(_accessToken, _username));

        return _channelFactory;
    }

    /// <summary>
    /// Get all phonebook entries via getEnumNotForUpdate (simplest method).
    /// Returns TUserPhoneBookEnum.UserPhoneBookEntries[] with m_Name, m_Number, m_UserID per entry.
    /// Falls back to GetPBXPhonebookElementsEx if getEnumNotForUpdate fails.
    /// </summary>
    public CdsPhonebookEntry[] GetAllEntries()
    {
        // Strategy 1: getEnumNotForUpdate — simple, returns name+number+userId
        try
        {
            var entries = GetViaEnumNotForUpdate();
            if (entries.Length > 0)
                return entries;
        }
        catch (Exception ex)
        {
            Utils.Logging.Warn($"CdsPhonebookClient: getEnumNotForUpdate failed: {ex.GetType().Name}: {ex.Message}");
        }

        // Strategy 2: GetPBXPhonebookElementsEx — full data with users, numbers, emails
        try
        {
            var entries = GetViaPhonebookElementsEx();
            if (entries.Length > 0)
                return entries;
        }
        catch (Exception ex)
        {
            Utils.Logging.Warn($"CdsPhonebookClient: GetPBXPhonebookElementsEx failed: {ex.GetType().Name}: {ex.Message}");
        }

        Utils.Logging.Warn("CdsPhonebookClient: All methods failed, returning 0 entries.");
        return Array.Empty<CdsPhonebookEntry>();
    }

    /// <summary>
    /// Strategy 1: getEnumNotForUpdate(TOrderBy[]) → TUserPhoneBookEnum
    /// Simple call, returns UserPhoneBookEntries[] with m_Name, m_Number, m_UserID.
    /// </summary>
    private CdsPhonebookEntry[] GetViaEnumNotForUpdate()
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            // Sort by name ascending (matching SwyxIt!'s default)
            var orderBy = new CdsTOrderBy[] { new CdsTOrderBy { columnName = "m_Name", ascending = true } };
            var result = channel.getEnumNotForUpdate(orderBy);

            var rawEntries = result?.UserPhoneBookEntries;
            if (rawEntries == null || rawEntries.Length == 0)
            {
                Utils.Logging.Info("CdsPhonebookClient: getEnumNotForUpdate returned 0 entries.");
                return Array.Empty<CdsPhonebookEntry>();
            }

            var entries = new List<CdsPhonebookEntry>();
            foreach (var raw in rawEntries)
            {
                if (raw.m_Hide) continue; // skip hidden entries
                if (string.IsNullOrEmpty(raw.m_Name) && string.IsNullOrEmpty(raw.m_Number)) continue;

                entries.Add(new CdsPhonebookEntry
                {
                    EntryId = raw.m_EntryID,
                    Name = raw.m_Name ?? "",
                    Number = raw.m_Number ?? "",
                    Email = "",
                    Description = "",
                    SiteId = 0,
                    UserId = raw.m_UserID
                });
            }

            Utils.Logging.Info($"CdsPhonebookClient: getEnumNotForUpdate returned {entries.Count} entries (from {rawEntries.Length} raw).");
            return entries.ToArray();
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>
    /// Strategy 2: GetPBXPhonebookElementsEx — full data including users, numbers, emails, status.
    /// Merges user data (Name, Email, Description) with internal number data (Number).
    /// </summary>
    private CdsPhonebookEntry[] GetViaPhonebookElementsEx()
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            // Get current user ID for the API call
            int userId;
            try { userId = channel.GetCurrentUserID(); }
            catch { userId = 0; }

            var editableEntries = channel.GetPBXPhonebookElementsEx(
                out var externalNumbers,
                out var internalNumbers,
                out var publicNumbers,
                out var users,
                out var groups,
                out var sites,
                out var userEmails,
                out var deletedEntries,
                out var statusEntries,
                out var richStatusEntries,
                out var serverTimestamp,
                out var serverProcessRef,
                iUserID: userId,
                bAllEntries: true,
                bStatus: false,
                bRichStatus: false,
                lClientTimestamp: 0);

            Utils.Logging.Info($"CdsPhonebookClient: GetPBXPhonebookElementsEx: users={users?.Length ?? 0}, internalNumbers={internalNumbers?.Length ?? 0}, editableEntries={editableEntries?.Length ?? 0}");

            if (users == null || users.Length == 0)
            {
                Utils.Logging.Info("CdsPhonebookClient: GetPBXPhonebookElementsEx returned 0 users.");
                return Array.Empty<CdsPhonebookEntry>();
            }

            // Build a lookup: EntityID → internal number
            var numberByEntityId = new Dictionary<int, string>();
            if (internalNumbers != null)
            {
                foreach (var num in internalNumbers)
                {
                    if (num.m_EntityID != null && !num.m_bHide && !string.IsNullOrEmpty(num.m_strNumber))
                    {
                        numberByEntityId[num.m_EntityID.m_iEntityID] = num.m_strNumber;
                    }
                }
            }

            // Build email lookup: EntityID → email
            var emailByEntityId = new Dictionary<int, string>();
            if (userEmails != null)
            {
                foreach (var email in userEmails)
                {
                    // Email data extends base — EntityID identifies the user
                    // We use ExtensionData or reflection if needed, but typically email is on the user data directly
                }
            }

            var entries = new List<CdsPhonebookEntry>();
            foreach (var user in users)
            {
                if (string.IsNullOrEmpty(user.m_strName)) continue;

                int entityId = user.m_EntityID?.m_iEntityID ?? 0;
                int siteId = user.m_EntityID?.m_iSiteID ?? 0;
                string number = numberByEntityId.GetValueOrDefault(entityId, "");

                entries.Add(new CdsPhonebookEntry
                {
                    EntryId = entityId,
                    Name = user.m_strName ?? "",
                    Number = number,
                    Email = user.m_strEmail ?? "",
                    Description = user.m_strDescription ?? "",
                    SiteId = siteId,
                    UserId = entityId
                });
            }

            Utils.Logging.Info($"CdsPhonebookClient: GetPBXPhonebookElementsEx built {entries.Count} entries from {users.Length} users.");
            return entries.ToArray();
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>
    /// Search phonebook entries by text (client-side filter on cached data).
    /// </summary>
    public CdsPhonebookEntry[] SearchEntries(string searchText)
    {
        var all = GetAllEntries();
        if (all.Length == 0 || string.IsNullOrEmpty(searchText)) return all;

        var lower = searchText.ToLowerInvariant();
        return all.Where(e =>
            (e.Name?.ToLowerInvariant().Contains(lower) == true) ||
            (e.Number?.Contains(lower) == true) ||
            (e.Email?.ToLowerInvariant().Contains(lower) == true) ||
            (e.Description?.ToLowerInvariant().Contains(lower) == true)
        ).ToArray();
    }

    /// <summary>Ping the phonebook service to verify connectivity.</summary>
    public bool Ping()
    {
        try
        {
            var factory = GetOrCreateFactory();
            var channel = factory.CreateChannel();
            try
            {
                channel.Ping();
                return true;
            }
            finally
            {
                CloseChannel(channel);
            }
        }
        catch
        {
            return false;
        }
    }

    private static void CloseChannel(ICdsUserPhoneBookEnum channel)
    {
        try
        {
            if (channel is ICommunicationObject co)
            {
                if (co.State == CommunicationState.Opened)
                    co.Close();
                else if (co.State == CommunicationState.Faulted)
                    co.Abort();
            }
        }
        catch
        {
            if (channel is ICommunicationObject co)
                co.Abort();
        }
    }

    public void Dispose()
    {
        try
        {
            _channelFactory?.Close();
        }
        catch
        {
            _channelFactory?.Abort();
        }
        _channelFactory = null;
    }
}
