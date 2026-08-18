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

        public DonationController(DashboardRepository dashboardRepo, AccountRepository accountRepo)
        {
            _dashboardRepo = dashboardRepo;
            _accountRepo = accountRepo;
        }

        // ==========================================
        // 1. DONATION PAGE (GET)
        // ==========================================
        [HttpGet]
        public ActionResult Create(int? ngoId, int? causeId, int? programId, decimal? amount)
        {
            var ngos = _dashboardRepo.GetActiveNGOs();
            var causes = _dashboardRepo.GetActiveCauses();
            var programs = _dashboardRepo.GetActivePrograms();

            var formModel = new CreateDonationModel
            {
                NGOID = ngoId ?? (ngos.FirstOrDefault()?.ID ?? 0),
                CauseID = causeId ?? (causes.FirstOrDefault()?.ID ?? 0),
                ProgramID = programId,
                Amount = amount.HasValue && amount.Value > 0 ? amount.Value : 2500,
                PaymentRail = "Raast / 1Link"
            };

            // Pre-fill user details if logged in
            if (User.Identity.IsAuthenticated)
            {
                var user = _accountRepo.GetUserByEmail(User.Identity.Name);
                if (user != null)
                {
                    formModel.DonorName = user.FullName;
                    formModel.DonorEmail = user.Email;
                }
            }
            else if (Session["UserEmail"] != null)
            {
                formModel.DonorEmail = Session["UserEmail"]?.ToString();
                formModel.DonorName = Session["UserName"]?.ToString();
            }

            var viewModel = new DonationFormDataViewModel
            {
                FormModel = formModel,
                NGOs = ngos,
                Causes = causes,
                Programs = programs
            };

            return View(viewModel);
        }

        // ==========================================
        // 2. DONATION SUBMISSION (POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateDonationModel model)
        {
            int? currentUserId = null;
            if (Session["UserID"] != null && int.TryParse(Session["UserID"].ToString(), out int uid))
            {
                currentUserId = uid;
            }
            else if (User.Identity.IsAuthenticated)
            {
                var user = _accountRepo.GetUserByEmail(User.Identity.Name);
                if (user != null)
                {
                    currentUserId = user.UserID;
                }
            }

            if (!ModelState.IsValid)
            {
                if (Request.IsAjaxRequest())
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return Json(new { success = false, message = string.Join(" ", errors) });
                }

                var viewModel = new DonationFormDataViewModel
                {
                    FormModel = model,
                    NGOs = _dashboardRepo.GetActiveNGOs(),
                    Causes = _dashboardRepo.GetActiveCauses(),
                    Programs = _dashboardRepo.GetActivePrograms()
                };
                return View(viewModel);
            }

            try
            {
                int donationId = _dashboardRepo.CreateDonation(model, currentUserId, out string paymentRef);

                TempData["SuccessMessage"] = $"Your donation of PKR {model.Amount:N0} has been created (Ref: {paymentRef})! It is now Pending Admin Approval.";

                if (Request.IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = true,
                        donationId = donationId,
                        reference = paymentRef,
                        amount = model.Amount,
                        redirectUrl = Url.Action("Success", "Donation", new { id = donationId })
                    });
                }

                return RedirectToAction("Success", new { id = donationId });
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "An error occurred while creating your donation: " + ex.Message });
                }

                TempData["ErrorMessage"] = "Could not complete your donation: " + ex.Message;
                var viewModel = new DonationFormDataViewModel
                {
                    FormModel = model,
                    NGOs = _dashboardRepo.GetActiveNGOs(),
                    Causes = _dashboardRepo.GetActiveCauses(),
                    Programs = _dashboardRepo.GetActivePrograms()
                };
                return View(viewModel);
            }
        }

        // ==========================================
        // 3. DONATION SUCCESS / RECEIPT (GET)
        // ==========================================
        [HttpGet]
        public ActionResult Success(int id)
        {
            var donation = _dashboardRepo.GetDonationById(id);
            if (donation == null)
            {
                TempData["ErrorMessage"] = "Donation record not found.";
                return RedirectToAction("Index", "Home");
            }

            return View(donation);
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

            return Json(new { ngos, causes, programs }, JsonRequestBehavior.AllowGet);
        }
    }
}
