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
                int uid = GetCurrentUserId();
                var ngoStatus = _accountRepo.GetNGOAccountStatus(uid);
                if (ngoStatus != null && (!string.Equals(ngoStatus.ApplicationStatus, "Approved", StringComparison.OrdinalIgnoreCase) || !ngoStatus.UserIsActive))
                {
                    TempData["ErrorMessage"] = "Your NGO account is currently awaiting approval or is inactive.";
                    return RedirectToAction("AccessDenied", "Account");
                }

                return RedirectToAction("NGO");
            }

            return RedirectToAction("UserDashboard");
        }

        // ==========================================
        // 1. ADMIN DASHBOARD & APPROVAL ACTIONS
        // ==========================================
        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult Admin()
        {
            var model = _dashboardRepo.GetAdminDashboardData();
            return View(model);
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveDonation(int donationId)
        {
            int adminId = GetCurrentUserId();
            bool success = _dashboardRepo.ApproveDonation(donationId, adminId);

            if (success)
            {
                TempData["SuccessMessage"] = $"Donation #{donationId} has been Approved successfully. Funds and status are updated.";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not approve the donation. Record not found.";
            }

            return RedirectToAction("Admin");
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult DenyDonation(int donationId, string remarks)
        {
            int adminId = GetCurrentUserId();
            bool success = _dashboardRepo.DenyDonation(donationId, adminId, remarks);

            if (success)
            {
                TempData["SuccessMessage"] = $"Donation #{donationId} has been Denied and preserved in history.";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not deny the donation. Record not found.";
            }

            return RedirectToAction("Admin");
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveNGO(int applicationId)
        {
            int adminId = GetCurrentUserId();
            bool success = _dashboardRepo.ApproveNgoApplication(applicationId, adminId);

            if (success)
            {
                TempData["SuccessMessage"] = "NGO Application approved successfully! The NGO account is now Active.";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not approve the application. Please try again.";
            }

            return RedirectToAction("Admin");
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult DenyNGO(int applicationId, string remarks)
        {
            int adminId = GetCurrentUserId();
            bool success = _dashboardRepo.DenyNgoApplication(applicationId, adminId, remarks);

            if (success)
            {
                TempData["SuccessMessage"] = "NGO Application has been denied.";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not deny the application. Please try again.";
            }

            return RedirectToAction("Admin");
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleNGOActive(int applicationId, bool isActive)
        {
            bool success = _dashboardRepo.SetNgoActiveStatus(applicationId, isActive);

            if (success)
            {
                TempData["SuccessMessage"] = $"NGO account status has been updated to {(isActive ? "Active" : "Inactive")}.";
            }
            else
            {
                TempData["ErrorMessage"] = "Could not update the NGO status. Please try again.";
            }

            return RedirectToAction("Admin");
        }

        // ==========================================
        // 2. NGO DASHBOARD
        // ==========================================
        [HttpGet]
        [AuthorizeRoles("NGO", "Admin")]
        public ActionResult NGO()
        {
            int userId = GetCurrentUserId();

            // Verify approval & active status if user is an NGO
            if (!User.IsInRole("Admin"))
            {
                var ngoStatus = _accountRepo.GetNGOAccountStatus(userId);
                if (ngoStatus != null && (!string.Equals(ngoStatus.ApplicationStatus, "Approved", StringComparison.OrdinalIgnoreCase) || !ngoStatus.UserIsActive))
                {
                    TempData["ErrorMessage"] = "Your NGO account is currently awaiting approval or is inactive.";
                    return RedirectToAction("AccessDenied", "Account");
                }
            }

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
        // 4. DONATION DETAILS MODAL (JSON HELPER)
        // ==========================================
        [HttpGet]
        public JsonResult GetDonationDetailsJson(int id)
        {
            var donation = _dashboardRepo.GetDonationById(id);
            if (donation == null)
            {
                return Json(new { success = false, message = "Donation not found." }, JsonRequestBehavior.AllowGet);
            }

            int currentUserId = GetCurrentUserId();

            // Role-based security check
            if (!User.IsInRole("Admin"))
            {
                if (User.IsInRole("NGO"))
                {
                    var ngoStatus = _accountRepo.GetNGOAccountStatus(currentUserId);
                    if (ngoStatus == null || ngoStatus.NGOID != donation.NGOID)
                    {
                        return Json(new { success = false, message = "Unauthorized access to this donation record." }, JsonRequestBehavior.AllowGet);
                    }
                }
                else
                {
                    if (donation.UserID != currentUserId)
                    {
                        return Json(new { success = false, message = "Unauthorized access to this donation record." }, JsonRequestBehavior.AllowGet);
                    }
                }
            }

            return Json(new
            {
                success = true,
                donation = new
                {
                    donation.DonationID,
                    donation.PaymentReference,
                    donation.DonorName,
                    donation.DonorEmail,
                    donation.NGOName,
                    donation.CauseName,
                    donation.ProgramName,
                    AmountFormatted = "PKR " + donation.Amount.ToString("N0"),
                    Amount = donation.Amount,
                    donation.DonationStatus,
                    donation.AdminApprovalStatus,
                    donation.NGOApprovalStatus,
                    donation.PaymentStatus,
                    donation.PaymentMethod,
                    DonationDateFormatted = donation.DonationDate.ToString("MMM dd, yyyy HH:mm"),
                    AdminReviewedAtFormatted = donation.AdminReviewedAt.HasValue ? donation.AdminReviewedAt.Value.ToString("MMM dd, yyyy HH:mm") : "Pending Review",
                    donation.AdminRemarks,
                    donation.Message
                }
            }, JsonRequestBehavior.AllowGet);
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
