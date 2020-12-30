using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Majorsilence.CrystalCmd.NetFrameworkServer.Controllers
{
    public class StatusController : ApiController
    {
        static string _status = null;

        // GET api/values
        public string Get()
        {
            if (_status == null)
            {
                _status = $"Online - {System.Environment.MachineName} - {GetVersion()}";
            }
            return _status;
        }

        private string GetVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            string version = fvi.FileVersion;
            return version;
        }

    }
}
