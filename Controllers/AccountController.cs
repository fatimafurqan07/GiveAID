using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using GiveAID_Project.Models;

namespace GiveAID_Project.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountRepository _accountRepo;

        public AccountController()
        {
            _accountRepo = new AccountRepository();
        }

        public AccountController(AccountRepository accountRepo)
        {
            _accountRepo = accountRepo;
        }

        // ==========================================
        // REGISTER (NORMAL USER)
        // ==========================================

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Register()
        {
            if (User.Identity.IsAuthenticated || Session["UserID"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new RegisterModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterModel model)
        {
            if (User.Identity.IsAuthenticated || Session["UserID"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                if (_accountRepo.EmailExists(model.Email))
                {
                    ModelState.AddModelError("Email", "An account with this email address already exists. Please log in or use a different email.");
                    return View(model);
                }

                // Create user in database with default role "User"
                var newUser = _accountRepo.CreateUser(model, "User");

                // Sign in user immediately upon successful registration
                EstablishUserSession(newUser, rememberMe: false);

                TempData["SuccessMessage"] = "Account created successfully! Welcome to Give-AID.";
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An unexpected error occurred while creating your account. Please try again. (" + ex.Message + ")");
                return View(model);
            }
        }

        // ==========================================
        // NGO REGISTRATION (APPLICATION FOR APPROVAL)
        // ==========================================

        [HttpGet]
        [AllowAnonymous]
        public ActionResult NGORegister()
        {
            if (User.Identity.IsAuthenticated || Session["UserID"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new NGORegisterModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult NGORegister(NGORegisterModel model)
        {
            if (User.Identity.IsAuthenticated || Session["UserID"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                if (_accountRepo.EmailExists(model.Email))
                {
                    ModelState.AddModelError("Email", "An account with this email address already exists. Please log in or use a different email.");
                    return View(model);
                }

                if (_accountRepo.NGONameExists(model.NGOName))
                {
                    ModelState.AddModelError("NGOName", "An NGO with this name is already registered or has an application on file.");
                    return View(model);
                }

                // Create NGO Application (Inactive pending admin approval)
                _accountRepo.CreateNGOApplication(model);

                TempData["SuccessMessage"] = "Your NGO registration application has been submitted successfully! It is currently awaiting review and approval by the platform administrator.";
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An unexpected error occurred while submitting your NGO application. Please try again. (" + ex.Message + ")");
                return View(model);
            }
        }

        // ==========================================
        // LOGIN
        // ==========================================

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity.IsAuthenticated || Session["UserID"] != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginModel model)
        {
            if (User.Identity.IsAuthenticated || Session["UserID"] != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = _accountRepo.GetUserByEmail(model.Email);

                if (user == null || !PasswordSecurity.VerifyPassword(model.Password, user.PasswordHash))
                {
                    ModelState.AddModelError("", "Invalid email address or password.");
                    return View(model);
                }

                if (user.IsBanned)
                {
                    ModelState.AddModelError("", "Your account has been suspended due to policy violations.");
                    return View(model);
                }

                // Check role-specific constraints for NGOs
                var roles = user.Roles ?? new List<string>();
                if (roles.Contains("NGO"))
                {
                    var ngoStatus = _accountRepo.GetNGOAccountStatus(user.UserID);
                    if (ngoStatus != null)
                    {
                        if (string.Equals(ngoStatus.ApplicationStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                        {
                            ModelState.AddModelError("", "Your NGO application is currently awaiting admin approval.");
                            return View(model);
                        }

                        if (string.Equals(ngoStatus.ApplicationStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ngoStatus.ApplicationStatus, "Denied", StringComparison.OrdinalIgnoreCase))
                        {
                            string reason = !string.IsNullOrWhiteSpace(ngoStatus.AdminRemarks) ? $" (Reason: {ngoStatus.AdminRemarks})" : "";
                            ModelState.AddModelError("", "Your NGO application was not approved." + reason);
                            return View(model);
                        }

                        if (!user.IsActive || !string.Equals(ngoStatus.NGOStatus, "Active", StringComparison.OrdinalIgnoreCase))
                        {
                            ModelState.AddModelError("", "Your NGO account is currently inactive. Please contact the administrator.");
                            return View(model);
                        }
                    }
                }
                else
                {
                    if (!user.IsActive)
                    {
                        ModelState.AddModelError("", "Your account has been deactivated. Please contact support.");
                        return View(model);
                    }
                }

                // Update last login timestamp
                _accountRepo.UpdateLastLogin(user.UserID);

                // Establish Forms Authentication & Session
                EstablishUserSession(user, model.RememberMe);

                TempData["SuccessMessage"] = $"Welcome back, {user.FullName}!";

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An unexpected error occurred during login. Please try again. (" + ex.Message + ")");
                return View(model);
            }
        }

        // ==========================================
        // ACCESS DENIED
        // ==========================================

        [HttpGet]
        [AllowAnonymous]
        public ActionResult AccessDenied()
        {
            return View();
        }

        // ==========================================
        // LOGOUT
        // ==========================================

        [HttpGet]
        public ActionResult Logout()
        {
            PerformSignOut();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogoutPost()
        {
            PerformSignOut();
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // HELPER METHODS
        // ==========================================

        private void EstablishUserSession(UserAccount user, bool rememberMe)
        {
            // Roles list to comma-delimited string
            var roles = user.Roles ?? new List<string> { "User" };
            string primaryRole = roles.FirstOrDefault() ?? "User";
            string rolesJoined = string.Join(",", roles);

            // UserData packed into ticket: UserID|FullName|Roles
            string userData = $"{user.UserID}|{user.FullName}|{rolesJoined}";

            DateTime issueDate = DateTime.Now;
            DateTime expireDate = rememberMe ? issueDate.AddDays(14) : issueDate.AddHours(8);

            var authTicket = new FormsAuthenticationTicket(
                1,
                user.Email,
                issueDate,
                expireDate,
                rememberMe,
                userData,
                FormsAuthentication.FormsCookiePath
            );

            string encryptedTicket = FormsAuthentication.Encrypt(authTicket);

            var authCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath
            };

            if (rememberMe)
            {
                authCookie.Expires = expireDate;
            }

            Response.Cookies.Add(authCookie);

            // Store in Session for convenient access in Razor views
            Session["UserID"] = user.UserID;
            Session["UserName"] = user.FullName;
            Session["UserEmail"] = user.Email;
            Session["UserRole"] = primaryRole;
            Session["UserRoles"] = roles;
        }

        private void PerformSignOut()
        {
            FormsAuthentication.SignOut();

            Session.Clear();
            Session.Abandon();

            // Clear authentication cookie
            if (Request.Cookies[FormsAuthentication.FormsCookieName] != null)
            {
                var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, "")
                {
                    Expires = DateTime.Now.AddYears(-1),
                    HttpOnly = true,
                    Path = FormsAuthentication.FormsCookiePath
                };
                Response.Cookies.Add(cookie);
            }

            // Clear ASP.NET session cookie
            if (Request.Cookies["ASP.NET_SessionId"] != null)
            {
                var sessionCookie = new HttpCookie("ASP.NET_SessionId", "")
                {
                    Expires = DateTime.Now.AddYears(-1),
                    HttpOnly = true
                };
                Response.Cookies.Add(sessionCookie);
            }
        }
    }
}
