using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace GiveAID_Project.Models
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class AuthorizeRolesAttribute : AuthorizeAttribute
    {
        public AuthorizeRolesAttribute(params string[] roles)
        {
            Roles = string.Join(",", roles);
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext == null)
                return false;

            if (!httpContext.User.Identity.IsAuthenticated)
                return false;

            if (string.IsNullOrEmpty(Roles))
                return true;

            var rolesAllowed = Roles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(r => r.Trim())
                                    .ToList();

            // Check HttpContext.User.IsInRole
            if (rolesAllowed.Any(r => httpContext.User.IsInRole(r)))
                return true;

            // Also check Session["UserRole"] or Session["UserRoles"] as fallback
            var sessionRole = httpContext.Session?["UserRole"] as string;
            if (!string.IsNullOrEmpty(sessionRole) && rolesAllowed.Any(r => string.Equals(r, sessionRole, StringComparison.OrdinalIgnoreCase)))
                return true;

            var sessionRoles = httpContext.Session?["UserRoles"] as System.Collections.Generic.List<string>;
            if (sessionRoles != null && sessionRoles.Any(sr => rolesAllowed.Any(ar => string.Equals(ar, sr, StringComparison.OrdinalIgnoreCase))))
                return true;

            return false;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                // Not logged in -> redirect to login with returnUrl
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Account" },
                        { "action", "Login" },
                        { "returnUrl", filterContext.HttpContext.Request.RawUrl }
                    });
            }
            else
            {
                // Logged in but insufficient permissions -> redirect to Access Denied
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Account" },
                        { "action", "AccessDenied" }
                    });
            }
        }
    }
}
