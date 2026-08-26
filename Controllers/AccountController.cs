using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using GiveAID_Project.Models;

namespace GiveAID_Project.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountRepository _accounts;
        public AccountController() { _accounts = new AccountRepository(); }
        public AccountController(AccountRepository accounts) { _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts)); }

        [HttpGet, AllowAnonymous]
        public ActionResult Register() { if (Request.IsAuthenticated) return RedirectToAction("Index", "Dashboard"); return View(new RegisterModel()); }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterModel model)
        {
            if (Request.IsAuthenticated) return RedirectToAction("Index", "Dashboard");
            if (!ModelState.IsValid) return View(model);
            try
            {
                if (_accounts.EmailExists(model.Email)) { ModelState.AddModelError("Email", "An account with this email address already exists."); return View(model); }
                var user = _accounts.CreateUser(model, "User"); EstablishUserSession(user, false);
                TempData["SuccessMessage"] = "Your account was created successfully."; return RedirectToAction("Index", "Dashboard");
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) { ModelState.AddModelError("Email", "An account with this email address already exists."); return View(model); }
            catch (Exception ex) { Trace.TraceError("Registration failed: {0}", ex); ModelState.AddModelError("", "We could not create your account. Please try again."); return View(model); }
        }

        [HttpGet, AllowAnonymous]
        public ActionResult Login(string returnUrl) { if (Request.IsAuthenticated) return RedirectToAction("Index", "Dashboard"); return View(new LoginModel { ReturnUrl = returnUrl }); }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Login(LoginModel model)
        {
            if (Request.IsAuthenticated) return RedirectToAction("Index", "Dashboard");
            if (!ModelState.IsValid) return View(model);
            try
            {
                var user = _accounts.GetUserByEmail(model.Email);
                if (user == null || !PasswordSecurity.VerifyPassword(model.Password, user.PasswordHash)) { ModelState.AddModelError("", "Invalid email address or password."); return View(model); }
                if (!user.IsActive) { ModelState.AddModelError("", "This account is inactive. Please contact the administrator."); return View(model); }
                if (user.Roles == null || !user.Roles.Any()) { ModelState.AddModelError("", "This account has no assigned role. Please contact the administrator."); return View(model); }
                if (PasswordSecurity.NeedsRehash(user.PasswordHash)) _accounts.UpdatePasswordHash(user.UserID, PasswordSecurity.HashPassword(model.Password));
                _accounts.UpdateLastLogin(user.UserID); EstablishUserSession(user, model.RememberMe);
                if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)) return Redirect(model.ReturnUrl);
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex) { Trace.TraceError("Login failed: {0}", ex); ModelState.AddModelError("", "We could not sign you in. Please try again."); return View(model); }
        }

        [HttpGet, AllowAnonymous]
        public ActionResult AccessDenied() { return View(); }

        [HttpGet]
        public ActionResult Logout() { PerformSignOut(); TempData["SuccessMessage"] = "You have been logged out successfully."; return RedirectToAction("Login"); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult LogoutPost() { PerformSignOut(); TempData["SuccessMessage"] = "You have been logged out successfully."; return RedirectToAction("Login"); }

        private void EstablishUserSession(UserAccount user, bool rememberMe)
        {
            var roles = user.Roles ?? new List<string>();
            var primary = roles.Any(role =>
                string.Equals(
                    role,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
                ? "Admin"
                : "User";
            var now = DateTime.Now; var expires = rememberMe ? now.AddDays(14) : now.AddHours(8);
            var userData = "v2|" + user.UserID + "|" + string.Join(",", roles);
            var ticket = new FormsAuthenticationTicket(2, user.Email, now, expires, rememberMe, userData, FormsAuthentication.FormsCookiePath);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket)) { HttpOnly = true, Secure = Request.IsSecureConnection, Path = FormsAuthentication.FormsCookiePath, SameSite = SameSiteMode.Lax };
            if (rememberMe) cookie.Expires = expires; Response.Cookies.Add(cookie);
            Session["UserID"] = user.UserID; Session["UserName"] = user.FullName; Session["UserEmail"] = user.Email; Session["UserRole"] = primary; Session["UserRoles"] = roles;
        }

        private void PerformSignOut() { FormsAuthentication.SignOut(); Session.Clear(); Session.Abandon(); ExpireCookie(FormsAuthentication.FormsCookieName); ExpireCookie("ASP.NET_SessionId"); }
        private void ExpireCookie(string name) { Response.Cookies.Add(new HttpCookie(name, string.Empty) { Expires = DateTime.Now.AddYears(-1), HttpOnly = true, Path = "/", SameSite = SameSiteMode.Lax }); }
    }
}
