using System;
using System.Collections.Generic;

namespace GiveAID_Project.Models
{
    public class PartnersPageViewModel
    {
        public List<PartnerListItemViewModel> Partners { get; set; } = new List<PartnerListItemViewModel>();
        public int ActivePartnerCount { get; set; }
    }

    public class PartnerListItemViewModel
    {
        public int PartnerID { get; set; }
        public string PartnerName { get; set; }
        public string Description { get; set; }
        public string LogoURL { get; set; }
        public string WebsiteURL { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
