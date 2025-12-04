using Majorsilence.CrystalCmd.WorkQueues;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
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
        static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddSingleton<StartupArgs>(new StartupArgs(args));
            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                // Enable synchronous IO for Kestrel until the compressed stream code,
                // BaseRoute.CompressedStreamInput, supports async
                options.AllowSynchronousIO = true;
            });

            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

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
            builder.Services.AddControllers();

            builder.Services.AddHostedService<QueueCleanupWorkerService>();

            var app = builder.Build();

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

                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

                var assembly = Assembly.LoadFrom(externalLogAssemblyPath);
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

            // Authentication: conditionally add JWT (if configured) and always add Basic (in-repo handler)
            var jwtKey = configuration["Jwt:Key"];
            var authBuilder = services.AddAuthentication();
            if (!string.IsNullOrWhiteSpace(jwtKey))
            {
                var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
                authBuilder = authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    // inside the AddJwtBearer options
                    var audienceConfig = configuration["Jwt:Audience"] ?? "";
                    var audiences = audienceConfig
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrEmpty(a))
                        .ToArray();
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidAudiences = audiences.Length > 0 ? audiences : null
                    };
                });
            }

            authBuilder.AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", options => { });

            services.AddAuthorization();
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
