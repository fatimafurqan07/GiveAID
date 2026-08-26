using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GiveAID_Project.Models
{
    public class NgoListItemViewModel
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public string RegistrationNumber { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string LogoURL { get; set; }
        public string WebsiteURL { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ActiveProgramsCount { get; set; }
        public int CausesSupportedCount { get; set; }
        public List<string> CausesList { get; set; } = new List<string>();
        public List<int> CauseIdsList { get; set; } = new List<int>();
        public decimal TotalFundsRaised { get; set; }
        public string Status { get; set; }
        public bool IsVerified { get; set; }

        public string PrimaryCategory
        {
            get { return Category; }
            set { Category = value; }
        }
    }

    public class NgoListViewModel
    {
        public string SearchQuery { get; set; }
        public string SelectedLocation { get; set; }
        public int? SelectedCauseId { get; set; }
        public string SelectedCategory { get; set; }
        public List<NgoListItemViewModel> NGOs { get; set; } = new List<NgoListItemViewModel>();
        public List<string> AvailableLocations { get; set; } = new List<string>();
        public List<string> AvailableCategories { get; set; } = new List<string>();
        public List<LookupItem> AvailableCauses { get; set; } = new List<LookupItem>();
        public int TotalActiveNgos { get; set; }
        public int TotalActivePrograms { get; set; }
        public decimal TotalImpactRaised { get; set; }

        public int TotalResultsCount
        {
            get { return NGOs == null ? 0 : NGOs.Count; }
        }

        public int TotalVerifiedNgos
        {
            get { return TotalActiveNgos; }
            set { TotalActiveNgos = value; }
        }
    }

    public class NgoCauseItemViewModel
    {
        public int CauseID { get; set; }
        public string CauseName { get; set; }
        public string Description { get; set; }
        public string ImageURL { get; set; }
        public string Icon { get; set; }
        public int ProgramsCount { get; set; }
        public decimal TotalRaised { get; set; }
    }

    public class NgoProgramDetailItemViewModel
    {
        public int ProgramID { get; set; }
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public int CauseID { get; set; }
        public string CauseName { get; set; }
        public string ProgramName { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public string Status { get; set; }
        public string ImageURL { get; set; }
        public int InterestedCount { get; set; }

        public int ProgressPercent
        {
            get
            {
                return TargetAmount <= 0
                    ? 0
                    : (int)Math.Min(100, Math.Round((CurrentAmount / TargetAmount) * 100));
            }
        }
    }

    public class NgoDetailViewModel
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public string RegistrationNumber { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Mission { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string LogoURL { get; set; }
        public string WebsiteURL { get; set; }
        public string Status { get; set; }
        public bool IsVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalFundsRaised { get; set; }
        public int TotalDonationsCount { get; set; }
        public int TotalDonorsCount { get; set; }
        public List<NgoCauseItemViewModel> Causes { get; set; } = new List<NgoCauseItemViewModel>();
        public List<NgoProgramDetailItemViewModel> Programs { get; set; } = new List<NgoProgramDetailItemViewModel>();

        public int TotalProgramsCount
        {
            get { return Programs == null ? 0 : Programs.Count; }
        }

        public int TotalCausesCount
        {
            get { return Causes == null ? 0 : Causes.Count; }
        }
    }

    /* =========================================================
       ADMIN NGO MANAGEMENT MODELS
       ========================================================= */

    public class AdminNgoListItemViewModel
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public string RegistrationNumber { get; set; }
        public string Category { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string ContactPerson { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ProgrammesCount { get; set; }
        public int ActiveProgrammesCount { get; set; }
        public decimal CompletedFunds { get; set; }
    }

    public class AdminNgoListViewModel
    {
        public string SearchQuery { get; set; }
        public string SelectedStatus { get; set; }
        public string SelectedCategory { get; set; }
        public List<string> AvailableCategories { get; set; } = new List<string>();
        public List<AdminNgoListItemViewModel> NGOs { get; set; } = new List<AdminNgoListItemViewModel>();
        public int TotalNGOs { get; set; }
        public int ActiveNGOs { get; set; }
        public int InactiveNGOs { get; set; }
        public int TotalProgrammes { get; set; }
    }

    public class NgoAdminFormViewModel
    {
        public int NGOID { get; set; }

        [Required(ErrorMessage = "NGO name is required.")]
        [StringLength(200, ErrorMessage = "NGO name cannot exceed 200 characters.")]
        [Display(Name = "NGO name")]
        public string NGOName { get; set; }

        [Required(ErrorMessage = "Registration number is required.")]
        [StringLength(100, ErrorMessage = "Registration number cannot exceed 100 characters.")]
        [Display(Name = "Registration number")]
        public string RegistrationNumber { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        [StringLength(120, ErrorMessage = "Category cannot exceed 120 characters.")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(2000, MinimumLength = 20, ErrorMessage = "Description must contain 20 to 2000 characters.")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [StringLength(500)]
        [DataType(DataType.MultilineText)]
        public string Address { get; set; }

        [Required(ErrorMessage = "City is required.")]
        [StringLength(100)]
        public string City { get; set; }

        [Required(ErrorMessage = "Country is required.")]
        [StringLength(100)]
        public string Country { get; set; } = "Pakistan";

        [StringLength(50)]
        [Phone(ErrorMessage = "Enter a valid phone number.")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [StringLength(200)]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "Enter a complete logo URL.")]
        [Display(Name = "Logo URL")]
        public string LogoURL { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "Enter a complete website URL.")]
        [Display(Name = "Website URL")]
        public string WebsiteURL { get; set; }

        [StringLength(150)]
        [Display(Name = "Contact person")]
        public string ContactPerson { get; set; }

        [Display(Name = "Active and publicly visible")]
        public bool IsActive { get; set; } = true;

        public bool IsEditMode
        {
            get { return NGOID > 0; }
        }
    }
}
