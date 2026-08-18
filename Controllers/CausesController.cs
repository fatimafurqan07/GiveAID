using System;
using System.Web.Mvc;
using GiveAID_Project.Models;

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
            _causesRepo = causesRepo;
        }

        // =========================================================
        // 1. PUBLIC CAUSES LISTING (/Causes or /Causes/Index)
        // =========================================================
        [HttpGet]
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

        // =========================================================
        // 2. CAUSE DETAILS (/Causes/Details/{id})
        // =========================================================
        [HttpGet]
        public ActionResult Details(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please specify a valid Cause ID.";
                return RedirectToAction("Index");
            }

            var cause = _causesRepo.GetCauseById(id.Value);
            if (cause == null)
            {
                TempData["ErrorMessage"] = "The requested Cause was not found or is currently inactive.";
                return RedirectToAction("Index");
            }

            return View(cause);
        }
    }
}
