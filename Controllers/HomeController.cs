using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace GiveAID_Project.Controllers
{
    public class HomeController : Controller
    {
        // GET: / or /Home/Index
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        // GET: /Home/About
        [HttpGet]
        public ActionResult About()
        {
            return View();
        }

        // GET: /Home/Partners
        [HttpGet]
        public ActionResult Partners()
        {
            return View();
        }

        // GET: /Home/Contact
        [HttpGet]
        public ActionResult Contact()
        {
            return View();
        }

        // POST: /Home/Contact
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Contact(string name, string email, string subject, string message, string inquirerType)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Please fill in all required fields (Name, Email, and Message).";
                return View();
            }

            // Successfully received inquiry (ready for future email/DB backend integration)
            TempData["SuccessMessage"] = $"Thank you, {name}! Your message has been received. Our team will get back to you at {email} within 24 hours.";
            return RedirectToAction("Contact");
        }
    }
}