using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GiveAID_Project.Models
{
    public class CreateDonationModel
    {
        [Required(ErrorMessage = "Donor name is required.")]
        [Display(Name = "Your Full Name")]
        [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
        public string DonorName { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
        public string DonorEmail { get; set; }

        [Required(ErrorMessage = "Donation amount is required.")]
        [Range(10, 50000000, ErrorMessage = "Please enter an amount of at least PKR 10.")]
        [Display(Name = "Donation Amount (PKR)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Please select a partner NGO.")]
        [Display(Name = "Partner NGO")]
        public int NGOID { get; set; }

        [Required(ErrorMessage = "Please select a cause / sector.")]
        [Display(Name = "Target Cause")]
        public int CauseID { get; set; }

        [Display(Name = "Specific Program (Optional)")]
        public int? ProgramID { get; set; }

        [Display(Name = "Message / Note of Support (Optional)")]
        [StringLength(1000, ErrorMessage = "Message cannot exceed 1000 characters.")]
        public string Message { get; set; }

        [Display(Name = "Payment Method")]
        public string PaymentRail { get; set; } = "Raast / 1Link";
    }

    public class DonationDetailViewModel
    {
        public int DonationID { get; set; }
        public string PaymentReference { get; set; }
        public int UserID { get; set; }
        public string DonorName { get; set; }
        public string DonorEmail { get; set; }
        public int NGOID { get; set; }
        public string NGOName { get; set; }
        public int CauseID { get; set; }
        public string CauseName { get; set; }
        public int? ProgramID { get; set; }
        public string ProgramName { get; set; }
        public decimal Amount { get; set; }
        public string DonationStatus { get; set; } // Pending, Approved, Denied
        public string AdminApprovalStatus { get; set; }
        public string NGOApprovalStatus { get; set; }
        public string PaymentStatus { get; set; } // Pending, Successful, Failed
        public string PaymentMethod { get; set; }
        public DateTime DonationDate { get; set; }
        public DateTime? AdminReviewedAt { get; set; }
        public string AdminRemarks { get; set; }
        public string Message { get; set; }
    }

    public class LookupItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int? SecondaryID { get; set; }
    }

    public class DonationFormDataViewModel
    {
        public CreateDonationModel FormModel { get; set; } = new CreateDonationModel();
        public List<LookupItem> NGOs { get; set; } = new List<LookupItem>();
        public List<LookupItem> Causes { get; set; } = new List<LookupItem>();
        public List<LookupItem> Programs { get; set; } = new List<LookupItem>();
    }
}
