using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Majorsilence.CrystalCmd.NetFrameworkServer.Controllers
{
    public class StatusController : ApiController
    {
        // GET api/values
        public string Get()
        {
            return "Online";
        }

    }
}
