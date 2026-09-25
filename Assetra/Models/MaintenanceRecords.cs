using System;
using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Assetra.Models
{
    [FirestoreData]
    public class MaintenanceRecord
    {
        [Key]
        [FirestoreProperty]
        public int MaintenanceId { get; set; }

        [FirestoreProperty]
        public string PropertyId { get; set; } = string.Empty;

        [FirestoreProperty]
        public DateTime ScheduledDate { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public string Description { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Status { get; set; } = "Scheduled";

        public Property? Property { get; set; }
    }
}