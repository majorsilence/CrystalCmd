using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.IdentityModel.Tokens;
using NReco.Logging.File;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Server
{
    internal class Program
    {
        // Values shipped in the sample appsettings.json. The server refuses to start with
        // these in place so a deployment can never accidentally run with known credentials
        // or a publicly known JWT signing key.
        internal const string DefaultUsername = "user";
        internal const string DefaultPassword = "password";
        internal const string PlaceholderJwtKey = "PLACEHOLDER_PLACEHOLDER_PLACEHOLDER_PLACEHOLDER";

        // 100 MB default cap on a single request body to bound memory use. Override with
        // Limits:MaxRequestBodyBytes.
        internal const long DefaultMaxRequestBodyBytes = 104_857_600L;

        static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddSingleton<StartupArgs>(new StartupArgs(args));

            // Fail closed on insecure defaults before anything starts listening.
            ValidateSecurityConfiguration(builder.Configuration);

            var maxRequestBodyBytes = builder.Configuration.GetValue<long?>("Limits:MaxRequestBodyBytes")
                                      ?? DefaultMaxRequestBodyBytes;
            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                // Enable synchronous IO for Kestrel until the compressed stream code,
                // BaseRoute.CompressedStreamInput, supports async
                options.AllowSynchronousIO = true;
                // Bound the request size to mitigate memory-exhaustion DoS from large
                // uploads or decompression bombs (see also BaseRoute decompression cap).
                options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
            });

            // Honour X-Forwarded-* from a trusted reverse proxy so HTTPS detection and
            // client IP logging are correct when TLS is terminated upstream.
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            var queue = WorkQueue.CreateDefault("crystal-reports", builder.Configuration);
            await queue.Migrate();

#if NET8_0_OR_GREATOR_WINDOWS
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "Majorsilence CrystalCMD Http Windows Service";
            });
#endif

            // Configuration and logging
            ConfigureServices(builder.Services, builder.Configuration);

#if NET8_0_OR_GREATOR_WINDOWS
            LoggerProviderOptions.RegisterProviderOptions<
                 EventLogSettings, EventLogLoggerProvider>(builder.Services);
#endif

            // Add controllers
            builder.Services.AddControllers()
            .AddJsonOptions(options =>
             {
                 options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
             });

            builder.Services.AddHostedService<QueueCleanupWorkerService>();

            var app = builder.Build();

            app.UseForwardedHeaders();

            // Optionally enforce TLS. Left off by default so the documented
            // http://localhost dev flow and proxy-terminated deployments keep working;
            // set Security:RequireHttps=true for direct internet exposure.
            if (string.Equals(builder.Configuration["Security:RequireHttps"], "true", StringComparison.OrdinalIgnoreCase))
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            // Enable authentication/authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // Map controllers
            app.MapControllers();

            // Run the web application
            await app.RunAsync();
        }

        static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            string externalLogAssemblyPath = Settings.GetSetting("ExternalLogs:AssemblyFilePath");
            string logFile = Settings.GetSetting("ExternalLogs:LogFile");
            if (!string.IsNullOrWhiteSpace(externalLogAssemblyPath))
            {
                string className = Settings.GetSetting("ExternalLogs:AssemblyClassName");
                string functionName = Settings.GetSetting("ExternalLogs:AssemblyFunctionName");

                // Loading an arbitrary assembly and invoking a method from it is equivalent
                // to remote code execution controlled by configuration/environment. Constrain
                // the path to the application directory so a tampered env var / config cannot
                // point the process at an attacker-supplied DLL elsewhere on disk.
                var baseDir = Path.GetFullPath(AppContext.BaseDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var requestedAssemblyPath = Path.GetFullPath(externalLogAssemblyPath);
                if (!requestedAssemblyPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "ExternalLogs:AssemblyFilePath must reference a file inside the application directory.");
                }

                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

                var assembly = Assembly.LoadFrom(requestedAssemblyPath);
                var externalLogsHelperType = assembly.GetType(className);
                var addLoggerMethod = externalLogsHelperType.GetMethod(functionName, BindingFlags.Static | BindingFlags.Public);

                services.AddLogging(configure =>
                {
                    configure.ClearProviders();

                    addLoggerMethod.Invoke(null, new object[] { configure });

                })
               .Configure<LoggerFilterOptions>(options =>
               {
                   options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Information;
               });
            }
            else if (!string.IsNullOrWhiteSpace(logFile))
            {
                services.AddLogging(configure =>
                {
                    configure.ClearProviders();
                    configure.AddFile(logFile, (options) =>
                    {
                        options.Append = true;
                        options.FileSizeLimitBytes = 5000000;
                        options.MaxRollingFiles = 10;
                    });
                })
               .Configure<LoggerFilterOptions>(options =>
               {
                   options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Information;
               });
            }
            else
            {
                services.AddLogging(configure =>
                {
                    configure.ClearProviders();
                    configure.AddConsole();
                })
                .Configure<LoggerFilterOptions>(options => options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Information);
            }

            services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(s =>
            {
                return s.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("CrystalCmd");
            });

            // Authentication: JWT (only validates with a real key) plus Basic (in-repo handler).
            var jwtKey = configuration["Jwt:Key"];
            // Only honour JWT with a real, sufficiently strong signing key. A missing,
            // placeholder, or too-short (< 256-bit) key would allow trivial token forgery.
            bool jwtUsable = !string.IsNullOrWhiteSpace(jwtKey)
                             && !string.Equals(jwtKey, PlaceholderJwtKey, StringComparison.Ordinal)
                             && Encoding.UTF8.GetByteCount(jwtKey) >= 32;

            // The "Bearer" scheme must ALWAYS be registered: the controllers declare
            // [Authorize(AuthenticationSchemes = "Bearer,Basic")], and authenticating an
            // unregistered scheme makes ASP.NET throw (HTTP 500) on every request. When no
            // strong key is configured we register it with an ephemeral random key so any
            // presented token fails signature validation (clean 401) and the publicly known
            // placeholder key can never be used to forge a token. Basic auth still applies.
            var signingKeyBytes = jwtUsable
                ? Encoding.UTF8.GetBytes(jwtKey)
                : System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

            var authBuilder = services.AddAuthentication();
            authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                if (jwtUsable)
                {
                    var audienceConfig = configuration["Jwt:Audience"] ?? "";
                    var audiences = audienceConfig
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrEmpty(a))
                        .ToArray();
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidAudiences = audiences.Length > 0 ? audiences : null
                    };
                }
                else
                {
                    // JWT effectively disabled: the ephemeral key guarantees no token validates.
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                }
            });

            if (!jwtUsable && !string.IsNullOrWhiteSpace(jwtKey))
            {
                Console.Error.WriteLine(
                    "WARNING: Jwt:Key is the placeholder or shorter than 32 bytes; JWT bearer authentication is disabled " +
                    "(tokens will be rejected). Set a strong, secret Jwt:Key (>= 32 bytes) to enable it.");
            }

            authBuilder.AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", options => { });

            services.AddAuthorization();
        }

        /// <summary>
        /// Refuses to start when known-insecure sample values are still in place.
        /// </summary>
        internal static void ValidateSecurityConfiguration(IConfiguration configuration)
        {
            bool allowDefaults = string.Equals(
                configuration["Security:AllowDefaultCredentials"], "true", StringComparison.OrdinalIgnoreCase);
            if (allowDefaults)
            {
                Console.Error.WriteLine(
                    "WARNING: Security:AllowDefaultCredentials is enabled. Do not use this outside local testing.");
                return;
            }

            var user = configuration["Credentials:Username"];
            var pass = configuration["Credentials:Password"];
            if (string.Equals(user, DefaultUsername, StringComparison.Ordinal) &&
                string.Equals(pass, DefaultPassword, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to start with the default Basic credentials (user/password). " +
                    "Change Credentials:Username and Credentials:Password, or set " +
                    "Security:AllowDefaultCredentials=true for local testing only.");
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, args.Name.Split(',')[0] + ".dll");
            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            return null;
        }

    }
}
