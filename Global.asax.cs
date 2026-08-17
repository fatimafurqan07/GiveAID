using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace GiveAID_Project
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var authCookie = Request.Cookies[System.Web.Security.FormsAuthentication.FormsCookieName];
            if (authCookie != null && !string.IsNullOrEmpty(authCookie.Value))
            {
                try
                {
                    var authTicket = System.Web.Security.FormsAuthentication.Decrypt(authCookie.Value);
                    if (authTicket != null && !authTicket.Expired)
                    {
                        var userData = authTicket.UserData;
                        string[] roles = new string[0];

                        if (!string.IsNullOrEmpty(userData))
                        {
                            var parts = userData.Split('|');
                            if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
                            {
                                roles = parts[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            }
                        }

                        var identity = new System.Web.Security.FormsIdentity(authTicket);
                        var principal = new System.Security.Principal.GenericPrincipal(identity, roles);

                        HttpContext.Current.User = principal;
                        System.Threading.Thread.CurrentPrincipal = principal;
                    }
                }
                catch
                {
                    // Invalid or corrupt auth ticket
                }
            }
        }
    }
}
