using System;
using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Assetra.Models
{
    [FirestoreData]
    public class User
    {
        [Key]
        [FirestoreProperty]
        public int UserId { get; set; }

        [FirestoreProperty]
        public string Username { get; set; } = string.Empty;

        [FirestoreProperty]
        public string PasswordHash { get; set; } = string.Empty;

        [FirestoreProperty]
        public string FullName { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Role { get; set; } = "User";

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}