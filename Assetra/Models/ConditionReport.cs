using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Google.Cloud.Firestore;

namespace Assetra.Models
{
    [FirestoreData]
    public class ConditionReport
    {
        [Key]
        [FirestoreProperty]
        public int ReportId { get; set; }
        
        [FirestoreProperty]
        public int LendingId { get; set; }

        [ForeignKey("LendingId")]
        public LendingRecord? Lending { get; set; }
        
        [FirestoreProperty]
        public bool IsScheduled { get; set; }

        [FirestoreProperty]
        public int? ReportNumber { get; set; }

        [FirestoreProperty]
        public DateTime? ScheduledDate { get; set; }
        
        [FirestoreProperty]
        public DateTime? DateSubmitted { get; set; }

        [FirestoreProperty]
        public string? Condition { get; set; } // "Good", "Minor Damage", "Critical Damage", "For Repair"

        [FirestoreProperty]
        public string? PhotoPath { get; set; }

        [FirestoreProperty]
        public string? Remarks { get; set; }
        
        [FirestoreProperty]
        public string Status { get; set; } = "Pending"; // "Pending", "Approved", "ReturnRequested", "Resolved"

        [FirestoreProperty]
        public string? CustodianRemarks { get; set; }

        [FirestoreProperty]
        public string? ReviewedBy { get; set; }

        [FirestoreProperty]
        public DateTime? DateReviewed { get; set; }
    }
}
