using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assetra.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Assetra.Services
{
    public class FirestoreService : IFirestoreService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<FirestoreService> _logger;
        private FirestoreDb? _db;
        private readonly bool _useInMemoryFallback;

        // In-memory fallback stores when Firestore credentials/Project ID are not yet configured
        private static readonly ConcurrentDictionary<int, User> _users = new();
        private static readonly ConcurrentDictionary<string, Property> _properties = new();
        private static readonly ConcurrentDictionary<int, LendingRecord> _lendings = new();
        private static readonly ConcurrentDictionary<int, ConditionReport> _conditionReports = new();
        private static readonly ConcurrentDictionary<int, ConditionHistory> _conditionHistories = new();
        private static readonly ConcurrentDictionary<int, MaintenanceRecord> _maintenanceRecords = new();
        private static bool _seeded = false;

        public FirestoreService(IConfiguration config, ILogger<FirestoreService> logger)
        {
            _config = config;
            _logger = logger;

            string projectId = _config["Firestore:ProjectId"] ?? string.Empty;
            string credPath = _config["Firestore:CredentialsPath"] ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(credPath) && File.Exists(credPath))
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credPath);
            }

            if (!string.IsNullOrWhiteSpace(projectId) && !projectId.Equals("YOUR_FIREBASE_PROJECT_ID", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _db = FirestoreDb.Create(projectId);
                    _useInMemoryFallback = false;
                    _logger.LogInformation("Successfully initialized Google Cloud Firestore for Project ID: {ProjectId}", projectId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to connect to Google Cloud Firestore with Project ID '{ProjectId}'. Operating in fallback mode.", projectId);
                    _useInMemoryFallback = true;
                }
            }
            else
            {
                _logger.LogInformation("No valid Firestore Project ID configured. Running with initialized Firestore schema & in-memory provider.");
                _useInMemoryFallback = true;
            }
        }

        public async Task InitializeAsync()
        {
            if (_seeded) return;

            if (_useInMemoryFallback || _db == null)
            {
                SeedDefaultsInMemory();
                _seeded = true;
                return;
            }

            try
            {
                // Check if Users collection has admin
                var usersColl = _db.Collection("users");
                var snapshot = await usersColl.Limit(1).GetSnapshotAsync();
                if (snapshot.Count == 0)
                {
                    await SeedDefaultsFirestoreAsync();
                }
                _seeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Firestore seeding. Falling back to memory provider.");
                SeedDefaultsInMemory();
                _seeded = true;
            }
        }

        private void SeedDefaultsInMemory()
        {
            if (!_users.Values.Any(u => u.Username == "admin"))
            {
                var admin = new User
                {
                    UserId = 1,
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    FullName = "Administrator",
                    Role = "Admin",
                    CreatedAt = DateTime.UtcNow
                };
                _users[admin.UserId] = admin;
            }

            if (!_users.Values.Any(u => u.Username == "johndoe"))
            {
                var user = new User
                {
                    UserId = 2,
                    Username = "johndoe",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    FullName = "John Doe",
                    Role = "User",
                    CreatedAt = DateTime.UtcNow
                };
                _users[user.UserId] = user;
            }

            if (_properties.IsEmpty)
            {
                var items = new[]
                {
                    new Property { PropertyId = "PROP001", Name = "Dell Latitude Laptop", Category = "Electronic Equipment", Description = "High-performance laptop for school assignments and labs.", Quantity = 5, Location = "IT Department", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP001_qr.png", DateAdded = DateTime.UtcNow },
                    new Property { PropertyId = "PROP002", Name = "Epson Projector", Category = "Other", Description = "High-definition portable classroom projector.", Quantity = 2, Location = "Conference Room A", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP002_qr.png", DateAdded = DateTime.UtcNow },
                    new Property { PropertyId = "PROP003", Name = "Digital Multimeter", Category = "Electronic Equipment", Description = "High-accuracy auto-ranging digital multimeter.", Quantity = 10, Location = "Physics Lab", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP003_qr.png", DateAdded = DateTime.UtcNow },
                    new Property { PropertyId = "PROP004", Name = "Compound Microscope", Category = "Laboratory Equipment", Description = "40X-2500X LED Lab Compound Microscope.", Quantity = 4, Location = "Biology Lab", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP004_qr.png", DateAdded = DateTime.UtcNow },
                    new Property { PropertyId = "PROP005", Name = "Steel Hammer 16oz", Category = "Hand Tools", Description = "Claw hammer with shock reduction grip.", Quantity = 12, Location = "Workshop Woodshop", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP005_qr.png", DateAdded = DateTime.UtcNow },
                    new Property { PropertyId = "PROP006", Name = "Leather Basketball", Category = "Sports Equipment", Description = "Official size 7 indoor/outdoor leather basketball.", Quantity = 15, Location = "Sports Gymnasium", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP006_qr.png", DateAdded = DateTime.UtcNow },
                    new Property { PropertyId = "4QEqdI", Name = "Demo Test Tool (QR Scanned)", Category = "Other", Description = "A mock tool matching the generated sample QR code.", Quantity = 1, Location = "Science Cabinet", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/4QEqdI_qr.png", DateAdded = DateTime.UtcNow }
                };

                foreach (var p in items)
                {
                    _properties[p.PropertyId] = p;
                }
            }
        }

        private async Task SeedDefaultsFirestoreAsync()
        {
            if (_db == null) return;

            var adminUser = new User
            {
                UserId = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                FullName = "Administrator",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            await _db.Collection("users").Document("1").SetAsync(adminUser);

            var normalUser = new User
            {
                UserId = 2,
                Username = "johndoe",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                FullName = "John Doe",
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };
            await _db.Collection("users").Document("2").SetAsync(normalUser);

            var properties = new[]
            {
                new Property { PropertyId = "PROP001", Name = "Dell Latitude Laptop", Category = "Electronic Equipment", Description = "High-performance laptop for school assignments and labs.", Quantity = 5, Location = "IT Department", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP001_qr.png", DateAdded = DateTime.UtcNow },
                new Property { PropertyId = "PROP002", Name = "Epson Projector", Category = "Other", Description = "High-definition portable classroom projector.", Quantity = 2, Location = "Conference Room A", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP002_qr.png", DateAdded = DateTime.UtcNow },
                new Property { PropertyId = "PROP003", Name = "Digital Multimeter", Category = "Electronic Equipment", Description = "High-accuracy auto-ranging digital multimeter.", Quantity = 10, Location = "Physics Lab", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP003_qr.png", DateAdded = DateTime.UtcNow },
                new Property { PropertyId = "PROP004", Name = "Compound Microscope", Category = "Laboratory Equipment", Description = "40X-2500X LED Lab Compound Microscope.", Quantity = 4, Location = "Biology Lab", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP004_qr.png", DateAdded = DateTime.UtcNow },
                new Property { PropertyId = "PROP005", Name = "Steel Hammer 16oz", Category = "Hand Tools", Description = "Claw hammer with shock reduction grip.", Quantity = 12, Location = "Workshop Woodshop", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP005_qr.png", DateAdded = DateTime.UtcNow },
                new Property { PropertyId = "PROP006", Name = "Leather Basketball", Category = "Sports Equipment", Description = "Official size 7 indoor/outdoor leather basketball.", Quantity = 15, Location = "Sports Gymnasium", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/PROP006_qr.png", DateAdded = DateTime.UtcNow },
                new Property { PropertyId = "4QEqdI", Name = "Demo Test Tool (QR Scanned)", Category = "Other", Description = "A mock tool matching the generated sample QR code.", Quantity = 1, Location = "Science Cabinet", Status = "Available", ConditionStatus = "Good", ImagePath = "/images/placeholder.jpg", QrCodePath = "/images/qrcodes/4QEqdI_qr.png", DateAdded = DateTime.UtcNow }
            };

            foreach (var p in properties)
            {
                await _db.Collection("properties").Document(p.PropertyId).SetAsync(p);
            }
        }

        #region Users
        public async Task<List<User>> GetUsersAsync()
        {
            if (_useInMemoryFallback || _db == null)
            {
                return _users.Values.OrderBy(u => u.UserId).ToList();
            }

            var snap = await _db.Collection("users").GetSnapshotAsync();
            return snap.Documents.Select(d => d.ConvertTo<User>()).OrderBy(u => u.UserId).ToList();
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            if (_useInMemoryFallback || _db == null)
            {
                return _users.TryGetValue(id, out var u) ? u : null;
            }

            var doc = await _db.Collection("users").Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<User>() : null;
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            if (_useInMemoryFallback || _db == null)
            {
                return _users.Values.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            }

            var query = await _db.Collection("users").WhereEqualTo("Username", username).GetSnapshotAsync();
            var doc = query.Documents.FirstOrDefault();
            return doc != null ? doc.ConvertTo<User>() : null;
        }

        public async Task AddUserAsync(User user)
        {
            if (user.UserId == 0)
            {
                var users = await GetUsersAsync();
                user.UserId = users.Count > 0 ? users.Max(u => u.UserId) + 1 : 1;
            }

            if (_useInMemoryFallback || _db == null)
            {
                _users[user.UserId] = user;
                return;
            }

            await _db.Collection("users").Document(user.UserId.ToString()).SetAsync(user);
        }

        public async Task UpdateUserAsync(User user)
        {
            if (_useInMemoryFallback || _db == null)
            {
                _users[user.UserId] = user;
                return;
            }

            await _db.Collection("users").Document(user.UserId.ToString()).SetAsync(user, SetOptions.Overwrite);
        }

        public async Task DeleteUserAsync(int id)
        {
            if (_useInMemoryFallback || _db == null)
            {
                _users.TryRemove(id, out _);
                return;
            }

            await _db.Collection("users").Document(id.ToString()).DeleteAsync();
        }
        #endregion

        #region Properties
        public async Task<List<Property>> GetPropertiesAsync()
        {
            if (_useInMemoryFallback || _db == null)
            {
                return _properties.Values.OrderBy(p => p.PropertyId).ToList();
            }

            var snap = await _db.Collection("properties").GetSnapshotAsync();
            return snap.Documents.Select(d => d.ConvertTo<Property>()).OrderBy(p => p.PropertyId).ToList();
        }

        public async Task<Property?> GetPropertyByIdAsync(string propertyId)
        {
            if (_useInMemoryFallback || _db == null)
            {
                return _properties.TryGetValue(propertyId, out var p) ? p : null;
            }

            var doc = await _db.Collection("properties").Document(propertyId).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Property>() : null;
        }

        public async Task AddPropertyAsync(Property property)
        {
            if (_useInMemoryFallback || _db == null)
            {
                _properties[property.PropertyId] = property;
                return;
            }

            await _db.Collection("properties").Document(property.PropertyId).SetAsync(property);
        }

        public async Task UpdatePropertyAsync(Property property)
        {
            if (_useInMemoryFallback || _db == null)
            {
                _properties[property.PropertyId] = property;
                return;
            }

            await _db.Collection("properties").Document(property.PropertyId).SetAsync(property, SetOptions.Overwrite);
        }

        public async Task DeletePropertyAsync(string propertyId)
        {
            if (_useInMemoryFallback || _db == null)
            {
                _properties.TryRemove(propertyId, out _);
                return;
            }

            await _db.Collection("properties").Document(propertyId).DeleteAsync();
        }
        #endregion

        #region LendingRecords
        public async Task<List<LendingRecord>> GetLendingRecordsAsync()
        {
            List<LendingRecord> records;
            if (_useInMemoryFallback || _db == null)
            {
                records = _lendings.Values.OrderByDescending(l => l.LendingId).ToList();
            }
            else
            {
                var snap = await _db.Collection("lendings").GetSnapshotAsync();
                records = snap.Documents.Select(d => d.ConvertTo<LendingRecord>()).OrderByDescending(l => l.LendingId).ToList();
            }

            // Populate relational navigation properties
            foreach (var rec in records)
            {
                if (!string.IsNullOrEmpty(rec.PropertyId))
                {
                    rec.Property = await GetPropertyByIdAsync(rec.PropertyId);
                }
                if (rec.BorrowedBy.HasValue)
                {
                    rec.User = await GetUserByIdAsync(rec.BorrowedBy.Value);
                }
            }

            return records;
        }

        public async Task<LendingRecord?> GetLendingRecordByIdAsync(int lendingId)
        {
            LendingRecord? record = null;
            if (_useInMemoryFallback || _db == null)
            {
                if (_lendings.TryGetValue(lendingId, out var r)) record = r;
            }
            else
            {
                var doc = await _db.Collection("lendings").Document(lendingId.ToString()).GetSnapshotAsync();
                if (doc.Exists) record = doc.ConvertTo<LendingRecord>();
            }

            if (record != null)
            {
                if (!string.IsNullOrEmpty(record.PropertyId))
                {
                    record.Property = await GetPropertyByIdAsync(record.PropertyId);
                }
                if (record.BorrowedBy.HasValue)
                {
                    record.User = await GetUserByIdAsync(record.BorrowedBy.Value);
                }
            }

            return record;
        }

        public async Task AddLendingRecordAsync(LendingRecord record)
        {
            if (record.LendingId == 0)
            {
                var list = await GetLendingRecordsAsync();
                record.LendingId = list.Count > 0 ? list.Max(l => l.LendingId) + 1 : 1;
            }

            if (_useInMemoryFallback || _db == null)
            {
                _lendings[record.LendingId] = record;
                return;
            }

            await _db.Collection("lendings").Document(record.LendingId.ToString()).SetAsync(record);
        }

        public async Task UpdateLendingRecordAsync(LendingRecord record)
        {
            if (_useInMemoryFallback || _db == null)
            {
                _lendings[record.LendingId] = record;
                return;
            }

            await _db.Collection("lendings").Document(record.LendingId.ToString()).SetAsync(record, SetOptions.Overwrite);
        }
        #endregion

        #region ConditionReports
        public async Task<List<ConditionReport>> GetConditionReportsAsync()
        {
            List<ConditionReport> reports;
            if (_useInMemoryFallback || _db == null)
            {
                reports = _conditionReports.Values.OrderByDescending(r => r.ReportId).ToList();
            }
            else
            {
                var snap = await _db.Collection("condition_reports").GetSnapshotAsync();
                reports = snap.Documents.Select(d => d.ConvertTo<ConditionReport>()).OrderByDescending(r => r.ReportId).ToList();
            }

            foreach (var r in reports)
            {
                if (r.LendingId != 0)
                {
                    r.Lending = await GetLendingRecordByIdAsync(r.LendingId);
                }
            }

            return reports;
        }

        public async Task<ConditionReport?> GetConditionReportByIdAsync(int reportId)
        {
            ConditionReport? report = null;
            if (_useInMemoryFallback || _db == null)
            {
                if (_conditionReports.TryGetValue(reportId, out var r)) report = r;
            }
            else
            {
                var doc = await _db.Collection("condition_reports").Document(reportId.ToString()).GetSnapshotAsync();
                if (doc.Exists) report = doc.ConvertTo<ConditionReport>();
            }

            if (report != null && report.LendingId != 0)
            {
                report.Lending = await GetLendingRecordByIdAsync(report.LendingId);
            }

            return report;
        }

        public async Task AddConditionReportAsync(ConditionReport report)
        {
            if (report.ReportId == 0)
            {
                var list = await GetConditionReportsAsync();
                report.ReportId = list.Count > 0 ? list.Max(r => r.ReportId) + 1 : 1;
            }

            if (_useInMemoryFallback || _db == null)
            {
                _conditionReports[report.ReportId] = report;
                return;
            }

            await _db.Collection("condition_reports").Document(report.ReportId.ToString()).SetAsync(report);
        }

        public async Task UpdateConditionReportAsync(ConditionReport report)
        {
            if (_useInMemoryFallback || _db == null)
            {
                _conditionReports[report.ReportId] = report;
                return;
            }

            await _db.Collection("condition_reports").Document(report.ReportId.ToString()).SetAsync(report, SetOptions.Overwrite);
        }
        #endregion

        #region ConditionHistories
        public async Task<List<ConditionHistory>> GetConditionHistoriesAsync()
        {
            List<ConditionHistory> histories;
            if (_useInMemoryFallback || _db == null)
            {
                histories = _conditionHistories.Values.OrderByDescending(h => h.ConditionHistoryId).ToList();
            }
            else
            {
                var snap = await _db.Collection("condition_histories").GetSnapshotAsync();
                histories = snap.Documents.Select(d => d.ConvertTo<ConditionHistory>()).OrderByDescending(h => h.ConditionHistoryId).ToList();
            }

            foreach (var h in histories)
            {
                if (!string.IsNullOrEmpty(h.PropertyId))
                {
                    h.Property = await GetPropertyByIdAsync(h.PropertyId);
                }
            }

            return histories;
        }

        public async Task AddConditionHistoryAsync(ConditionHistory history)
        {
            if (history.ConditionHistoryId == 0)
            {
                var list = await GetConditionHistoriesAsync();
                history.ConditionHistoryId = list.Count > 0 ? list.Max(h => h.ConditionHistoryId) + 1 : 1;
            }

            if (_useInMemoryFallback || _db == null)
            {
                _conditionHistories[history.ConditionHistoryId] = history;
                return;
            }

            await _db.Collection("condition_histories").Document(history.ConditionHistoryId.ToString()).SetAsync(history);
        }
        #endregion

        #region MaintenanceRecords
        public async Task<List<MaintenanceRecord>> GetMaintenanceRecordsAsync()
        {
            List<MaintenanceRecord> records;
            if (_useInMemoryFallback || _db == null)
            {
                records = _maintenanceRecords.Values.OrderByDescending(m => m.MaintenanceId).ToList();
            }
            else
            {
                var snap = await _db.Collection("maintenance_records").GetSnapshotAsync();
                records = snap.Documents.Select(d => d.ConvertTo<MaintenanceRecord>()).OrderByDescending(m => m.MaintenanceId).ToList();
            }

            foreach (var m in records)
            {
                if (!string.IsNullOrEmpty(m.PropertyId))
                {
                    m.Property = await GetPropertyByIdAsync(m.PropertyId);
                }
            }

            return records;
        }

        public async Task AddMaintenanceRecordAsync(MaintenanceRecord record)
        {
            if (record.MaintenanceId == 0)
            {
                var list = await GetMaintenanceRecordsAsync();
                record.MaintenanceId = list.Count > 0 ? list.Max(m => m.MaintenanceId) + 1 : 1;
            }

            if (_useInMemoryFallback || _db == null)
            {
                _maintenanceRecords[record.MaintenanceId] = record;
                return;
            }

            await _db.Collection("maintenance_records").Document(record.MaintenanceId.ToString()).SetAsync(record);
        }

        public async Task UpdateMaintenanceRecordAsync(MaintenanceRecord record)
        {
            if (_useInMemoryFallback || _db == null)
            {
                _maintenanceRecords[record.MaintenanceId] = record;
                return;
            }

            await _db.Collection("maintenance_records").Document(record.MaintenanceId.ToString()).SetAsync(record, SetOptions.Overwrite);
        }
        #endregion
    }
}
