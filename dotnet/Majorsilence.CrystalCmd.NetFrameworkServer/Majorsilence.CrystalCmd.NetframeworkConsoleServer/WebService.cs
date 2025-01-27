using System.Threading.Tasks;
using System.Threading;
using System.ServiceProcess;

namespace Majorsilence.CrystalCmd.NetframeworkConsoleServer
{
    public class WebService : ServiceBase
    {
        private readonly WebServerManager _webServerManager;
        private CancellationTokenSource _cancellationTokenSource;

        public WebService(string serviceName)
        {
            ServiceName = serviceName;
            _webServerManager = new WebServerManager();
        }

        protected override void OnStart(string[] args)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => _webServerManager.StartAsync(_cancellationTokenSource.Token));
        }

        protected override void OnStop()
        {
            _cancellationTokenSource.Cancel();
            _webServerManager.Stop();
        }
    }
}
