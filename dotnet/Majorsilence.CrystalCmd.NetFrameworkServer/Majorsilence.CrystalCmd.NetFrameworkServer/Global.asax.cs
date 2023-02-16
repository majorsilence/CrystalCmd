using Majorsilence.CrystalCmd.Server.Common;
using NSwag.AspNet.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Majorsilence.CrystalCmd.NetFrameworkServer
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            string workingfolder = WorkingFolder.GetMajorsilenceTempFolder();
            if (System.IO.Directory.Exists(workingfolder))
            {
                System.IO.Directory.Delete(workingfolder, true);
            }
            System.IO.Directory.CreateDirectory(workingfolder);

            RouteTable.Routes.MapOwinPath("swagger", app =>
            {
                app.UseSwaggerUi3(typeof(WebApiApplication).Assembly, settings =>
                {
                    settings.MiddlewareBasePath = "/swagger";
                    //settings.GeneratorSettings.DefaultUrlTemplate = "api/{controller}/{id}";  //this is the default one
                    settings.GeneratorSettings.DefaultUrlTemplate = "api/{controller}/{action}/{id}";
                });
            });

            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
