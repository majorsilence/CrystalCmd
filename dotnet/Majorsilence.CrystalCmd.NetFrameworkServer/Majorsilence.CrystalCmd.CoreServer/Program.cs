using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Majorsilence.CrystalCmd.Client;
using Majorsilence.CrystalCmd.CoreServer;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/healthz", () => "Healthy");
app.MapPost("/export", async (IFormFileCollection files, IConfiguration config,
    HttpContext context, CancellationToken cancellationToken) =>
{

    if (AuthFailed(config, context))
    {
        var authproblem = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        return authproblem;
    }
    var id = Guid.NewGuid().ToString();
    string workingFolder = Path.Combine(config.GetValue<string>("WorkingFolder"), id);
    string crystalCmdNetFrameworkConsoleExeFolder = config.GetValue<string>("CrystalCmdNetFrameworkConsoleExeFolder");

    foreach (var file in files)
    {
        string name = file.Name.Replace("\"", "");
        if (string.Equals(name, "reportdata", StringComparison.CurrentCultureIgnoreCase))
        {
            string dataPath = Path.Combine(workingFolder, $"{id}.json");

            using (var fstream = new FileStream(dataPath, FileMode.Create))
            {
                file.CopyTo(fstream);
            }
        }
        else
        {
            string reportPath = Path.Combine(workingFolder, $"{id}.rpt");
            using (var fstream = new FileStream(reportPath, FileMode.Create))
            {
                file.CopyTo(fstream);
            }
        }
    }

    var consoleHelper = new ConsoleSubProcess(workingFolder, crystalCmdNetFrameworkConsoleExeFolder);
    await consoleHelper.Run();

    // Scan work directory for the generated pdf created by the ConsoleApplication
    byte[] bytes;
    try
    {
        while (!System.IO.File.Exists(System.IO.Path.Combine(workingFolder, "completed.txt")))
        {
            await Task.Delay(500, cancellationToken);
        }

        bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(workingFolder, "report.pdf"));
    }
    catch (Exception ex)
    {
        var message = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(ex.Message + System.Environment.NewLine + ex.StackTrace)
        };

        return message;
    }
    finally
    {
        try
        {
            System.IO.Directory.Delete(workingFolder, true);
        }
        catch (Exception)
        {
            // TODO: cleanup will happen later
        }
    }

    var result = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(bytes)
    };
    result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
    {
        FileName = "report.pdf"
    };
    result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    return result;

});

static bool AuthFailed(IConfiguration config, HttpContext context)
{
    string user = config.GetValue<string>("Username");
    string password = config.GetValue<string>("Password");
    if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
    {
        // auth required
        var basicAuth = GetUserNameAndPassword(context);
        if (!basicAuth.HasValue)
        {
            //Auth problem
            return true;
        }
        if (!string.Equals(user, basicAuth.Value.UserName, StringComparison.InvariantCultureIgnoreCase) ||
            !string.Equals(password, basicAuth.Value.Password, StringComparison.InvariantCulture))
        {
            // auth problem
            return true;
        }
    }

    return false;
}

static (string UserName, string Password)? GetUserNameAndPassword(HttpContext context)
{
    var auth = GetHeader(context, "Authorization")?.Replace("Basic ", "");
    var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(auth ?? ""));
    if (string.IsNullOrWhiteSpace(credentials))
    {
        return null;
    }

    int separator = credentials.IndexOf(':');
    string name = credentials.Substring(0, separator);
    string password = credentials.Substring(separator + 1);

    return (name, password);
}

static string GetHeader(HttpContext context, string header)
{
    Microsoft.Extensions.Primitives.StringValues result;
    context.Request.Headers.TryGetValue(header, out result);
    return result;
}


app.Run();

