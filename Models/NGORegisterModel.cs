using System.ComponentModel.DataAnnotations;

namespace GiveAID_Project.Models
{
    public class NGORegisterModel
    {
        [Required(ErrorMessage = "NGO / Organization name is required.")]
        [Display(Name = "NGO / Organization Name")]
        [StringLength(150, ErrorMessage = "NGO Name cannot exceed 150 characters.")]
        public string NGOName { get; set; }

        [Required(ErrorMessage = "Official email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Official Email Address")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Contact phone number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Contact Phone")]
        [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Primary cause/sector is required.")]
        [Display(Name = "Primary Sector / Category")]
        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
        public string Category { get; set; }

        [Required(ErrorMessage = "City is required.")]
        [Display(Name = "City")]
        [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
        public string City { get; set; }

        [Required(ErrorMessage = "Head office address is required.")]
        [Display(Name = "Office / Headquarters Address")]
        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
        public string Address { get; set; }

        [Display(Name = "Official Website (Optional)")]
        [Url(ErrorMessage = "Please enter a valid URL (e.g., https://example.org).")]
        [StringLength(500, ErrorMessage = "Website URL cannot exceed 500 characters.")]
        public string WebsiteURL { get; set; }

        [Display(Name = "SECP / Registration Number (Optional)")]
        [StringLength(100, ErrorMessage = "Registration number cannot exceed 100 characters.")]
        public string RegistrationNumber { get; set; }

        [Required(ErrorMessage = "Organization description and mission is required.")]
        [Display(Name = "Organization Mission & Description")]
        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class NGOAccountStatusInfo
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public string NGOStatus { get; set; } // 'Pending', 'Active', 'Inactive', 'Suspended', 'Banned'
        public string ApplicationStatus { get; set; } // 'Pending', 'Approved', 'Rejected', 'Denied'
        public bool UserIsActive { get; set; }
        public string AdminRemarks { get; set; }
    }
}
