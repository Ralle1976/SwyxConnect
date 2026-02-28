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
