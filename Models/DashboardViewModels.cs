using System;
using System.Collections.Generic;

namespace GiveAID_Project.Models
{
    // ==========================================
    // SHARED / COMMON ITEM VIEW MODELS
    // ==========================================

    public class RecentDonationItem
    {
        public int DonationID { get; set; }
        public string PaymentReference { get; set; }
        public string DonorName { get; set; }
        public string DonorEmail { get; set; }
        public string NGOName { get; set; }
        public string ProgramName { get; set; }
        public string CauseName { get; set; }
        public decimal Amount { get; set; }
        public DateTime DonationDate { get; set; }
        public string Status { get; set; }
    }

    public class RecentApplicationItem
    {
        public int ApplicationID { get; set; }
        public string NGOName { get; set; }
        public string ApplicantName { get; set; }
        public string ApplicantEmail { get; set; }
        public string City { get; set; }
        public string Status { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class RecentUserItem
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string RoleName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class NgoProgramItem
    {
        public int ProgramID { get; set; }
        public string ProgramName { get; set; }
        public string CauseName { get; set; }
        public string Location { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public int ProgressPercent => TargetAmount > 0 ? (int)Math.Min(100, Math.Round((CurrentAmount / TargetAmount) * 100)) : 0;
        public string Status { get; set; }
        public int InterestedCount { get; set; }
    }

    public class NgoSupporterItem
    {
        public string SupporterName { get; set; }
        public string SupporterEmail { get; set; }
        public decimal TotalDonated { get; set; }
        public int DonationsCount { get; set; }
        public DateTime LastDonationDate { get; set; }
    }

    public class UserDonationItem
    {
        public int DonationID { get; set; }
        public string PaymentReference { get; set; }
        public string NGOName { get; set; }
        public string ProgramName { get; set; }
        public string CauseName { get; set; }
        public decimal Amount { get; set; }
        public DateTime DonationDate { get; set; }
        public string Status { get; set; }
        public string CardType { get; set; }
        public string CardLastFour { get; set; }
    }

    public class UserInterestItem
    {
        public int ProgramID { get; set; }
        public string ProgramName { get; set; }
        public string NGOName { get; set; }
        public string CauseName { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public int ProgressPercent => TargetAmount > 0 ? (int)Math.Min(100, Math.Round((CurrentAmount / TargetAmount) * 100)) : 0;
        public string Status { get; set; }
        public DateTime InterestDate { get; set; }
    }

    // ==========================================
    // 1. ADMIN DASHBOARD VIEW MODEL
    // ==========================================

    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalNGOs { get; set; }
        public int TotalPrograms { get; set; }
        public int TotalCauses { get; set; }
        public int TotalDonationsCount { get; set; }
        public decimal TotalFundsRaised { get; set; }
        public int PendingApplicationsCount { get; set; }

        // Chart Data
        public List<string> MonthlyLabels { get; set; } = new List<string>();
        public List<decimal> MonthlyAmounts { get; set; } = new List<decimal>();

        public List<string> CauseLabels { get; set; } = new List<string>();
        public List<decimal> CauseAmounts { get; set; } = new List<decimal>();

        public List<string> ProgramStatusLabels { get; set; } = new List<string>();
        public List<int> ProgramStatusCounts { get; set; } = new List<int>();

        // Lists
        public List<RecentDonationItem> RecentDonations { get; set; } = new List<RecentDonationItem>();
        public List<RecentApplicationItem> RecentApplications { get; set; } = new List<RecentApplicationItem>();
        public List<RecentUserItem> RecentUsers { get; set; } = new List<RecentUserItem>();
    }

    // ==========================================
    // 2. NGO DASHBOARD VIEW MODEL
    // ==========================================

    public class NgoDashboardViewModel
    {
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string City { get; set; }
        public string Status { get; set; }

        public decimal TotalRaised { get; set; }
        public int TotalDonationsCount { get; set; }
        public int TotalDonorsCount { get; set; }
        public int TotalProgramsCount { get; set; }
        public int TotalInterestedUsersCount { get; set; }

        // Chart Data
        public List<string> ProgramNames { get; set; } = new List<string>();
        public List<decimal> ProgramRaised { get; set; } = new List<decimal>();
        public List<decimal> ProgramTargets { get; set; } = new List<decimal>();

        public List<string> MonthlyLabels { get; set; } = new List<string>();
        public List<decimal> MonthlyAmounts { get; set; } = new List<decimal>();

        // Lists
        public List<NgoProgramItem> Programs { get; set; } = new List<NgoProgramItem>();
        public List<RecentDonationItem> RecentDonations { get; set; } = new List<RecentDonationItem>();
        public List<NgoSupporterItem> TopSupporters { get; set; } = new List<NgoSupporterItem>();
    }

    // ==========================================
    // 3. USER DASHBOARD VIEW MODEL
    // ==========================================

    public class UserDashboardViewModel
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime MemberSince { get; set; }

        public decimal TotalDonated { get; set; }
        public int TotalDonationsCount { get; set; }
        public int CausesSupportedCount { get; set; }
        public int SavedProgramsCount { get; set; }

        // Chart Data
        public List<string> CauseLabels { get; set; } = new List<string>();
        public List<decimal> CauseAmounts { get; set; } = new List<decimal>();

        public List<string> MonthlyLabels { get; set; } = new List<string>();
        public List<decimal> MonthlyAmounts { get; set; } = new List<decimal>();

        // Lists
        public List<UserDonationItem> Donations { get; set; } = new List<UserDonationItem>();
        public List<UserInterestItem> SavedPrograms { get; set; } = new List<UserInterestItem>();
    }
}
