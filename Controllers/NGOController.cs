using System;
using System.Web.Mvc;
using GiveAID_Project.Models;

namespace GiveAID_Project.Controllers
{
    public class NGOController : Controller
    {
        private readonly NgoRepository _ngoRepo;

        public NGOController()
        {
            _ngoRepo = new NgoRepository();
        }

        public NGOController(NgoRepository ngoRepo)
        {
            _ngoRepo = ngoRepo;
        }

        // =========================================================
        // PUBLIC NGO LISTING (/NGO or /NGO/Index)
        // =========================================================
        [HttpGet]
        public ActionResult Index(string search, string location, int? causeId, string category)
        {
            var model = _ngoRepo.GetPublicNgos(search, location, causeId, category);

            if (Request.IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    total = model.TotalResultsCount,
                    ngos = model.NGOs
                }, JsonRequestBehavior.AllowGet);
            }

            return View(model);
        }

        // =========================================================
        // PUBLIC NGO DETAILS (/NGO/Details/{id})
        // =========================================================
        [HttpGet]
        public ActionResult Details(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please specify a valid NGO ID.";
                return RedirectToAction("Index");
            }

            var ngo = _ngoRepo.GetNgoById(id.Value);

            if (ngo == null)
            {
                TempData["ErrorMessage"] = "The requested NGO is not found or is currently not available publicly.";
                return RedirectToAction("Index");
            }

            return View(ngo);
        }

        // =========================================================
        // ADMIN NGO DIRECTORY
        // /NGO/AdminIndex
        // =========================================================
        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult AdminIndex(string search = "", string status = "all", string category = "")
        {
            ViewBag.AdminPageTitle = "NGO management";
            ViewBag.AdminPageSubtitle = "Manage associated organisations and public visibility";

            var model = _ngoRepo.GetAdminNgos(search, status, category);
            return View(model);
        }

        // =========================================================
        // CREATE NGO - GET
        // /NGO/AdminCreate
        // =========================================================
        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult AdminCreate()
        {
            ViewBag.AdminPageTitle = "Add NGO";
            ViewBag.AdminPageSubtitle = "Create a new associated organisation record";

            var model = new NgoAdminFormViewModel
            {
                Country = "Pakistan",
                IsActive = true
            };

            return View("AdminForm", model);
        }

        // =========================================================
        // CREATE NGO - POST
        // =========================================================
        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult AdminCreate(NgoAdminFormViewModel model)
        {
            ViewBag.AdminPageTitle = "Add NGO";
            ViewBag.AdminPageSubtitle = "Create a new associated organisation record";

            if (!ModelState.IsValid)
            {
                return View("AdminForm", model);
            }

            string message;
            bool success = _ngoRepo.CreateNgo(model, out message);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View("AdminForm", model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction("AdminIndex");
        }

        // =========================================================
        // EDIT NGO - GET
        // /NGO/AdminEdit/{id}
        // =========================================================
        [HttpGet]
        [AuthorizeRoles("Admin")]
        public ActionResult AdminEdit(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please select a valid NGO record.";
                return RedirectToAction("AdminIndex");
            }

            var model = _ngoRepo.GetNgoForAdmin(id.Value);

            if (model == null)
            {
                TempData["ErrorMessage"] = "The selected NGO record was not found.";
                return RedirectToAction("AdminIndex");
            }

            ViewBag.AdminPageTitle = "Edit NGO";
            ViewBag.AdminPageSubtitle = "Update organisation information and public visibility";

            return View("AdminForm", model);
        }

        // =========================================================
        // EDIT NGO - POST
        // =========================================================
        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult AdminEdit(NgoAdminFormViewModel model)
        {
            ViewBag.AdminPageTitle = "Edit NGO";
            ViewBag.AdminPageSubtitle = "Update organisation information and public visibility";

            if (model == null || model.NGOID <= 0)
            {
                TempData["ErrorMessage"] = "A valid NGO record is required.";
                return RedirectToAction("AdminIndex");
            }

            if (!ModelState.IsValid)
            {
                return View("AdminForm", model);
            }

            string message;
            bool success = _ngoRepo.UpdateNgo(model, out message);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View("AdminForm", model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction("AdminIndex");
        }

        // =========================================================
        // ACTIVATE / DEACTIVATE NGO
        // =========================================================
        [HttpPost]
        [AuthorizeRoles("Admin")]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleAdminStatus(
            int ngoId,
            bool makeActive,
            string search = "",
            string status = "all",
            string category = "")
        {
            string message;
            bool success = _ngoRepo.SetNgoActiveStatus(ngoId, makeActive, out message);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;

            return RedirectToAction("AdminIndex", new
            {
                search = search,
                status = status,
                category = category
            });
        }
    }
}
