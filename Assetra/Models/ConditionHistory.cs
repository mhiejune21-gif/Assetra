using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Google.Cloud.Firestore;

namespace Assetra.Models
{
    [FirestoreData]
    public class ConditionHistory
    {
        [Key]
        [FirestoreProperty]
        public int ConditionHistoryId { get; set; }

        [Required]
        [FirestoreProperty]
        public string PropertyId { get; set; } = string.Empty;

        [Required]
        [FirestoreProperty]
        public string ConditionStatus { get; set; } = string.Empty;

        [FirestoreProperty]
        public string? Notes { get; set; }

        [FirestoreProperty]
        public DateTime DateRecorded { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public string? RecordedBy { get; set; }

        [ForeignKey("PropertyId")]
        public Property? Property { get; set; }
    }
}
