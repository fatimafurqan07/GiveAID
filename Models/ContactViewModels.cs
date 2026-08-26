using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GiveAID_Project.Models
{
    /* =========================================================
       PUBLIC CONTACT FORM
       ========================================================= */

    public class ContactViewModel
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(150, MinimumLength = 2,
            ErrorMessage = "Name must be between 2 and 150 characters.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(256)]
        [Display(Name = "Email address")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(30)]
        [Display(Name = "Phone number")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Please enter a subject.")]
        [StringLength(200, MinimumLength = 3,
            ErrorMessage = "Subject must be between 3 and 200 characters.")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Please enter your message.")]
        [StringLength(2000, MinimumLength = 10,
            ErrorMessage = "Message must be between 10 and 2000 characters.")]
        [DataType(DataType.MultilineText)]
        public string Message { get; set; }

        // Hidden anti-spam field. Real visitors leave this empty.
        public string Website { get; set; }
    }

    /* =========================================================
       ADMIN CONTACT MESSAGE LISTING
       ========================================================= */

    public class AdminContactMessageListItemViewModel
    {
        public int ContactMessageID { get; set; }
        public int? UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Subject { get; set; }
        public string MessagePreview { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? RepliedAt { get; set; }

        public bool IsNew
        {
            get
            {
                return string.Equals(
                    Status,
                    "New",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsRegisteredUser
        {
            get { return UserID.HasValue && UserID.Value > 0; }
        }
    }

    public class AdminContactMessagesViewModel
    {
        public string SearchQuery { get; set; }
        public string SelectedStatus { get; set; }

        public List<AdminContactMessageListItemViewModel> Messages { get; set; }
            = new List<AdminContactMessageListItemViewModel>();

        public int TotalMessages { get; set; }
        public int NewMessages { get; set; }
        public int ReadMessages { get; set; }
        public int RepliedMessages { get; set; }
        public int ClosedMessages { get; set; }
    }

    /* =========================================================
       ADMIN CONTACT MESSAGE DETAILS AND REPLY
       ========================================================= */

    public class AdminContactMessageDetailViewModel
    {
        public int ContactMessageID { get; set; }
        public int? UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public string AdminReply { get; set; }
        public DateTime? RepliedAt { get; set; }
        public int? RepliedByUserID { get; set; }
        public string RepliedByName { get; set; }

        public bool IsRegisteredUser
        {
            get { return UserID.HasValue && UserID.Value > 0; }
        }

        public bool HasAdminReply
        {
            get { return !string.IsNullOrWhiteSpace(AdminReply); }
        }

        public bool CanMarkRead
        {
            get
            {
                return string.Equals(
                    Status,
                    "New",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool CanMarkReplied
        {
            get
            {
                return !string.Equals(
                           Status,
                           "Replied",
                           StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(
                           Status,
                           "Closed",
                           StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool CanClose
        {
            get
            {
                return !string.Equals(
                    Status,
                    "Closed",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool CanReopen
        {
            get
            {
                return string.Equals(
                    Status,
                    "Closed",
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public class AdminContactReplyViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid contact message.")]
        public int ContactMessageID { get; set; }

        [Required(ErrorMessage = "Please enter a reply before saving.")]
        [StringLength(2000, MinimumLength = 3,
            ErrorMessage = "Reply must be between 3 and 2000 characters.")]
        [Display(Name = "Administrator reply")]
        [DataType(DataType.MultilineText)]
        public string Reply { get; set; }
    }

    /* =========================================================
       USER DASHBOARD - MY MESSAGES LIST
       ========================================================= */

    public class UserContactMessageListItemViewModel
    {
        public int ContactMessageID { get; set; }
        public string Subject { get; set; }
        public string MessagePreview { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RepliedAt { get; set; }
        public bool HasAdminReply { get; set; }
    }

    public class UserContactMessagesViewModel
    {
        public string SearchQuery { get; set; }
        public string SelectedStatus { get; set; }

        public List<UserContactMessageListItemViewModel> Messages { get; set; }
            = new List<UserContactMessageListItemViewModel>();

        public int TotalMessages { get; set; }
        public int AwaitingReplyCount { get; set; }
        public int RepliedCount { get; set; }
        public int ClosedCount { get; set; }
    }

    /* =========================================================
       USER DASHBOARD - MESSAGE DETAILS
       ========================================================= */

    public class UserContactMessageDetailViewModel
    {
        public int ContactMessageID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public string AdminReply { get; set; }
        public DateTime? RepliedAt { get; set; }
        public string RepliedByName { get; set; }

        public bool HasAdminReply
        {
            get { return !string.IsNullOrWhiteSpace(AdminReply); }
        }
    }
}
