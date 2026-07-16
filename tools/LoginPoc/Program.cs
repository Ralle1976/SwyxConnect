// LoginPoc — Proof-of-Concept: SwyxWare Login via IpPbxCDSClientLib.dll
//
// This is the MINIMAL test to prove that our C# bridge can login to SwyxWare
// WITHOUT SwyxIt! running, by calling LibManagerLogin.LoginWithCredentials().
//
// This is exactly what SwyxIt! does internally (proven by decompilation).
// Target: .NET Framework 4.8 (same as SwyxIt!) — WCF is built-in, no package juggling.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace LoginPoc;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== LoginPoc — SwyxWare Login via WCF (.NET Framework 4.8) ===");
        Console.WriteLine();

        // 1. Read credentials from .env (no values logged)
        var (server, user, password) = ReadEnv();
        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("ERROR: Missing credentials. Check .env file.");
            return 1;
        }

        // Allow override via command-line arg (e.g. "LoginPoc.exe 127.0.0.1" or "LoginPoc.exe hostname:port")
        if (args.Length >= 1 && !string.IsNullOrEmpty(args[0]))
        {
            Console.WriteLine($"[override] Using server from CLI: {args[0]}");
            server = args[0];
        }
        // Allow port override via second arg
        if (args.Length >= 2 && !string.IsNullOrEmpty(args[1]))
        {
            Console.WriteLine($"[override] Using port from CLI: {args[1]} (appended to server as 'server:port')");
            server = server + ":" + args[1];
        }
        Console.WriteLine($"Server: {server}");
        Console.WriteLine($"User:   <hidden>");
        Console.WriteLine($"Pass:   <hidden>");
        Console.WriteLine();

        // 2. Load the Swyx DLLs explicitly (so we get meaningful errors if they fail)
        Console.WriteLine("--- Loading Swyx assemblies ---");
        string swyxDir = @"C:\Program Files (x86)\Swyx\SwyxIt!";
        string[] requiredDlls = {
            "IpPbxCDSClientLib.dll",
            "IpPbxCDSSharedLib.dll",
            "IpPbx.Configuration.Model.dll",
            "IpPbx.Web.Model.dll",
            "IpPbxTracing.dll",
            "IpPbxWin32.dll"
        };

        foreach (var dll in requiredDlls)
        {
            try
            {
                var path = Path.Combine(swyxDir, dll);
                Assembly.LoadFrom(path);
                Console.WriteLine($"  loaded: {dll}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAILED: {dll}: {ex.Message}");
                return 2;
            }
        }
        Console.WriteLine();

        // 3. Find the LibManagerLogin type via reflection (avoids compile-time binding issues
        //    with .NET Framework 4.x assemblies referenced from .NET 8)
        Console.WriteLine("--- Locating LibManagerLogin ---");
        Type? libManagerLoginType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("SWConfigDataClientLib.LibManagerLogin");
            if (t != null)
            {
                libManagerLoginType = t;
                Console.WriteLine($"  found in: {asm.GetName().Name}");
                break;
            }
        }
        if (libManagerLoginType == null)
        {
            Console.WriteLine("ERROR: LibManagerLogin type not found in any loaded assembly.");
            return 3;
        }
        Console.WriteLine();

        // 4. Find LoginWithCredentials method
        Console.WriteLine("--- Locating LoginWithCredentials ---");
        var method = libManagerLoginType.GetMethod("LoginWithCredentials",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(string), typeof(string) },
            modifiers: null);

        if (method == null)
        {
            Console.WriteLine("ERROR: LoginWithCredentials(string,string,string) not found.");
            Console.WriteLine("Available static methods:");
            foreach (var m in libManagerLoginType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                Console.WriteLine($"  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
            return 4;
        }
        Console.WriteLine($"  found: {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
        Console.WriteLine();

        // 5. Invoke the login
        Console.WriteLine("--- Calling LoginWithCredentials ---");
        Console.WriteLine("  (This makes a WCF call to net.tcp://<server>:9094/ConfigDataStore/CLoginImpl.none)");
        Console.WriteLine();

        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            object? result = method.Invoke(null, new object[] { server, user, password });
            watch.Stop();
            Console.WriteLine($"  call returned after {watch.ElapsedMilliseconds}ms");
            Console.WriteLine();

            if (result == null)
            {
                Console.WriteLine("ERROR: Result was null.");
                return 5;
            }

            // Inspect the result via reflection (UserLoginResult type)
            var resultType = result.GetType();
            Console.WriteLine($"--- Result Type: {resultType.Name} ---");

            // ResultType property
            var resultTypeProp = resultType.GetProperty("ResultType");
            if (resultTypeProp != null)
            {
                var resultTypeValue = resultTypeProp.GetValue(result);
                Console.WriteLine($"  ResultType: {resultTypeValue}");
            }

            // Try common property names
            foreach (var propName in new[] { "LibManager", "UserName", "AccessToken", "ErrorMessage", "Result", "Success" })
            {
                var prop = resultType.GetProperty(propName);
                if (prop != null)
                {
                    var val = prop.GetValue(result);
                    if (val is string s && (propName.Contains("Token") || propName.Contains("Access")))
                        Console.WriteLine($"  {propName}: <hidden, length={s.Length}>");
                    else
                        Console.WriteLine($"  {propName}: {val}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== ALL PROPERTIES OF RESULT ===");
            foreach (var prop in resultType.GetProperties())
            {
                try
                {
                    var val = prop.GetValue(result);
                    var valStr = val?.ToString() ?? "null";
                    if (prop.Name.Contains("Token") || prop.Name.Contains("Access"))
                        valStr = $"<hidden, length={(val as string)?.Length ?? 0}>";
                    Console.WriteLine($"  {prop.Name} ({prop.PropertyType.Name}): {valStr}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  {prop.Name}: <error: {ex.Message}>");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== END ===");
            return 0;
        }
        catch (TargetInvocationException tie)
        {
            Console.WriteLine($"ERROR during login: {tie.InnerException?.Message ?? tie.Message}");
            Console.WriteLine($"Stack: {tie.InnerException?.StackTrace}");
            return 6;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            return 7;
        }
    }

    /// <summary>
    /// Reads credentials from the .env file. Values are NEVER printed.
    /// </summary>
    private static (string server, string user, string password) ReadEnv()
    {
        // Try multiple locations
        string[] candidates = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SwyxConnect", ".env"),
            ".env",
            Path.Combine(Directory.GetCurrentDirectory(), ".env")
        };

        string envPath = candidates.FirstOrDefault(File.Exists);
        if (envPath == null)
        {
            Console.WriteLine("ERROR: No .env file found in:");
            foreach (var c in candidates) Console.WriteLine($"  {c}");
            return (null, null, null);
        }

        var env = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
            var eq = trimmed.IndexOf('=');
            if (eq < 0) continue;
            env[trimmed.Substring(0, eq).Trim()] = trimmed.Substring(eq + 1).Trim();
        }

        // SWYX_SERVER is the WSBaseUrl — the internal SwyxWare server IP/hostname
        string s = null, u = null, p = null;
        env.TryGetValue("SWYX_SERVER", out s);
        env.TryGetValue("SWYX_USERNAME", out u);
        env.TryGetValue("SWYX_PASSWORD", out p);
        return (s, u, p);
    }
}
