using System;
using System.ComponentModel.DataAnnotations;

namespace GiveAID_Project.Models
{
    public class UserProfileViewModel
    {
        public int UserID { get; set; }

        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must contain between 2 and 150 characters.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        [Display(Name = "Email address")]
        public string Email { get; set; }

        [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Phone number")]
        public string Phone { get; set; }

        [StringLength(20)]
        public string Gender { get; set; }

        [StringLength(120, ErrorMessage = "Profession cannot exceed 120 characters.")]
        public string Profession { get; set; }

        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
        [DataType(DataType.MultilineText)]
        public string Address { get; set; }

        [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
        public string City { get; set; }

        [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters.")]
        public string Country { get; set; }

        public DateTime MemberSince { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public string Initial
        {
            get
            {
                return string.IsNullOrWhiteSpace(FullName)
                    ? "U"
                    : FullName.Substring(0, 1).ToUpperInvariant();
            }
        }
    }
}
