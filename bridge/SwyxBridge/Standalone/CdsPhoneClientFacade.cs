// CDS WCF PhoneClientFacade — connects to SwyxWare ConfigDataStore via net.tcp
// Protocol reverse-engineered from IpPbxCDSClientLib.dll decompilation
//
// Endpoint URL: net.tcp://{host}:{port}/ConfigDataStore/CPhoneClientFacadeImpl.jwt2
// The .jwt2 suffix indicates JWT auth mode — token injected via custom SOAP header.

using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace SwyxBridge.Standalone;

// ──────────────────────────────────────────────────────────
// Data Contracts (from decompiled SWConfigDataWSLib / IpPbx.Model.Info)
// ──────────────────────────────────────────────────────────

[DataContract(Name = "TUserSipCredentialsShort", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Transport")]
public class CdsSipCredentials
{
    [DataMember] public string? SipRealm { get; set; }
    [DataMember] public string? SipUserID { get; set; }
    [DataMember] public string? SipUserName { get; set; }
    [DataMember] public int UserID { get; set; }
}

[DataContract(Name = "ServerInfo", Namespace = "http://schemas.datacontract.org/2004/07/IpPbx.Model.Info")]
public class CdsServerInfo
{
    [DataMember] public string? ServerName { get; set; }
    [DataMember] public string? ServerVersion { get; set; }
    [DataMember] public int ServerType { get; set; }
}

// ──────────────────────────────────────────────────────────
// WCF Service Contract (IPhoneClientFacade)
// ──────────────────────────────────────────────────────────

// LoginIdType enum — decompiled from SWConfigDataClientLib
// IpPbxSrv = used for SIP registration
// UaCstaSrv = used for UACSTA server authentication
[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataWSLib.Transport")]
public enum CdsLoginIdType
{
    [EnumMember] IpPbxSrv = 0,
    [EnumMember] UaCstaSrv = 1
}

[ServiceContract(ConfigurationName = "WSPhoneClientFacade.IPhoneClientFacade")]
public interface ICdsPhoneClientFacade
{
    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/Ping", ReplyAction = "http://tempuri.org/IPhoneClientFacade/PingResponse")]
    void Ping();

    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/GetCurrentUserID", ReplyAction = "http://tempuri.org/IPhoneClientFacade/GetCurrentUserIDResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IPhoneClientFacade/GetCurrentUserIDTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    int GetCurrentUserID();

    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/GetSipCredentials", ReplyAction = "http://tempuri.org/IPhoneClientFacade/GetSipCredentialsResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IPhoneClientFacade/GetSipCredentialsTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    CdsSipCredentials GetSipCredentials(int userId);

    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/GetServerInfo", ReplyAction = "http://tempuri.org/IPhoneClientFacade/GetServerInfoResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IPhoneClientFacade/GetServerInfoTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    CdsServerInfo GetServerInfo();

    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/GetUserLoginID", ReplyAction = "http://tempuri.org/IPhoneClientFacade/GetUserLoginIDResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IPhoneClientFacade/GetUserLoginIDTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    Guid GetUserLoginID();

    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/GetUserLoginIDEx", ReplyAction = "http://tempuri.org/IPhoneClientFacade/GetUserLoginIDExResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IPhoneClientFacade/GetUserLoginIDExTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    Guid GetUserLoginIDEx(int UserID);

    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/GetCurrentUserName", ReplyAction = "http://tempuri.org/IPhoneClientFacade/GetCurrentUserNameResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IPhoneClientFacade/GetCurrentUserNameTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    int GetCurrentUserName(out string UserName);

    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/GetCurrentPSK", ReplyAction = "http://tempuri.org/IPhoneClientFacade/GetCurrentPSKResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IPhoneClientFacade/GetCurrentPSKTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    byte[] GetCurrentPSK();

    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/GetCurrentPSKEx", ReplyAction = "http://tempuri.org/IPhoneClientFacade/GetCurrentPSKExResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IPhoneClientFacade/GetCurrentPSKExTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    byte[] GetCurrentPSKEx(int UserID);

    [OperationContract(Action = "http://tempuri.org/IPhoneClientFacade/GetUserLoginIDEx2", ReplyAction = "http://tempuri.org/IPhoneClientFacade/GetUserLoginIDEx2Response")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/IPhoneClientFacade/GetUserLoginIDEx2TExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    Guid GetUserLoginIDEx2(int UserID, CdsLoginIdType type);
}

// ──────────────────────────────────────────────────────────
// JWT Custom SOAP Header — exact match of SwyxWare's SCustomHeader
// Decompiled from IpPbxCDSSharedLib.dll (SWConfigDataSharedLib.WCFUtils)
//
// Header Name:      "IPPBX_HEADER"
// Header Namespace: "LANPHONE.COM"
// Content: XmlSerializer-serialized SCustomHeaderData inside <IPPBX_HEADER Key="...">
// ──────────────────────────────────────────────────────────

/// <summary>
/// Data object matching SWConfigDataSharedLib.WCFUtils.SCustomHeaderData exactly.
/// Serialized via XmlSerializer, so property names = XML element names.
/// </summary>
[System.Xml.Serialization.XmlRoot("SCustomHeaderData")]
public class CdsCustomHeaderData
{
    public string? SelectedIpPbxUserName { get; set; }
    public string? ClientVersion { get; set; }
    public string? AccessToken { get; set; }
    public string? RequestId { get; set; }
}

internal sealed class CdsCustomHeader : MessageHeader
{
    private static readonly System.Xml.Serialization.XmlSerializer _serializer =
        new System.Xml.Serialization.XmlSerializer(typeof(CdsCustomHeaderData));

    private readonly CdsCustomHeaderData _data;

    public CdsCustomHeader(string accessToken, string username)
    {
        _data = new CdsCustomHeaderData
        {
            AccessToken = accessToken,
            SelectedIpPbxUserName = username,
            ClientVersion = "14.25.0.0",
            RequestId = ""
        };
    }

    public override string Name => "IPPBX_HEADER";
    public override string Namespace => "LANPHONE.COM";

    protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
    {
        // Match original: XmlSerializer → string → WriteElementString("IPPBX_HEADER", "Key", serializedXml)
        using var sw = new System.IO.StringWriter();
        _serializer.Serialize(sw, _data);
        var serializedXml = sw.ToString().Trim();
        writer.WriteElementString("IPPBX_HEADER", "Key", serializedXml);
    }
}

// ──────────────────────────────────────────────────────────
// WCF Message Inspector — adds JWT header to every outgoing request
// ──────────────────────────────────────────────────────────

internal sealed class CdsJwtMessageInspector : IClientMessageInspector
{
    private readonly string _accessToken;
    private readonly string _username;

    public CdsJwtMessageInspector(string accessToken, string username)
    {
        _accessToken = accessToken;
        _username = username;
    }

    public object? BeforeSendRequest(ref System.ServiceModel.Channels.Message request, IClientChannel channel)
    {
        request.Headers.Add(new CdsCustomHeader(_accessToken, _username));
        return null;
    }

    public void AfterReceiveReply(ref System.ServiceModel.Channels.Message reply, object? correlationState)
    {
        // Nothing to do on reply
    }
}

// ──────────────────────────────────────────────────────────
// WCF Endpoint Behavior — registers the JWT inspector
// ──────────────────────────────────────────────────────────

internal sealed class CdsJwtEndpointBehavior : IEndpointBehavior
{
    private readonly string _accessToken;
    private readonly string _username;

    public CdsJwtEndpointBehavior(string accessToken, string username)
    {
        _accessToken = accessToken;
        _username = username;
    }

    public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }
    public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher) { }
    public void Validate(ServiceEndpoint endpoint) { }

    public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
    {
        clientRuntime.ClientMessageInspectors.Add(new CdsJwtMessageInspector(_accessToken, _username));
    }
}

// ──────────────────────────────────────────────────────────
// CDS PhoneClientFacade WCF Client
// ──────────────────────────────────────────────────────────

public sealed class CdsPhoneClientFacade : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _accessToken;
    private readonly string _username;
    private ChannelFactory<ICdsPhoneClientFacade>? _channelFactory;

    public CdsPhoneClientFacade(string host, int port, string accessToken, string username)
    {
        _host = host;
        _port = port;
        _accessToken = accessToken;
        _username = username;
    }

    /// <summary>
    /// Build a WCF ChannelFactory matching IpPbxCDSClientLib's PhoneClientFacade binding.
    /// Endpoint: net.tcp://{host}:{port}/ConfigDataStore/CPhoneClientFacadeImpl.jwt2
    ///   SecurityMode = Transport
    ///   TcpClientCredentialType = None
    ///   ProtectionLevel = EncryptAndSign
    ///   EndpointIdentity = DNS "IpPbx"
    ///   JWT token injected via custom SOAP header on every request.
    /// </summary>
    private ChannelFactory<ICdsPhoneClientFacade> GetOrCreateFactory()
    {
        if (_channelFactory != null)
            return _channelFactory;

        var binding = new NetTcpBinding
        {
            MaxReceivedMessageSize = int.MaxValue,
            OpenTimeout = TimeSpan.FromMilliseconds(7500),
            SendTimeout = TimeSpan.FromSeconds(15),
            ReceiveTimeout = TimeSpan.FromSeconds(15),
            CloseTimeout = TimeSpan.FromSeconds(5),
        };
        binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
        binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
        binding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;

        // Match CLMgr's PhoneClientFacade binding (jwt2 suffix = JWT auth mode):
        binding.Security.Mode = SecurityMode.Transport;
        binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
        binding.Security.Transport.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;

        var endpointUri = new Uri($"net.tcp://{_host}:{_port}/ConfigDataStore/CPhoneClientFacadeImpl.jwt2");
        var endpointAddress = new EndpointAddress(endpointUri, new DnsEndpointIdentity("IpPbx"));

        _channelFactory = new ChannelFactory<ICdsPhoneClientFacade>(binding, endpointAddress);

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

    /// <summary>Ping the PhoneClientFacade service.</summary>
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

    /// <summary>Get the current user's ID from the CDS.</summary>
    public int GetCurrentUserID()
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            return channel.GetCurrentUserID();
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>Retrieve SIP credentials for the given userId.</summary>
    public CdsSipCredentials GetSipCredentials(int userId)
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            return channel.GetSipCredentials(userId);
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>Retrieve server info from the CDS.</summary>
    public CdsServerInfo GetServerInfo()
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            return channel.GetServerInfo();
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>Get the user's login session GUID (used for SIP auth).</summary>
    public Guid GetUserLoginID()
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            return channel.GetUserLoginID();
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>Get login session GUID for a specific userId.</summary>
    public Guid GetUserLoginIDEx(int userId)
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            return channel.GetUserLoginIDEx(userId);
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>Get login session GUID with specific type (IpPbxSrv for SIP, UaCstaSrv for UACSTA).</summary>
    public Guid GetUserLoginIDEx2(int userId, CdsLoginIdType type)
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            return channel.GetUserLoginIDEx2(userId, type);
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>Get the current user's name from the CDS session.</summary>
    public (int userId, string userName) GetCurrentUserName()
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            int result = channel.GetCurrentUserName(out string userName);
            return (result, userName);
        }
        finally
        {
            CloseChannel(channel);
        }
    }


    /// <summary>Get Pre-Shared Key (DES-encrypted) and decrypt it.</summary>
    public string GetDecryptedPSK(int userId)
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            byte[] encrypted = channel.GetCurrentPSKEx(userId);
            if (encrypted == null || encrypted.Length == 0) return "";
            return CdsCrypt.DecryptDES(encrypted);
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>Get raw PSK bytes (for diagnostics).</summary>
    public byte[] GetRawPSK(int userId)
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            return channel.GetCurrentPSKEx(userId);
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    private static void CloseChannel(ICdsPhoneClientFacade channel)
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

// ──────────────────────────────────────────────────────────
// DES Decryption — matches SWConfigDataSharedLib.Security.SCrypt
// ──────────────────────────────────────────────────────────

internal static class CdsCrypt
{
    // Hardcoded keys from decompiled SCrypt.cs
    private static readonly byte[] DesKey = new byte[]
    {
        101, 32, 105, 52, 239, 97, 22, 32, 50, 114,
        37, 32, 101, 50, 111, 99, 110, 50, 130, 119,
        87, 94, 111, 32, 57, 145, 101, 117, 131, 134,
        67, 118, 121, 104, 71, 114, 24, 97, 101, 86
    };

    private static readonly int[] DesIndex = new int[]
    {
        20, 12, 1, 5, 9, 38, 23, 33, 26, 37,
        31, 32, 11, 14, 16, 7, 19, 2, 35, 0
    };

    public static string DecryptDES(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length == 0) return "";

        #pragma warning disable SYSLIB0021 // DES is obsolete but required for Swyx compatibility
        using var des = System.Security.Cryptography.DES.Create();
        #pragma warning restore SYSLIB0021

        // Build key from DesKey[DesIndex[j % 20]]
        byte[] key = new byte[des.Key.Length];
        for (int j = 0; j < key.Length; j++)
            key[j] = DesKey[DesIndex[j % DesIndex.Length]];
        des.Key = key;

        // Build IV from DesKey[DesIndex[19 - k % 20]]
        byte[] iv = new byte[des.IV.Length];
        for (int k = 0; k < iv.Length; k++)
            iv[k] = DesKey[DesIndex[DesIndex.Length - 1 - k % DesIndex.Length]];
        des.IV = iv;

        using var ms = new System.IO.MemoryStream(encryptedData, writable: false);
        using var cs = new System.Security.Cryptography.CryptoStream(ms, des.CreateDecryptor(), System.Security.Cryptography.CryptoStreamMode.Read);
        using var sr = new System.IO.StreamReader(cs);
        return sr.ReadLine() ?? "";
    }
}
