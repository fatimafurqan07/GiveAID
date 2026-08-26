using System;
using System.Linq;
using System.Web.Mvc;
using GiveAID_Project.Models;

namespace GiveAID_Project.Controllers
{
    public class DonationController : Controller
    {
        private readonly DashboardRepository _dashboardRepo;
        private readonly AccountRepository _accountRepo;

        public DonationController()
        {
            _dashboardRepo = new DashboardRepository();
            _accountRepo = new AccountRepository();
        }

        public DonationController(
            DashboardRepository dashboardRepo,
            AccountRepository accountRepo)
        {
            _dashboardRepo = dashboardRepo;
            _accountRepo = accountRepo;
        }

        // ==========================================
        // 1. DONATION PAGE (GET)
        // ==========================================
        [HttpGet]
        public ActionResult Create(
            int? ngoId,
            int? causeId,
            int? programId,
            decimal? amount)
        {
            var ngos = _dashboardRepo.GetActiveNGOs();
            var causes = _dashboardRepo.GetActiveCauses();
            var programs = _dashboardRepo.GetActivePrograms();

            var formModel = new CreateDonationModel
            {
                NGOID = ngoId ?? (ngos.FirstOrDefault()?.ID ?? 0),
                CauseID = causeId ?? (causes.FirstOrDefault()?.ID ?? 0),
                ProgramID = programId,

                Amount = amount.HasValue && amount.Value > 0
                    ? amount.Value
                    : 2500,

                PaymentRail = "Raast / 1Link"
            };

            // Pre-fill signed-in user's information.
            var currentUser = GetCurrentUser();

            if (currentUser != null)
            {
                formModel.DonorName = currentUser.FullName;
                formModel.DonorEmail = currentUser.Email;
            }
            else
            {
                if (Session["UserName"] != null)
                {
                    formModel.DonorName =
                        Session["UserName"].ToString();
                }

                if (Session["UserEmail"] != null)
                {
                    formModel.DonorEmail =
                        Session["UserEmail"].ToString();
                }
            }

            var viewModel = BuildDonationFormViewModel(formModel);

            return View(viewModel);
        }

        // ==========================================
        // 2. DONATION SUBMISSION (POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            [Bind(Prefix = "FormModel")]
            CreateDonationModel model)
        {
            int? currentUserId = GetCurrentUserId();

            // Server-side safety check.
            if (model.Amount < 10)
            {
                ModelState.AddModelError(
                    "FormModel.Amount",
                    "Please enter an amount of at least PKR 10.");
            }

            if (!ModelState.IsValid)
            {
                if (Request.IsAjaxRequest())
                {
                    var errors = ModelState.Values
                        .SelectMany(value => value.Errors)
                        .Select(error =>
                            !string.IsNullOrWhiteSpace(error.ErrorMessage)
                                ? error.ErrorMessage
                                : "Please check the entered information.")
                        .ToList();

                    return Json(new
                    {
                        success = false,
                        message = string.Join(" ", errors)
                    });
                }

                var invalidFormViewModel =
                    BuildDonationFormViewModel(model);

                return View(invalidFormViewModel);
            }

            try
            {
                string paymentReference;

                int donationId = _dashboardRepo.CreateDonation(
                    model,
                    currentUserId,
                    out paymentReference);

                if (donationId <= 0)
                {
                    throw new InvalidOperationException(
                        "The donation record could not be created.");
                }

                TempData["SuccessMessage"] =
                    $"Your donation of PKR {model.Amount:N0} " +
                    $"has been created (Ref: {paymentReference}). " +
                    "It is now pending administrator review.";

                if (Request.IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = true,
                        donationId = donationId,
                        reference = paymentReference,
                        amount = model.Amount,

                        redirectUrl = Url.Action(
                            "Success",
                            "Donation",
                            new { id = donationId })
                    });
                }

                // Donation ID must be included in this redirect.
                return RedirectToAction(
                    "Success",
                    new { id = donationId });
            }
            catch (Exception ex)
            {
                string errorMessage =
                    "The donation could not be created. " +
                    "Please review your information and try again.";

                if (Request.IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = false,
                        message = errorMessage
                    });
                }

                TempData["ErrorMessage"] = errorMessage;

                // Optional for debugging in Visual Studio Output window.
                System.Diagnostics.Debug.WriteLine(
                    "Donation creation error: " + ex);

                var errorFormViewModel =
                    BuildDonationFormViewModel(model);

                return View(errorFormViewModel);
            }
        }

        // ==========================================
        // 3. DONATION SUCCESS / RECEIPT (GET)
        // ==========================================
        [HttpGet]
        public ActionResult Success(int? id)
        {
            // Handles /Donation/Success without an ID.
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] =
                    "No donation record was selected. " +
                    "Please create a donation first.";

                return RedirectToAction("Create");
            }

            try
            {
                var donation =
                    _dashboardRepo.GetDonationById(id.Value);

                if (donation == null)
                {
                    TempData["ErrorMessage"] =
                        "The requested donation record could not be found.";

                    return RedirectToAction("Create");
                }

                return View(donation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Donation receipt error: " + ex);

                TempData["ErrorMessage"] =
                    "The donation receipt could not be loaded.";

                return RedirectToAction("Create");
            }
        }

        // ==========================================
        // 4. DYNAMIC LOOKUP JSON (AJAX)
        // ==========================================
        [HttpGet]
        public JsonResult GetFormData()
        {
            var ngos = _dashboardRepo.GetActiveNGOs();
            var causes = _dashboardRepo.GetActiveCauses();
            var programs = _dashboardRepo.GetActivePrograms();

            return Json(
                new
                {
                    ngos,
                    causes,
                    programs
                },
                JsonRequestBehavior.AllowGet);
        }

        // ==========================================
        // PRIVATE HELPERS
        // ==========================================
        private DonationFormDataViewModel BuildDonationFormViewModel(
            CreateDonationModel formModel)
        {
            return new DonationFormDataViewModel
            {
                FormModel = formModel ?? new CreateDonationModel(),

                NGOs = _dashboardRepo.GetActiveNGOs(),
                Causes = _dashboardRepo.GetActiveCauses(),
                Programs = _dashboardRepo.GetActivePrograms()
            };
        }

        private UserAccount GetCurrentUser()
        {
            if (User.Identity.IsAuthenticated)
            {
                var user =
                    _accountRepo.GetUserByEmail(User.Identity.Name);

                if (user != null)
                {
                    SaveUserInSession(user);
                    return user;
                }
            }

            if (Session["UserEmail"] != null)
            {
                string email =
                    Session["UserEmail"].ToString();

                if (!string.IsNullOrWhiteSpace(email))
                {
                    var user =
                        _accountRepo.GetUserByEmail(email);

                    if (user != null)
                    {
                        SaveUserInSession(user);
                        return user;
                    }
                }
            }

            return null;
        }

        private int? GetCurrentUserId()
        {
            int userId;

            if (Session["UserID"] != null &&
                int.TryParse(
                    Session["UserID"].ToString(),
                    out userId))
            {
                return userId;
            }

            var user = GetCurrentUser();

            return user != null
                ? (int?)user.UserID
                : null;
        }

        private void SaveUserInSession(UserAccount user)
        {
            Session["UserID"] = user.UserID;
            Session["UserName"] = user.FullName;
            Session["UserEmail"] = user.Email;

            if (user.Roles != null)
            {
                Session["UserRole"] =
                    user.Roles.FirstOrDefault() ?? "User";
            }
        }
    }
}