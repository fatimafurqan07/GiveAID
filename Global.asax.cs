using System;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;

namespace GiveAID_Project
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start() { AreaRegistration.RegisterAllAreas(); FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters); RouteConfig.RegisterRoutes(RouteTable.Routes); BundleConfig.RegisterBundles(BundleTable.Bundles); }
        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var cookie = Request.Cookies[FormsAuthentication.FormsCookieName]; if (cookie == null || string.IsNullOrWhiteSpace(cookie.Value)) return;
            try
            {
                var ticket = FormsAuthentication.Decrypt(cookie.Value); if (ticket == null || ticket.Expired) return;
                var roles = ExtractRoles(ticket.UserData); var principal = new GenericPrincipal(new FormsIdentity(ticket), roles);
                Context.User = principal; System.Threading.Thread.CurrentPrincipal = principal;
            }
            catch { FormsAuthentication.SignOut(); }
        }
        private static string[] ExtractRoles(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return new string[0]; var parts = data.Split('|'); string roleData = null;
            if (parts.Length >= 3 && parts[0] == "v2") roleData = parts[2]; else if (parts.Length >= 3) roleData = parts[2];
            return string.IsNullOrWhiteSpace(roleData) ? new string[0] : roleData.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}