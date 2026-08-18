using System;
using System.Collections.Generic;

namespace GiveAID_Project.Models
{
    public class CauseListItemViewModel
    {
        public int CauseID { get; set; }
        public string CauseName { get; set; }
        public string Description { get; set; }
        public string ImageURL { get; set; }
        public string Icon { get; set; }
        public bool IsActive { get; set; }
        public int ActiveNGOsCount { get; set; }
        public int ActiveProgramsCount { get; set; }
        public decimal TotalRaised { get; set; }
        public decimal TargetGoal { get; set; }
        public int ProgressPercent => TargetGoal > 0 ? (int)Math.Min(100, Math.Round((TotalRaised / TargetGoal) * 100)) : 0;
    }

    public class CauseListViewModel
    {
        public string SearchQuery { get; set; }
        public string SelectedCategory { get; set; }
        public List<CauseListItemViewModel> Causes { get; set; } = new List<CauseListItemViewModel>();
        public int TotalCausesCount => Causes != null ? Causes.Count : 0;
        public int TotalNGOsCount { get; set; }
        public int TotalProgramsCount { get; set; }
        public decimal TotalFundsRaised { get; set; }
    }

    public class CauseNgoItemViewModel
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
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
        public string Description { get; set; }
        public string ImageURL { get; set; }
        public string Icon { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalNGOsCount => NGOs != null ? NGOs.Count : 0;
        public int TotalProgramsCount => Programs != null ? Programs.Count : 0;
        public decimal TotalFundsRaised { get; set; }
        public decimal TotalTargetGoal { get; set; }
        public int OverallProgressPercent => TotalTargetGoal > 0 ? (int)Math.Min(100, Math.Round((TotalFundsRaised / TotalTargetGoal) * 100)) : 0;
        public List<CauseNgoItemViewModel> NGOs { get; set; } = new List<CauseNgoItemViewModel>();
        public List<NgoProgramDetailItemViewModel> Programs { get; set; } = new List<NgoProgramDetailItemViewModel>();
    }
}
