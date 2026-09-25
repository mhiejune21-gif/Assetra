using System;
using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Assetra.Models
{
    [FirestoreData]
    public class Property
    {
        [Key]
        [FirestoreProperty]
        public string PropertyId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Category { get; set; } = string.Empty;

        [FirestoreProperty]
        public string? Description { get; set; }

        [FirestoreProperty]
        public int Quantity { get; set; } = 1;

        [FirestoreProperty]
        public string Location { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Status { get; set; } = "Available";

        [FirestoreProperty]
        public string ConditionStatus { get; set; } = "Good";

        [FirestoreProperty]
        public string ImagePath { get; set; } = "/images/placeholder.jpg";

        [FirestoreProperty]
        public string QrCodePath { get; set; } = string.Empty;

        [FirestoreProperty]
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    }
}