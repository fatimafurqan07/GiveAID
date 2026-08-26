
using GiveAID_Project.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace GiveAID_Project.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly DashboardRepository _dashboardRepo;
        private readonly AccountRepository _accountRepo;
        private readonly ContactRepository _contactRepo;

        public DashboardController()
        {
            _dashboardRepo = new DashboardRepository();
            _accountRepo = new AccountRepository();
            _contactRepo = new ContactRepository();
        }

        public DashboardController(DashboardRepository dashboardRepo, AccountRepository accountRepo)
        {
            _dashboardRepo = dashboardRepo;
            _accountRepo = accountRepo;
            _contactRepo = new ContactRepository();
        }

        public DashboardController(
            DashboardRepository dashboardRepo,
            AccountRepository accountRepo,
            ContactRepository contactRepo)
        {
            if (dashboardRepo == null)
            {
                throw new ArgumentNullException("dashboardRepo");
            }

            if (accountRepo == null)
            {
                throw new ArgumentNullException("accountRepo");
            }

            if (contactRepo == null)
            {
                throw new ArgumentNullException("contactRepo");
            }

            _dashboardRepo = dashboardRepo;
            _accountRepo = accountRepo;
            _contactRepo = contactRepo;
        }

        [HttpGet]
        public ActionResult Index()
        {
            bool isAdmin =
                User.IsInRole("Admin") ||
                string.Equals(
                    Convert.ToString(Session["UserRole"]),
                    "Admin",
                    StringComparison.OrdinalIgnoreCase);

            Session["UserRole"] = isAdmin ? "Admin" : "User";

            return isAdmin
                ? RedirectToAction("Admin")
                : RedirectToAction("UserDashboard");
        }

        [HttpGet]
        [ActionName("User")]
        [AuthorizeRoles("User", "Admin")]
        public ActionResult UserAlias()
        {
            return RedirectToAction("Index");
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult Admin()
        {
            var model = _dashboardRepo.GetAdminDashboardData();
            return View(model);
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult AdminDonations(string search = "", string status = "all")
        {
            var model = _dashboardRepo.GetAdminDonations(search, status);
            return View(model);
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewDonation(
            AdminDonationDecisionViewModel model,
            string search = "",
            string status = "all")
        {
            int adminId = GetCurrentUserId();

            if (adminId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                string validationMessage = string.Join(
                    " ",
                    ModelState.Values
                        .SelectMany(value => value.Errors)
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "Please check the donation review information."
                            : error.ErrorMessage));

                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(validationMessage)
                    ? "Please check the donation review information."
                    : validationMessage;

                return RedirectToAction("AdminDonations", new { search, status });
            }

            string message;
            bool success = _dashboardRepo.ReviewDonation(model, adminId, out message);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;

            return RedirectToAction("AdminDonations", new { search, status });
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveDonation(int donationId)
        {
            int adminId = GetCurrentUserId();
            bool success = _dashboardRepo.ApproveDonation(donationId, adminId);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? $"Donation #{donationId} has been approved."
                : "The donation could not be approved.";

            return RedirectToAction("Admin");
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult DenyDonation(int donationId, string remarks)
        {
            int adminId = GetCurrentUserId();
            bool success = _dashboardRepo.DenyDonation(donationId, adminId, remarks);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? $"Donation #{donationId} has been denied."
                : "The donation could not be denied.";

            return RedirectToAction("Admin");
        }

        [HttpGet]
        [AuthorizeRoles("User", "Admin")]
        public ActionResult UserDashboard()
        {
            bool isAdmin =
                User.IsInRole("Admin") ||
                string.Equals(
                    Convert.ToString(Session["UserRole"]),
                    "Admin",
                    StringComparison.OrdinalIgnoreCase);

            if (isAdmin)
            {
                Session["UserRole"] = "Admin";
                return RedirectToAction("Admin");
            }

            int userId = GetCurrentUserId();

            if (userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = _dashboardRepo.GetUserDashboardData(userId);
            return View("User", model);
        }

        [HttpGet]
        [AuthorizeRoles("User", "Admin")]
        public new ActionResult Profile()
        {
            int userId = GetCurrentUserId();

            if (userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = _dashboardRepo.GetUserProfile(userId);

            if (model == null)
            {
                TempData["ErrorMessage"] = "Your profile could not be loaded.";
                return RedirectToAction("UserDashboard");
            }

            return View(model);
        }

        [HttpPost]
        [AuthorizeRoles("User", "Admin")]
        [ValidateAntiForgeryToken]
        public new ActionResult Profile(UserProfileViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            if (currentUserId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var currentProfile = _dashboardRepo.GetUserProfile(currentUserId);

            if (currentProfile == null)
            {
                TempData["ErrorMessage"] = "Your profile could not be loaded.";
                return RedirectToAction("UserDashboard");
            }

            // Identity values always come from the authenticated session/database,
            // never from editable or tampered form values.
            model.UserID = currentUserId;
            model.Email = currentProfile.Email;
            model.MemberSince = currentProfile.MemberSince;
            model.LastLoginAt = currentProfile.LastLoginAt;

            ModelState.Remove("UserID");
            ModelState.Remove("Email");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            bool updated = _dashboardRepo.UpdateUserProfile(model);

            if (!updated)
            {
                ModelState.AddModelError("", "Your profile could not be updated. Please try again.");
                return View(model);
            }

            Session["UserName"] = model.FullName.Trim();
            Session["UserEmail"] = currentProfile.Email;
            TempData["SuccessMessage"] = "Your profile has been updated successfully.";

            return RedirectToAction("Profile");
        }

        [HttpGet]
        [AuthorizeRoles("User", "Admin")]
        public ActionResult Donations(string search = "", string status = "all")
        {
            int userId = GetCurrentUserId();

            if (userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = _dashboardRepo.GetUserDonations(userId, search, status);
            return View(model);
        }

        [HttpGet]
        [AuthorizeRoles("User", "Admin")]
        public ActionResult Interests()
        {
            int userId = GetCurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");
            return View(_dashboardRepo.GetUserInterests(userId));
        }

        [HttpPost]
        [AuthorizeRoles("User", "Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult SaveInterest(int programId, string returnUrl = null)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");

            bool saved = _dashboardRepo.SaveProgramInterest(userId, programId);
            TempData[saved ? "SuccessMessage" : "ErrorMessage"] = saved
                ? "Programme added to My Interests."
                : "This programme could not be saved.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Interests");
        }

        [HttpPost]
        [AuthorizeRoles("User", "Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveInterest(int programId)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");

            bool removed = _dashboardRepo.RemoveProgramInterest(userId, programId);
            TempData[removed ? "SuccessMessage" : "ErrorMessage"] = removed
                ? "Programme removed from My Interests."
                : "The programme was not present in your interests.";
            return RedirectToAction("Interests");
        }

        /* =====================================================
           USER DASHBOARD - MY CONTACT MESSAGES
           ===================================================== */

        [HttpGet]
        [AuthorizeRoles("User", "Admin")]
        public ActionResult Messages(string search = "", string status = "all")
        {
            int userId = GetCurrentUserId();

            if (userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = _contactRepo.GetUserMessages(userId, search, status);
            return View(model);
        }

        /* =====================================================
           USER DASHBOARD - CONTACT MESSAGE DETAILS
           ===================================================== */

        [HttpGet]
        [AuthorizeRoles("User", "Admin")]
        public ActionResult MessageDetails(int? id)
        {
            int userId = GetCurrentUserId();

            if (userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid contact message.";

                return RedirectToAction("Messages");
            }

            // UserID is included in the repository WHERE clause. This prevents
            // one user from opening another user's message by changing the URL.
            var model = _contactRepo.GetUserMessageById(id.Value, userId);

            if (model == null)
            {
                TempData["ErrorMessage"] =
                    "The requested message was not found or does not belong to your account.";

                return RedirectToAction("Messages");
            }

            return View(model);
        }

        [HttpGet]
        [AuthorizeRoles("User", "Admin")]
        public ActionResult DonationDetails(int? id)
        {
            int userId = GetCurrentUserId();

            if (userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid donation record.";

                return RedirectToAction("Donations");
            }

            var donation =
                _dashboardRepo.GetDonationById(id.Value);

            if (donation == null)
            {
                TempData["ErrorMessage"] =
                    "The requested donation record was not found.";

                return RedirectToAction("Donations");
            }

            bool isAdmin =
                string.Equals(
                    Convert.ToString(Session["UserRole"]),
                    "Admin",
                    StringComparison.OrdinalIgnoreCase)
                || User.IsInRole("Admin");

            if (!isAdmin && donation.UserID != userId)
            {
                return RedirectToAction(
                    "AccessDenied",
                    "Account"
                );
            }

            return View("DonationDetails", donation);
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult Users(string search = "", string status = "all")
        {
            var model = _dashboardRepo.GetAdminUsers(search, status);
            return View(model);
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleUserStatus(
            int userId,
            bool? makeActive,
            string search = "",
            string status = "all")
        {
            // A nullable Boolean prevents MVC from throwing an unhandled
            // parameter-binding exception if an old/cached form omits the value.
            if (!makeActive.HasValue)
            {
                TempData["ErrorMessage"] =
                    "The requested account status was not received. Please try again.";

                return RedirectToAction("Users", new { search, status });
            }

            int currentAdminId = GetCurrentUserId();

            if (currentAdminId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            string message;
            bool success = _dashboardRepo.SetUserActiveStatus(
                userId,
                makeActive.Value,
                currentAdminId,
                out message);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;

            return RedirectToAction("Users", new { search, status });
        }

        [HttpGet]
        [AuthorizeRoles("User", "Admin")]
        public JsonResult GetDonationDetailsJson(int id)
        {
            var donation = _dashboardRepo.GetDonationById(id);
            if (donation == null)
            {
                return Json(new { success = false, message = "Donation not found." }, JsonRequestBehavior.AllowGet);
            }

            int currentUserId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && donation.UserID != currentUserId)
            {
                return Json(new { success = false, message = "You cannot access this donation." }, JsonRequestBehavior.AllowGet);
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
                    donation.Amount,
                    donation.DonationStatus,
                    donation.AdminApprovalStatus,
                    donation.NGOApprovalStatus,
                    donation.PaymentStatus,
                    donation.PaymentMethod,
                    DonationDateFormatted = donation.DonationDate.ToString("MMM dd, yyyy HH:mm"),
                    AdminReviewedAtFormatted = donation.AdminReviewedAt.HasValue
                        ? donation.AdminReviewedAt.Value.ToString("MMM dd, yyyy HH:mm")
                        : "Pending review",
                    donation.AdminRemarks,
                    donation.Message
                }
            }, JsonRequestBehavior.AllowGet);
        }

        private int GetCurrentUserId()
        {
            int id;
            if (Session["UserID"] != null && int.TryParse(Session["UserID"].ToString(), out id))
            {
                return id;
            }

            if (User.Identity.IsAuthenticated)
            {
                var user = _accountRepo.GetUserByEmail(User.Identity.Name);
                if (user != null)
                {
                    bool isAdmin =
                        user.Roles != null &&
                        user.Roles.Any(role =>
                            string.Equals(
                                role,
                                "Admin",
                                StringComparison.OrdinalIgnoreCase));

                    Session["UserID"] = user.UserID;
                    Session["UserName"] = user.FullName;
                    Session["UserEmail"] = user.Email;
                    Session["UserRole"] = isAdmin ? "Admin" : "User";
                    return user.UserID;
                }
            }

            return 0;
        }
    }
}
