using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GiveAID_Project.Models
{
    /* =========================================================
       PUBLIC CAUSE MODELS
       ========================================================= */

    public class CauseListItemViewModel
    {
        public int CauseID { get; set; }
        public string CauseName { get; set; }
        public string Slug { get; set; }
        public string ShortDescription { get; set; }
        public string Description { get; set; }
        public string ImageURL { get; set; }
        public string Icon { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public int ActiveNGOsCount { get; set; }
        public int ActiveProgramsCount { get; set; }
        public decimal TotalRaised { get; set; }
        public decimal TargetGoal { get; set; }

        public int ProgressPercent
        {
            get
            {
                return TargetGoal <= 0
                    ? 0
                    : (int)Math.Min(100, Math.Round((TotalRaised / TargetGoal) * 100));
            }
        }
    }

    public class CauseListViewModel
    {
        public string SearchQuery { get; set; }
        public string SelectedCategory { get; set; }
        public List<string> AvailableCategories { get; set; } = new List<string>();
        public List<CauseListItemViewModel> Causes { get; set; } = new List<CauseListItemViewModel>();
        public int TotalNGOsCount { get; set; }
        public int TotalProgramsCount { get; set; }
        public decimal TotalFundsRaised { get; set; }

        public int TotalCausesCount
        {
            get { return Causes == null ? 0 : Causes.Count; }
        }
    }

    public class CauseNgoItemViewModel
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public string Category { get; set; }
        public string LogoURL { get; set; }
        public string City { get; set; }
        public string Description { get; set; }
        public bool IsVerified { get; set; }
        public int ProgramsCount { get; set; }
        public decimal TotalRaised { get; set; }
    }

    public class CauseDetailViewModel
    {
        public int CauseID { get; set; }
        public string CauseName { get; set; }
        public string Slug { get; set; }
        public string ShortDescription { get; set; }
        public string Description { get; set; }
        public string ImageURL { get; set; }
        public string Icon { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalFundsRaised { get; set; }
        public decimal TotalTargetGoal { get; set; }
        public List<CauseNgoItemViewModel> NGOs { get; set; } = new List<CauseNgoItemViewModel>();
        public List<NgoProgramDetailItemViewModel> Programs { get; set; } = new List<NgoProgramDetailItemViewModel>();

        public int TotalNGOsCount
        {
            get { return NGOs == null ? 0 : NGOs.Count; }
        }

        public int TotalProgramsCount
        {
            get { return Programs == null ? 0 : Programs.Count; }
        }

        public int OverallProgressPercent
        {
            get
            {
                return TotalTargetGoal <= 0
                    ? 0
                    : (int)Math.Min(100, Math.Round((TotalFundsRaised / TotalTargetGoal) * 100));
            }
        }
    }

    /* =========================================================
       SHARED ADMIN LOOKUP MODEL
       ========================================================= */

    public class CauseAdminLookupItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string SecondaryText { get; set; }
        public bool IsActive { get; set; }
    }

    /* =========================================================
       ADMIN CAUSE LIST MODELS
       ========================================================= */

    public class AdminCauseListItemViewModel
    {
        public int CauseID { get; set; }
        public string CauseName { get; set; }
        public string Slug { get; set; }
        public string ShortDescription { get; set; }
        public string ImageURL { get; set; }
        public string Icon { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int TotalProgrammes { get; set; }
        public int ActiveProgrammes { get; set; }
        public int AssociatedNGOs { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CompletedFunds { get; set; }
    }

    public class AdminCauseListViewModel
    {
        public string SearchQuery { get; set; }
        public string SelectedStatus { get; set; }
        public string SelectedFeature { get; set; }
        public List<AdminCauseListItemViewModel> Causes { get; set; } = new List<AdminCauseListItemViewModel>();
        public int TotalCauses { get; set; }
        public int ActiveCauses { get; set; }
        public int InactiveCauses { get; set; }
        public int FeaturedCauses { get; set; }
        public int TotalProgrammes { get; set; }
    }

    /* =========================================================
       ADMIN CAUSE CREATE / EDIT MODEL
       ========================================================= */

    public class CauseAdminFormViewModel
    {
        public int CauseID { get; set; }

        [Required(ErrorMessage = "Cause name is required.")]
        [StringLength(120, ErrorMessage = "Cause name cannot exceed 120 characters.")]
        [Display(Name = "Cause name")]
        public string CauseName { get; set; }

        [Required(ErrorMessage = "Slug is required.")]
        [StringLength(140, ErrorMessage = "Slug cannot exceed 140 characters.")]
        [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, numbers and hyphens only.")]
        public string Slug { get; set; }

        [StringLength(300, ErrorMessage = "Short description cannot exceed 300 characters.")]
        [Display(Name = "Short description")]
        public string ShortDescription { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(2000, MinimumLength = 20, ErrorMessage = "Description must contain 20 to 2000 characters.")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "Enter a complete image URL.")]
        [Display(Name = "Image URL")]
        public string ImageURL { get; set; }

        [StringLength(100, ErrorMessage = "Icon name cannot exceed 100 characters.")]
        [Display(Name = "Icon name")]
        public string Icon { get; set; }

        [Display(Name = "Feature this cause")]
        public bool IsFeatured { get; set; }

        [Display(Name = "Active and publicly visible")]
        public bool IsActive { get; set; } = true;

        [Range(0, 10000, ErrorMessage = "Display order must be zero or greater.")]
        [Display(Name = "Display order")]
        public int DisplayOrder { get; set; }

        public bool IsEditMode
        {
            get { return CauseID > 0; }
        }
    }

    /* =========================================================
       ADMIN PROGRAMME LIST MODELS
       ========================================================= */

    public class AdminProgrammeListItemViewModel
    {
        public int ProgrammeID { get; set; }
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public int CauseID { get; set; }
        public string CauseName { get; set; }
        public string ProgrammeName { get; set; }
        public string Slug { get; set; }
        public string ShortDescription { get; set; }
        public string Location { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CompletedFunds { get; set; }
        public string Status { get; set; }
        public bool IsFeatured { get; set; }
        public string ImageURL { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int InterestedUsers { get; set; }

        public int ProgressPercent
        {
            get
            {
                return TargetAmount <= 0
                    ? 0
                    : (int)Math.Min(100, Math.Round((CompletedFunds / TargetAmount) * 100));
            }
        }
    }

    public class AdminProgrammeListViewModel
    {
        public string SearchQuery { get; set; }
        public string SelectedStatus { get; set; }
        public int? SelectedCauseID { get; set; }
        public int? SelectedNGOID { get; set; }
        public List<CauseAdminLookupItem> AvailableCauses { get; set; } = new List<CauseAdminLookupItem>();
        public List<CauseAdminLookupItem> AvailableNGOs { get; set; } = new List<CauseAdminLookupItem>();
        public List<AdminProgrammeListItemViewModel> Programmes { get; set; } = new List<AdminProgrammeListItemViewModel>();
        public int TotalProgrammes { get; set; }
        public int ActiveProgrammes { get; set; }
        public int UpcomingProgrammes { get; set; }
        public int CompletedProgrammes { get; set; }
        public int CancelledProgrammes { get; set; }
        public decimal TotalTargetAmount { get; set; }
        public decimal CompletedFunds { get; set; }
    }

    /* =========================================================
       ADMIN PROGRAMME CREATE / EDIT MODEL
       ========================================================= */

    public class ProgrammeAdminFormViewModel : IValidatableObject
    {
        public int ProgrammeID { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select an NGO.")]
        [Display(Name = "Associated NGO")]
        public int NGOID { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a cause.")]
        [Display(Name = "Cause")]
        public int CauseID { get; set; }

        [Required(ErrorMessage = "Programme name is required.")]
        [StringLength(180, ErrorMessage = "Programme name cannot exceed 180 characters.")]
        [Display(Name = "Programme name")]
        public string ProgrammeName { get; set; }

        [Required(ErrorMessage = "Slug is required.")]
        [StringLength(200, ErrorMessage = "Slug cannot exceed 200 characters.")]
        [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, numbers and hyphens only.")]
        public string Slug { get; set; }

        [StringLength(350, ErrorMessage = "Short description cannot exceed 350 characters.")]
        [Display(Name = "Short description")]
        public string ShortDescription { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(3000, MinimumLength = 20, ErrorMessage = "Description must contain 20 to 3000 characters.")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [StringLength(500, ErrorMessage = "Location cannot exceed 500 characters.")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "End date")]
        public DateTime? EndDate { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Target amount cannot be negative.")]
        [Display(Name = "Target amount (PKR)")]
        public decimal TargetAmount { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "Enter a complete image URL.")]
        [Display(Name = "Image URL")]
        public string ImageURL { get; set; }

        [Required(ErrorMessage = "Programme status is required.")]
        [RegularExpression("^(Upcoming|Active|Completed|Cancelled)$", ErrorMessage = "Select a valid programme status.")]
        public string Status { get; set; } = "Upcoming";

        [Display(Name = "Feature this programme")]
        public bool IsFeatured { get; set; }

        public List<CauseAdminLookupItem> AvailableCauses { get; set; } = new List<CauseAdminLookupItem>();
        public List<CauseAdminLookupItem> AvailableNGOs { get; set; } = new List<CauseAdminLookupItem>();

        public bool IsEditMode
        {
            get { return ProgrammeID > 0; }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate.HasValue && EndDate.Value.Date < StartDate.Date)
            {
                yield return new ValidationResult(
                    "End date cannot be earlier than the start date.",
                    new[] { "EndDate" });
            }
        }
    }
}
