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
        // 1. PUBLIC NGO LISTING (/NGO or /NGO/Index)
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
        // 2. NGO DETAILS (/NGO/Details/{id})
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
    }
}
