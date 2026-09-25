using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Google.Cloud.Firestore;

namespace Assetra.Models
{
    [FirestoreData]
    public class LendingRecord
    {
        [Key]
        [FirestoreProperty]
        public int LendingId { get; set; }

        [FirestoreProperty]
        public string PropertyId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string BorrowerName { get; set; } = string.Empty;

        [FirestoreProperty]
        public int? BorrowedBy { get; set; }

        [FirestoreProperty]
        public string? StudentId { get; set; }

        [FirestoreProperty]
        public string BorrowedCondition { get; set; } = "Good";

        [FirestoreProperty]
        public string? ReturnedCondition { get; set; }

        [FirestoreProperty]
        public string? ProcessedBy { get; set; }

        [FirestoreProperty]
        public DateTime DateBorrowed { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(7);

        [FirestoreProperty]
        public DateTime? DateReturned { get; set; }

        [FirestoreProperty]
        public string Status { get; set; } = "Pending";

        [ForeignKey("PropertyId")]
        public Property? Property { get; set; }

        [ForeignKey("BorrowedBy")]
        public User? User { get; set; }
    }
}