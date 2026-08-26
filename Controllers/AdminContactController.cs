using System;
using System.Linq;
using System.Web.Mvc;
using GiveAID_Project.Models;

namespace GiveAID_Project.Controllers
{
    [Authorize]
    [AuthorizeRoles("Admin")]
    public class AdminContactController : Controller
    {
        private readonly ContactRepository _contactRepository;
        private readonly AccountRepository _accountRepository;

        public AdminContactController()
        {
            _contactRepository = new ContactRepository();
            _accountRepository = new AccountRepository();
        }

        // Existing constructor preserved for compatibility.
        public AdminContactController(ContactRepository contactRepository)
        {
            if (contactRepository == null)
            {
                throw new ArgumentNullException("contactRepository");
            }

            _contactRepository = contactRepository;
            _accountRepository = new AccountRepository();
        }

        public AdminContactController(
            ContactRepository contactRepository,
            AccountRepository accountRepository)
        {
            if (contactRepository == null)
            {
                throw new ArgumentNullException("contactRepository");
            }

            if (accountRepository == null)
            {
                throw new ArgumentNullException("accountRepository");
            }

            _contactRepository = contactRepository;
            _accountRepository = accountRepository;
        }

        /* =====================================================
           ADMIN CONTACT MESSAGE LIST
           URL: /AdminContact
           ===================================================== */

        [HttpGet]
        public ActionResult Index(string search = "", string status = "all")
        {
            var model = _contactRepository.GetAdminMessages(search, status);
            return View(model);
        }

        /* =====================================================
           ADMIN CONTACT MESSAGE DETAILS
           URL: /AdminContact/Details/5
           ===================================================== */

        [HttpGet]
        public ActionResult Details(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid contact message.";

                return RedirectToAction("Index");
            }

            var model = _contactRepository.GetAdminMessageById(id.Value);

            if (model == null)
            {
                TempData["ErrorMessage"] =
                    "The requested contact message could not be found.";

                return RedirectToAction("Index");
            }

            // Opening a new message counts as reading it.
            if (string.Equals(
                model.Status,
                "New",
                StringComparison.OrdinalIgnoreCase))
            {
                _contactRepository.MarkAsReadIfNew(id.Value);
                model.Status = "Read";
            }

            return View(model);
        }

        /* =====================================================
           SAVE ADMINISTRATOR REPLY
           The reply is stored in ContactMessages and becomes
           visible to the registered user in My Messages.
           ===================================================== */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveReply(AdminContactReplyViewModel model)
        {
            if (model == null || model.ContactMessageID <= 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid contact message.";

                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                var validationMessage = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Please enter a valid reply."
                        : error.ErrorMessage)
                    .FirstOrDefault();

                TempData["ErrorMessage"] =
                    validationMessage ?? "Please enter a valid reply.";

                return RedirectToAction(
                    "Details",
                    new { id = model.ContactMessageID });
            }

            var adminUserId = GetCurrentAdminUserId();

            string resultMessage;
            var success = _contactRepository.SaveAdminReply(
                model.ContactMessageID,
                model.Reply,
                adminUserId,
                out resultMessage);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                resultMessage;

            return RedirectToAction(
                "Details",
                new { id = model.ContactMessageID });
        }

        /* =====================================================
           ADMIN STATUS UPDATE
           Allowed values: New, Read, Replied, Closed
           ===================================================== */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(
            int? id,
            string status,
            string returnSearch = "",
            string returnStatus = "all")
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid contact message.";

                return RedirectToAction("Index", new
                {
                    search = returnSearch,
                    status = returnStatus
                });
            }

            // A message must contain a real saved reply before it can be Replied.
            if (string.Equals(
                status,
                "Replied",
                StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "Please write and save an administrator reply instead of changing only the status.";

                return RedirectToAction("Details", new { id = id.Value });
            }

            string resultMessage;
            var success = _contactRepository.UpdateMessageStatus(
                id.Value,
                status,
                out resultMessage);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                resultMessage;

            return RedirectToAction("Details", new { id = id.Value });
        }

        /* =====================================================
           QUICK STATUS UPDATE FROM LIST PAGE
           ===================================================== */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult QuickUpdateStatus(
            int? id,
            string status,
            string search = "",
            string filterStatus = "all")
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid contact message.";

                return RedirectToAction("Index", new
                {
                    search,
                    status = filterStatus
                });
            }

            if (string.Equals(
                status,
                "Replied",
                StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "Open the message and write a reply before marking it as replied.";

                return RedirectToAction("Details", new { id = id.Value });
            }

            string resultMessage;
            var success = _contactRepository.UpdateMessageStatus(
                id.Value,
                status,
                out resultMessage);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                resultMessage;

            return RedirectToAction("Index", new
            {
                search,
                status = filterStatus
            });
        }

        /* =====================================================
           CURRENT ADMINISTRATOR USER ID
           ===================================================== */

        private int GetCurrentAdminUserId()
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
                    Session["UserRole"] =
                        user.Roles.FirstOrDefault() ?? "Admin";

                    return user.UserID;
                }
            }

            return 0;
        }
    }
}
