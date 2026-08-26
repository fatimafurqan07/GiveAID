using System.Collections.Generic;

namespace GiveAID_Project.Models
{
    public class UserDonationsViewModel
    {
        public string Search { get; set; }

        public string Status { get; set; }

        public int TotalRecords { get; set; }

        public int CompletedRecords { get; set; }

        public int PendingRecords { get; set; }

        public decimal CompletedAmount { get; set; }

        public List<UserDonationItem> Donations { get; set; }
            = new List<UserDonationItem>();
    }

    public class UserInterestsViewModel
    {
        public List<UserInterestItem> Programs { get; set; }
            = new List<UserInterestItem>();

        public int TotalPrograms => Programs == null ? 0 : Programs.Count;
    }
}
