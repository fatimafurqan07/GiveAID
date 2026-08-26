
using GiveAID_Project.Models;
using System;
using System.Web.Mvc;

namespace GiveAID_Project.Controllers
{
    public class CausesController : Controller
    {
        private readonly CausesRepository _causesRepo;

        public CausesController()
        {
            _causesRepo = new CausesRepository();
        }

        public CausesController(CausesRepository causesRepo)
        {
            if (causesRepo == null)
            {
                throw new ArgumentNullException("causesRepo");
            }

            _causesRepo = causesRepo;
        }

        /* =========================================================
           PUBLIC CAUSE PAGES
           ========================================================= */

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Index(string search, string category)
        {
            var model = _causesRepo.GetCausesList(search, category);

            if (Request.IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    total = model.TotalCausesCount,
                    causes = model.Causes
                }, JsonRequestBehavior.AllowGet);
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Details(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please specify a valid cause ID.";
                return RedirectToAction("Index");
            }

            var cause = _causesRepo.GetCauseById(id.Value);
            if (cause == null)
            {
                TempData["ErrorMessage"] =
                    "The requested cause was not found or is currently inactive.";
                return RedirectToAction("Index");
            }

            return View(cause);
        }

        /* =========================================================
           ADMIN CAUSE MANAGEMENT
           ========================================================= */

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult AdminCauses(
            string search = "",
            string status = "all",
            string feature = "all")
        {
            var model = _causesRepo.GetAdminCauses(search, status, feature);
            return View("AdminCauses", model);
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult CreateCause()
        {
            var model = new CauseAdminFormViewModel
            {
                IsActive = true,
                IsFeatured = false,
                DisplayOrder = 0
            };

            return View("CauseForm", model);
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCause(CauseAdminFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("CauseForm", model);
            }

            string message;
            if (!_causesRepo.CreateCause(model, out message))
            {
                ModelState.AddModelError(string.Empty, message);
                return View("CauseForm", model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction("AdminCauses");
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult EditCause(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid cause record.";
                return RedirectToAction("AdminCauses");
            }

            var model = _causesRepo.GetCauseForAdmin(id.Value);
            if (model == null)
            {
                TempData["ErrorMessage"] = "The selected cause record was not found.";
                return RedirectToAction("AdminCauses");
            }

            return View("CauseForm", model);
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult EditCause(CauseAdminFormViewModel model)
        {
            if (model == null || model.CauseID <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid cause record.";
                return RedirectToAction("AdminCauses");
            }

            if (!ModelState.IsValid)
            {
                return View("CauseForm", model);
            }

            string message;
            if (!_causesRepo.UpdateCause(model, out message))
            {
                ModelState.AddModelError(string.Empty, message);
                return View("CauseForm", model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction("AdminCauses");
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult SetCauseStatus(
            int causeId,
            bool makeActive,
            string search = "",
            string status = "all",
            string feature = "all")
        {
            string message;
            var success = _causesRepo.SetCauseActiveStatus(
                causeId,
                makeActive,
                out message);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;

            return RedirectToAction("AdminCauses", new
            {
                search,
                status,
                feature
            });
        }

        /* =========================================================
           ADMIN PROGRAMME MANAGEMENT
           ========================================================= */

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult AdminProgrammes(
            string search = "",
            string status = "all",
            int? causeId = null,
            int? ngoId = null)
        {
            var model = _causesRepo.GetAdminProgrammes(
                search,
                status,
                causeId,
                ngoId);

            return View("AdminProgrammes", model);
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult CreateProgramme(int? causeId, int? ngoId)
        {
            var model = new ProgrammeAdminFormViewModel
            {
                CauseID = causeId.GetValueOrDefault(),
                NGOID = ngoId.GetValueOrDefault(),
                StartDate = DateTime.Today,
                Status = "Upcoming",
                IsFeatured = false
            };

            _causesRepo.PopulateProgrammeLookups(model);
            return View("ProgrammeForm", model);
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult CreateProgramme(ProgrammeAdminFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _causesRepo.PopulateProgrammeLookups(model);
                return View("ProgrammeForm", model);
            }

            string message;
            if (!_causesRepo.CreateProgramme(model, out message))
            {
                ModelState.AddModelError(string.Empty, message);
                _causesRepo.PopulateProgrammeLookups(model);
                return View("ProgrammeForm", model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction("AdminProgrammes");
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult EditProgramme(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid programme record.";
                return RedirectToAction("AdminProgrammes");
            }

            var model = _causesRepo.GetProgrammeForAdmin(id.Value);
            if (model == null)
            {
                TempData["ErrorMessage"] = "The selected programme record was not found.";
                return RedirectToAction("AdminProgrammes");
            }

            return View("ProgrammeForm", model);
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult EditProgramme(ProgrammeAdminFormViewModel model)
        {
            if (model == null || model.ProgrammeID <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid programme record.";
                return RedirectToAction("AdminProgrammes");
            }

            if (!ModelState.IsValid)
            {
                _causesRepo.PopulateProgrammeLookups(model);
                return View("ProgrammeForm", model);
            }

            string message;
            if (!_causesRepo.UpdateProgramme(model, out message))
            {
                ModelState.AddModelError(string.Empty, message);
                _causesRepo.PopulateProgrammeLookups(model);
                return View("ProgrammeForm", model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction("AdminProgrammes");
        }

        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult SetProgrammeStatus(
            int programmeId,
            string newStatus,
            string search = "",
            string status = "all",
            int? causeId = null,
            int? ngoId = null)
        {
            string message;
            var success = _causesRepo.SetProgrammeStatus(
                programmeId,
                newStatus,
                out message);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;

            return RedirectToAction("AdminProgrammes", new
            {
                search,
                status,
                causeId,
                ngoId
            });
        }
    }
}
