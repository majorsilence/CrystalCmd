using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Majorsilence.CrystalCmd.NetFrameworkServer.Controllers
{
    public class HealthzController : ApiController
    {
        [HttpGet]
        [Route("healthz")]
        public string Get()
        {
            return "";
        }

        [HttpGet]
        [Route("healthz/live")]
        public string GetLive()
        {
            return "";
        }

        [HttpGet]
        [Route("healthz/ready")]
        public HttpResponseMessage GetReady()
        {
            if (Server.Common.HealthCheckTask.IsHealthy)
            {
                // Return a 200 OK status code
                return Request.CreateResponse(HttpStatusCode.OK, "Ready");
            }

            // Return a 404 Not Found status code
            // return Request.CreateResponse(HttpStatusCode.NotFound, "Not Found");

            // Return a 500 Internal Server Error status code
            return Request.CreateResponse(HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }
}
