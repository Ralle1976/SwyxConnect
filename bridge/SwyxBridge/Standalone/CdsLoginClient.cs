// CDS WCF Login Client — connects to SwyxWare ConfigDataStore via net.tcp
// Protocol reverse-engineered from IpPbxCDSClientLib.dll decompilation
//
// Login endpoint: net.tcp://{host}:9094/ConfigDataStore/CLoginImpl.none
// Auth flow: AcquireToken(Credentials{UserName,Password}) → AuthenticationResult{AccessToken,RefreshToken,UserId}

using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.IdentityModel.Selectors;

namespace SwyxBridge.Standalone;

// ──────────────────────────────────────────────────────────
// WCF Data Contracts (from decompiled IpPbxCDSClientLib.dll)
// ──────────────────────────────────────────────────────────

[DataContract(Name = "Credentials", Namespace = "http://schemas.datacontract.org/2004/07/IpPbx.Web.Model")]
public class CdsCredentials
{
    [DataMember]
    public string? UserName { get; set; }

    [DataMember]
    public string? Password { get; set; }
}

[DataContract(Name = "AuthenticationResult", Namespace = "http://schemas.datacontract.org/2004/07/IpPbx.Web.Model")]
public class CdsAuthenticationResult
{
    [DataMember]
    public string? AccessToken { get; set; }

    [DataMember]
    public string? RefreshToken { get; set; }

    [DataMember]
    public int UserId { get; set; }
}

[DataContract(Name = "UserCredentialsAuthenticationResult", Namespace = "http://schemas.datacontract.org/2004/07/IpPbx.Web.Model")]
public class CdsUserCredentialsAuthenticationResult
{
    [DataMember]
    public string? AccessToken { get; set; }

    [DataMember]
    public string? RefreshToken { get; set; }

    [DataMember]
    public int UserId { get; set; }

    [DataMember]
    public int ResultType { get; set; }
}

[DataContract(Name = "FirstFactorAuthenticationResult", Namespace = "http://schemas.datacontract.org/2004/07/IpPbx.Web.Model")]
public class CdsFirstFactorAuthenticationResult
{
    [DataMember]
    public string? AccessToken { get; set; }

    [DataMember]
    public int UserId { get; set; }

    [DataMember]
    public string? SharedKey { get; set; }
}

[DataContract(Name = "TwoFactorCredentials", Namespace = "http://schemas.datacontract.org/2004/07/IpPbx.Web.Model")]
public class CdsTwoFactorCredentials
{
    [DataMember]
    public string? FirstFactorAccessToken { get; set; }

    [DataMember]
    public string? SecondFactor { get; set; }
}

[DataContract(Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
public class CdsTException
{
    [DataMember]
    public string? Message { get; set; }

    [DataMember]
    public int ErrorCode { get; set; }
}

// ──────────────────────────────────────────────────────────
// WCF Service Contract (ILogin)
// ──────────────────────────────────────────────────────────

[ServiceContract(ConfigurationName = "WSLogin.ILogin")]
public interface ICdsLogin
{
    [OperationContract(Action = "http://tempuri.org/ILogin/Ping", ReplyAction = "http://tempuri.org/ILogin/PingResponse")]
    void Ping();

    [OperationContract(Action = "http://tempuri.org/ILogin/GetSupportedClientVersions", ReplyAction = "http://tempuri.org/ILogin/GetSupportedClientVersionsResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/ILogin/GetSupportedClientVersionsTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    string[] GetSupportedClientVersions();

    [OperationContract(Action = "http://tempuri.org/ILogin/CheckVersion", ReplyAction = "http://tempuri.org/ILogin/CheckVersionResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/ILogin/CheckVersionTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    void CheckVersion();

    [OperationContract(Action = "http://tempuri.org/ILogin/Login", ReplyAction = "http://tempuri.org/ILogin/LoginResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/ILogin/LoginTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    CdsUserCredentialsAuthenticationResult Login(CdsCredentials credentials);

    [OperationContract(Action = "http://tempuri.org/ILogin/AcquireToken", ReplyAction = "http://tempuri.org/ILogin/AcquireTokenResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/ILogin/AcquireTokenTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    CdsAuthenticationResult AcquireToken(CdsCredentials credentials);

    [OperationContract(Action = "http://tempuri.org/ILogin/AcquireFirstFactorToken", ReplyAction = "http://tempuri.org/ILogin/AcquireFirstFactorTokenResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/ILogin/AcquireFirstFactorTokenTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    CdsFirstFactorAuthenticationResult AcquireFirstFactorToken(CdsCredentials credentials);

    [OperationContract(Action = "http://tempuri.org/ILogin/AcquireTokenByTwoFactors", ReplyAction = "http://tempuri.org/ILogin/AcquireTokenByTwoFactorsResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/ILogin/AcquireTokenByTwoFactorsTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    CdsAuthenticationResult AcquireTokenByTwoFactors(CdsTwoFactorCredentials credentials);

    [OperationContract(Action = "http://tempuri.org/ILogin/RefreshToken", ReplyAction = "http://tempuri.org/ILogin/RefreshTokenResponse")]
    [FaultContract(typeof(CdsTException), Action = "http://tempuri.org/ILogin/RefreshTokenTExceptionFault", Name = "TException", Namespace = "http://schemas.datacontract.org/2004/07/SWConfigDataSharedLib.Exceptions")]
    CdsAuthenticationResult RefreshToken(string token);
}

// ──────────────────────────────────────────────────────────
// CDS Login Client
// ──────────────────────────────────────────────────────────

public sealed class CdsLoginClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private ChannelFactory<ICdsLogin>? _channelFactory;

    public CdsLoginClient(string host = "127.0.0.1", int port = 9094)
    {
        _host = host;
        _port = port;
    }

    /// <summary>
    /// Build a WCF ChannelFactory matching the exact binding from CLMgr/IpPbxCDSClientLib.
    /// Login uses: net.tcp://{host}:{port}/ConfigDataStore/CLoginImpl.none
    ///   SecurityMode = Transport
    ///   TcpClientCredentialType = None
    ///   ProtectionLevel = EncryptAndSign
    ///   EndpointIdentity = DNS "IpPbx"
    /// </summary>
    private ChannelFactory<ICdsLogin> GetOrCreateFactory()
    {
        if (_channelFactory != null)
            return _channelFactory;

        var binding = new NetTcpBinding
        {
            MaxReceivedMessageSize = int.MaxValue,
            OpenTimeout = TimeSpan.FromMilliseconds(7500),
        };
        binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
        binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
        binding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;

        // Match CLMgr's login binding exactly:
        // SecurityMode.Transport (enum value 1)
        binding.Security.Mode = SecurityMode.Transport;
        // TcpClientCredentialType.None (enum value 0)
        binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
        binding.Security.Transport.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;

        var endpointUri = new Uri($"net.tcp://{_host}:{_port}/ConfigDataStore/CLoginImpl.none");
        var endpointAddress = new EndpointAddress(endpointUri, new DnsEndpointIdentity("IpPbx"));

        _channelFactory = new ChannelFactory<ICdsLogin>(binding, endpointAddress);

        // Accept any server certificate (matches CLMgr's custom validator that accepts all)
        _channelFactory.Credentials.ServiceCertificate.SslCertificateAuthentication =
            new System.ServiceModel.Security.X509ServiceCertificateAuthentication
            {
                CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.Custom,
                RevocationMode = X509RevocationMode.NoCheck,
                CustomCertificateValidator = new AcceptAllCertificateValidator()
            };

        return _channelFactory;
    }

    /// <summary>Ping the CDS login service to check connectivity.</summary>
    public CdsProbeResult Ping()
    {
        var result = new CdsProbeResult { Host = _host, Port = _port };
        try
        {
            var factory = GetOrCreateFactory();
            var channel = factory.CreateChannel();
            try
            {
                channel.Ping();
                result.PingOk = true;
            }
            finally
            {
                CloseChannel(channel);
            }
        }
        catch (Exception ex)
        {
            result.PingError = $"{ex.GetType().Name}: {ex.Message}";
        }
        return result;
    }

    /// <summary>Get supported client versions from the CDS.</summary>
    public string[]? GetSupportedVersions()
    {
        var factory = GetOrCreateFactory();
        var channel = factory.CreateChannel();
        try
        {
            return channel.GetSupportedClientVersions();
        }
        finally
        {
            CloseChannel(channel);
        }
    }

    /// <summary>Acquire a JWT token using username/password credentials.</summary>
    public CdsLoginResult AcquireToken(string username, string password)
    {
        var result = new CdsLoginResult { Host = _host, Port = _port, Username = username };
        try
        {
            var factory = GetOrCreateFactory();
            var channel = factory.CreateChannel();
            try
            {
                var authResult = channel.AcquireToken(new CdsCredentials
                {
                    UserName = username,
                    Password = password
                });

                if (authResult != null)
                {
                    result.Success = true;
                    result.AccessToken = authResult.AccessToken;
                    result.RefreshToken = authResult.RefreshToken;
                    result.UserId = authResult.UserId;
                }
                else
                {
                    result.Error = "AcquireToken returned null";
                }
            }
            finally
            {
                CloseChannel(channel);
            }
        }
        catch (FaultException<CdsTException> fex)
        {
            result.Error = $"CDS Fault: {fex.Detail?.Message ?? fex.Message} (Code: {fex.Detail?.ErrorCode})";
        }
        catch (Exception ex)
        {
            result.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
        return result;
    }

    /// <summary>Login with username/password, returns more detail (ResultType).</summary>
    public CdsLoginResult LoginWithCredentials(string username, string password)
    {
        var result = new CdsLoginResult { Host = _host, Port = _port, Username = username };
        try
        {
            var factory = GetOrCreateFactory();
            var channel = factory.CreateChannel();
            try
            {
                var authResult = channel.Login(new CdsCredentials
                {
                    UserName = username,
                    Password = password
                });

                if (authResult != null)
                {
                    result.Success = authResult.AccessToken != null;
                    result.AccessToken = authResult.AccessToken;
                    result.RefreshToken = authResult.RefreshToken;
                    result.UserId = authResult.UserId;
                    result.ResultType = authResult.ResultType;
                }
                else
                {
                    result.Error = "Login returned null";
                }
            }
            finally
            {
                CloseChannel(channel);
            }
        }
        catch (FaultException<CdsTException> fex)
        {
            result.Error = $"CDS Fault: {fex.Detail?.Message ?? fex.Message} (Code: {fex.Detail?.ErrorCode})";
        }
        catch (Exception ex)
        {
            result.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
        return result;
    }

    /// <summary>Refresh an existing JWT token.</summary>
    public CdsLoginResult RefreshToken(string refreshToken)
    {
        var result = new CdsLoginResult { Host = _host, Port = _port };
        try
        {
            var factory = GetOrCreateFactory();
            var channel = factory.CreateChannel();
            try
            {
                var authResult = channel.RefreshToken(refreshToken);
                if (authResult != null)
                {
                    result.Success = true;
                    result.AccessToken = authResult.AccessToken;
                    result.RefreshToken = authResult.RefreshToken;
                    result.UserId = authResult.UserId;
                }
                else
                {
                    result.Error = "RefreshToken returned null";
                }
            }
            finally
            {
                CloseChannel(channel);
            }
        }
        catch (Exception ex)
        {
            result.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
        return result;
    }

    /// <summary>Run a comprehensive probe: Ping + GetVersions + AcquireToken.</summary>
    public CdsProbeResult FullProbe(string? username = null, string? password = null)
    {
        var result = new CdsProbeResult { Host = _host, Port = _port };

        // Step 1: Ping
        try
        {
            var factory = GetOrCreateFactory();
            var channel = factory.CreateChannel();
            try
            {
                channel.Ping();
                result.PingOk = true;
            }
            finally
            {
                CloseChannel(channel);
            }
        }
        catch (Exception ex)
        {
            result.PingError = $"{ex.GetType().Name}: {ex.Message}";
        }

        // Step 2: Supported versions
        try
        {
            var factory = GetOrCreateFactory();
            var channel = factory.CreateChannel();
            try
            {
                result.SupportedVersions = channel.GetSupportedClientVersions();
            }
            finally
            {
                CloseChannel(channel);
            }
        }
        catch (Exception ex)
        {
            result.VersionError = $"{ex.GetType().Name}: {ex.Message}";
        }

        // Step 3: Login (if credentials provided)
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var loginResult = AcquireToken(username, password);
            result.LoginResult = loginResult;
        }

        return result;
    }

    private static void CloseChannel(ICdsLogin channel)
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
// Result types
// ──────────────────────────────────────────────────────────

public class CdsLoginResult
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Username { get; set; }
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int UserId { get; set; }
    public int ResultType { get; set; }
    public string? Error { get; set; }
}

public class CdsProbeResult
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public bool PingOk { get; set; }
    public string? PingError { get; set; }
    public string[]? SupportedVersions { get; set; }
    public string? VersionError { get; set; }
    public CdsLoginResult? LoginResult { get; set; }
}

// ──────────────────────────────────────────────────────────
// Certificate validator that accepts all certs (matches CLMgr)
// ──────────────────────────────────────────────────────────

public class AcceptAllCertificateValidator : System.IdentityModel.Selectors.X509CertificateValidator
{
    public override void Validate(X509Certificate2 certificate)
    {
        // Accept all certificates — matches CLMgr's SCertificateManager.CertificateValidator
    }
}
