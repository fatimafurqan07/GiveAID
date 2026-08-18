using System;
using System.Collections.Generic;

namespace GiveAID_Project.Models
{
    public class NgoListItemViewModel
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public string Description { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string LogoURL { get; set; }
        public string WebsiteURL { get; set; }
        public string Status { get; set; }
        public bool IsVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ActiveProgramsCount { get; set; }
        public int CausesSupportedCount { get; set; }
        public List<string> CausesList { get; set; } = new List<string>();
        public List<int> CauseIdsList { get; set; } = new List<int>();
        public decimal TotalFundsRaised { get; set; }
        public string PrimaryCategory { get; set; }
    }

    public class NgoListViewModel
    {
        public string SearchQuery { get; set; }
        public string SelectedLocation { get; set; }
        public int? SelectedCauseId { get; set; }
        public string SelectedCategory { get; set; }
        public List<NgoListItemViewModel> NGOs { get; set; } = new List<NgoListItemViewModel>();
        public List<string> AvailableLocations { get; set; } = new List<string>();
        public List<LookupItem> AvailableCauses { get; set; } = new List<LookupItem>();
        public int TotalResultsCount => NGOs != null ? NGOs.Count : 0;
        public int TotalVerifiedNgos { get; set; }
        public int TotalActivePrograms { get; set; }
        public decimal TotalImpactRaised { get; set; }
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
        public int ProgressPercent => TargetAmount > 0 ? (int)Math.Min(100, Math.Round((CurrentAmount / TargetAmount) * 100)) : 0;
        public string Status { get; set; } // Active, Upcoming, Completed
        public string ImageURL { get; set; }
        public int InterestedCount { get; set; }
    }

    public class NgoDetailViewModel
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public string Description { get; set; }
        public string Mission { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
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
        public int TotalProgramsCount => Programs != null ? Programs.Count : 0;
        public int TotalCausesCount => Causes != null ? Causes.Count : 0;
        public List<NgoCauseItemViewModel> Causes { get; set; } = new List<NgoCauseItemViewModel>();
        public List<NgoProgramDetailItemViewModel> Programs { get; set; } = new List<NgoProgramDetailItemViewModel>();
    }
}
