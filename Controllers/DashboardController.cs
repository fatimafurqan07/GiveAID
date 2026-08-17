using System;
using System.Linq;
using System.Web.Mvc;
using GiveAID_Project.Models;

namespace GiveAID_Project.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly DashboardRepository _dashboardRepo;
        private readonly AccountRepository _accountRepo;

        public DashboardController()
        {
            _dashboardRepo = new DashboardRepository();
            _accountRepo = new AccountRepository();
        }

        public DashboardController(DashboardRepository dashboardRepo, AccountRepository accountRepo)
        {
            _dashboardRepo = dashboardRepo;
            _accountRepo = accountRepo;
        }

        // ==========================================
        // ROUTING HUB: Role-based redirect
        // ==========================================
        [HttpGet]
        public ActionResult Index()
        {
            string role = Session["UserRole"] as string;

            if (string.IsNullOrEmpty(role))
            {
                if (User.IsInRole("Admin"))
                    role = "Admin";
                else if (User.IsInRole("NGO"))
                    role = "NGO";
                else
                    role = "User";

                Session["UserRole"] = role;
            }

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Admin");
            }
            if (string.Equals(role, "NGO", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("NGO");
            }

            return RedirectToAction("UserDashboard");
        }

        // ==========================================
        // 1. ADMIN DASHBOARD
        // ==========================================
        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult Admin()
        {
            var model = _dashboardRepo.GetAdminDashboardData();
            return View(model);
        }

        // ==========================================
        // 2. NGO DASHBOARD
        // ==========================================
        [HttpGet]
        [AuthorizeRoles("NGO", "Admin")]
        public ActionResult NGO()
        {
            int userId = GetCurrentUserId();
            var model = _dashboardRepo.GetNgoDashboardData(userId);
            return View(model);
        }

        // ==========================================
        // 3. USER DASHBOARD
        // ==========================================
        [HttpGet]
        [AuthorizeRoles("User", "Admin", "NGO")]
        public ActionResult UserDashboard()
        {
            int userId = GetCurrentUserId();
            var model = _dashboardRepo.GetUserDashboardData(userId);
            return View("User", model);
        }

        // ==========================================
        // HELPER: Resolve current user ID
        // ==========================================
        private int GetCurrentUserId()
        {
            if (Session["UserID"] != null && int.TryParse(Session["UserID"].ToString(), out int id))
            {
                return id;
            }

            if (User.Identity.IsAuthenticated)
            {
                var user = _accountRepo.GetUserByEmail(User.Identity.Name);
                if (user != null)
                {
                    Session["UserID"] = user.UserID;
                    Session["UserName"] = user.FullName;
                    Session["UserEmail"] = user.Email;
                    Session["UserRole"] = user.Roles.FirstOrDefault() ?? "User";
                    return user.UserID;
                }
            }

            return 0;
        }
    }
}
