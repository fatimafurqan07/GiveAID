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
        public AuthorizeRolesAttribute(params string[] roles) { Roles = string.Join(",", roles ?? new string[0]); }
        protected override bool AuthorizeCore(HttpContextBase context)
        {
            if (context == null || context.User == null || !context.User.Identity.IsAuthenticated) return false;
            if (string.IsNullOrWhiteSpace(Roles)) return true;
            return Roles.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Any(context.User.IsInRole);
        }
        protected override void HandleUnauthorizedRequest(AuthorizationContext filter)
        {
            filter.Result = !filter.HttpContext.User.Identity.IsAuthenticated
                ? (ActionResult)new RedirectToRouteResult(new RouteValueDictionary { { "controller", "Account" }, { "action", "Login" }, { "returnUrl", filter.HttpContext.Request.RawUrl } })
                : new RedirectToRouteResult(new RouteValueDictionary { { "controller", "Account" }, { "action", "AccessDenied" } });
        }
    }
}