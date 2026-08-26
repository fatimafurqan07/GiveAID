using System;
using System.Web.Mvc;
using GiveAID_Project.Models;

namespace GiveAID_Project.Controllers
{
    public class HomeController : Controller
    {
        private readonly HomePageRepository _homeRepository;
        private readonly PartnersRepository _partnersRepository;
        private readonly ContactRepository _contactRepository;
        private readonly AccountRepository _accountRepository;

        public HomeController()
        {
            _homeRepository = new HomePageRepository();
            _partnersRepository = new PartnersRepository();
            _contactRepository = new ContactRepository();
            _accountRepository = new AccountRepository();
        }

        public HomeController(HomePageRepository homeRepository)
        {
            _homeRepository = homeRepository;
            _partnersRepository = new PartnersRepository();
            _contactRepository = new ContactRepository();
            _accountRepository = new AccountRepository();
        }

        public HomeController(
            HomePageRepository homeRepository,
            PartnersRepository partnersRepository,
            ContactRepository contactRepository)
        {
            _homeRepository = homeRepository;
            _partnersRepository = partnersRepository;
            _contactRepository = contactRepository;
            _accountRepository = new AccountRepository();
        }

        public HomeController(
            HomePageRepository homeRepository,
            PartnersRepository partnersRepository,
            ContactRepository contactRepository,
            AccountRepository accountRepository)
        {
            _homeRepository = homeRepository;
            _partnersRepository = partnersRepository;
            _contactRepository = contactRepository;
            _accountRepository = accountRepository;
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View(_homeRepository.GetHomePage());
        }

        [HttpGet]
        public ActionResult About()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Partners()
        {
            return View(_partnersRepository.GetActivePartners());
        }

        [HttpGet]
        public ActionResult Contact()
        {
            return View(new ContactViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Contact(ContactViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Website))
            {
                TempData["SuccessMessage"] =
                    "Thank you. Your message has been received.";

                return RedirectToAction("Contact");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int? currentUserId = GetCurrentUserId();
            var result = _contactRepository.Save(model, currentUserId);

            TempData["SuccessMessage"] =
                result == ContactSaveResult.RecentDuplicate
                    ? "This message was already received. You do not need to send it again."
                    : currentUserId.HasValue
                        ? "Thank you. Your message has been saved. You can follow the administrator's reply from My Messages."
                        : "Thank you. Your message has been saved and is ready for administrator review.";

            return RedirectToAction("Contact");
        }

        /* =====================================================
           CURRENT SIGNED-IN USER
           ===================================================== */

        private int? GetCurrentUserId()
        {
            int userId;

            if (Session["UserID"] != null &&
                int.TryParse(Session["UserID"].ToString(), out userId) &&
                userId > 0)
            {
                return userId;
            }

            if (User != null &&
                User.Identity != null &&
                User.Identity.IsAuthenticated &&
                !string.IsNullOrWhiteSpace(User.Identity.Name))
            {
                var user = _accountRepository.GetUserByEmail(User.Identity.Name);

                if (user != null && user.UserID > 0)
                {
                    Session["UserID"] = user.UserID;
                    Session["UserName"] = user.FullName;
                    Session["UserEmail"] = user.Email;

                    return user.UserID;
                }
            }

            return null;
        }
    }
}
