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
        private bool _dbInitAttempted;

        // In-memory fallback stores
        private static readonly ConcurrentDictionary<int, User> _users = new();
        private static readonly ConcurrentDictionary<string, Property> _properties = new();
        private static readonly ConcurrentDictionary<int, LendingRecord> _lendings = new();
        private static readonly ConcurrentDictionary<int, ConditionReport> _conditionReports = new();
        private static readonly ConcurrentDictionary<int, ConditionHistory> _conditionHistories = new();
        private static readonly ConcurrentDictionary<int, MaintenanceRecord> _maintenanceRecords = new();
        private static bool _seeded = false;

        private const string EmbeddedBase64Creds = "ewogICJ0eXBlIjogInNlcnZpY2VfYWNjb3VudCIsCiAgInByb2plY3RfaWQiOiAiYXNzZXRyYS1lZjE2NSIsCiAgInByaXZhdGVfa2V5X2lkIjogIjhlYzhkMWZiNzI3ZmM0ZWRiN2RhYjQ3MWM5MWY3NTQ3YWYwZDJkYzIiLAogICJwcml2YXRlX2tleSI6ICItLS0tLUJFR0lOIFBSSVZBVEUgS0VZLS0tLS1cbk1JSUV2UUlCQURBTkJna3Foa2lHOXcwQkFRRUZBQVNDQktjd2dnU2pBZ0VBQW9JQkFRQzVDRC9wbkVobmFSaCtcbkUybG1OVWViajA5YlcvNU1rSkVqcnBya0xmMnp0MWlOdGEzWVU4b1pvR2FOR1RwVzhJMC90M1ZxMkZURFZJTTFcbmtvanFnMmxxenFVc3RwL0FkZU5mcDJyQ21lMU9URU8yQURlU3VocnpHNXVLOHpxaVl5WSsxaElDVEt6b1E0ZkpcblB0OU9tNDhDaFhjamVnMFhwSGcxMnRudHZ5WTRpRDVyOHNDTjh1a2J3c21LdHpDWUQ1Rld2UXlmcmR3R283dmhcblFwNFhZdnNCQThIa1pWY2pBeHFqdU1TdUVIcmQwSzdzS0pvQVphb1pUdURKa0lwK2Qvck50Y3ZjaVNHb054VXRcbkU1Y1NMblV3MThHNnNqb2FYa3dsRmUvbjNnN1hMNlNLeWdJS0JaVWJRbExFL1VMNmhKY2xBdVdsRG9wa3kwSVJcbkdCWmFaUXJQQWdNQkFBRUNnZ0VBS0taMVZCOXZrTGg0Rndxd3R6R3hYNjJtWTQzY1dublFTU1NOQnVCTHduWGZcblVKSy9kSzFEMDBsMy9qdXlvM01KdFJ2YkFmUXcreERRR3E3c3dZakpXaHU0RWhDMUhCVktOTE9WTXRlYVdQOU1cblNPblhTN2J6UU1HcDlHYm5WTkd2ajFKOGRtRGVBOUVRVFZnd3V4WlYzdG52aUszQnZwWDFpdTlmdldtblBmZmdcbmtLV1ZrWnRjZ1FxOGsyRDFERWpnNlR2S3ZhQlNEeXpjTnozbWRaZ0c1d2h4NXdROExTSFNBNm9BQW1UYzhNVHVcbjBpeGQrcHZuc01IMWZaV3RjakdvbG1uVFdwTk9RdXRPbmlOQUo5ZFppb2kzOWxEODd3WDBMRk5udWFwTDZ5UlJcbm56aUI4aE5aSU9DeHBObjRVRzhJWWlOTGNycmlVdE9PeFlUVGVucnF3UUtCZ1FEd3dqekxJUUpHMjlMTGZ6QU9cbndzUHdCeWVheU5MNnVnM1ZLemdqRGdrZ29McnBZMjJWbGxJU201aWdpRkExQ2VQdWNTZFBKbFZ6TDVuUWlBZlNcbmxFS1RWb3I1MjJySVpWR0lPaGZ1eWhRc1FMOUVhTk53MkNpVWxrTVVHK2NGNFg2aUFXRm9CUTVKWmk0U1pNcTZcblMxSE5mbzJPRVlXdHJUV1ZNUnA4WC8va3NRS0JnUURFdnViMURIdStsWlNrN2FwVVY3SnpKcjl4TElsenZ3bldcbjJXdnI4emxGVXI5alJUbEYySXJJa0wrcjMvZEMyT2ZROWZ6WU9SVkNQclArQllYUjEwdzN4eEI1WVdlV29uMnZcbnhkTFVKTlhncEJrb0J2K2ZNTHZka1MyYnFZR29Qc09ZTHZuWWRBaXk3M2o4TGNlTUYwdXFYeXBSR3Baa1dabXpcblppSEl5YjNIZndLQmdGMThqdTZ4V3BqNU10a2lBaDg1TWF3Nm12NVhqTlVlK2RBVWdDL2NlMTdZQ3J3bGg1L1dcblJ2aEN3dmxTOVJJalRRYUJtYW42VUtQeGorQ1JjYmdySTVoaXVvUmExeFFKZzZkS0o1RHBsdnU0Q0kwZnh6ckNcbk5NLzl1VDVOdDE5cE9DcmdMbHFkMi9aVVh2OTFjK0x5N0VqSEkyQlBIWUZiQ0x0dDNjTDk0L2VCQW9HQUtDOTlcbjZRdDFzd1hHYUxHS210T1d4V0ppcy9FTzJpOXBDUk03c2VQcURMak1FckN1OUE4NHVhS25JNm9KVFFRVXhWK1pcbkYya0JhSmg2RnlaMW9OakMzcG13U2JxVmQvVVVpdlJ6RFpYQWdiUEMxNlFtVGhPY0s3TmRoMi9sNWNGOEhmZHFcblhNWEdpUlhVdGwxN1pxZlRjcWNoYzVOa3FIYU1xRkh5RUpyMFFtMENnWUVBcjcrM1pCZ2NyVGxZd2Z3VkhNT2Vcbi9NYVNDbzhuL1JrNVJPb2JubU5FbUN6SXdnTjBmQ0FOa25wdWFob041T1RiU0ZXbW5BWFI4MVBxdjJybG9xTTJcbndWTVJpekI4aHNaL055UGU2WWordUl6SlpvdXpqckdEdTdLd1o0ek5rR0g0YXVJdjkyT1R6eEtmaENmOFFNUGRcbjkyUHIyaHJidUxmSmJGbWRvZkljR21ZPVxuLS0tLS1FTkQgUFJJVkFURSBLRVktLS0tLVxuIiwKICAiY2xpZW50X2VtYWlsIjogImZpcmViYXNlLWFkbWluc2RrLWZic3ZjQGFzc2V0cmEtZWYxNjUuaWFtLmdzZXJ2aWNlYWNjb3VudC5jb20iLAogICJjbGllbnRfaWQiOiAiMTEwNzMxOTY1NTI0MjY0MTg2NzM2IiwKICAiYXV0aF91cmkiOiAiaHR0cHM6Ly9hY2NvdW50cy5nb29nbGUuY29tL28vb2F1dGgyL2F1dGgiLAogICJ0b2tlbl91cmkiOiAiaHR0cHM6Ly9vYXV0aDIuZ29vZ2xlYXBpcy5jb20vdG9rZW4iLAogICJhdXRoX3Byb3ZpZGVyX3g1MDlfY2VydF91cmwiOiAiaHR0cHM6Ly93d3cuZ29vZ2xlYXBpcy5jb20vb2F1dGgyL3YxL2NlcnRzIiwKICAiY2xpZW50X3g1MDlfY2VydF91cmwiOiAiaHR0cHM6Ly93d3cuZ29vZ2xlYXBpcy5jb20vcm9ib3QvdjEvbWV0YWRhdGEveDUwOS9maXJlYmFzZS1hZG1pbnNkay1mYnN2YyU0MGFzc2V0cmEtZWYxNjUuaWFtLmdzZXJ2aWNlYWNjb3VudC5jb20iLAogICJ1bml2ZXJzZV9kb21haW4iOiAiZ29vZ2xlYXBpcy5jb20iCn0K";

        public FirestoreService(IConfiguration config, ILogger<FirestoreService> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Lazily builds and caches the FirestoreDb. If building fails, returns null
        /// but does NOT permanently lock into fallback — retries on the next call.
        /// </summary>
        private FirestoreDb? GetDb()
        {
            if (_db != null) return _db;
            if (_dbInitAttempted) return null; // Avoid repeated failures within the same request cycle

            _dbInitAttempted = true;

            string projectId = _config["Firestore:ProjectId"] ?? "assetra-ef165";
            string credPath = _config["Firestore:CredentialsPath"] ?? "firebase_key.json";

            try
            {
                if (File.Exists(credPath))
                {
                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credPath);
                    _db = FirestoreDb.Create(projectId);
                }
                else
                {
                    byte[] bytes = Convert.FromBase64String(EmbeddedBase64Creds);
                    using var stream = new MemoryStream(bytes);
                    var builder = new FirestoreDbBuilder
                    {
                        ProjectId = projectId,
                        Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromStream(stream)
                    };
                    _db = builder.Build();
                }
                _logger.LogInformation("[FIRESTORE] Connected successfully to project: {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FIRESTORE] FAILED to build FirestoreDb for project '{ProjectId}'", projectId);
                _db = null;
            }

            return _db;
        }

        public async Task InitializeAsync()
        {
            if (_seeded) return;

            var db = GetDb();
            if (db == null)
            {
                _logger.LogWarning("[FIRESTORE] No database connection. Seeding defaults in-memory.");
                SeedDefaultsInMemory();
                _seeded = true;
                return;
            }

            try
            {
                var snapshot = await db.Collection("users").Limit(1).GetSnapshotAsync();
                if (snapshot.Count == 0)
                {
                    _logger.LogInformation("[FIRESTORE] Empty database detected. Seeding defaults to Firestore.");
                    await SeedDefaultsFirestoreAsync(db);
                }
                else
                {
                    _logger.LogInformation("[FIRESTORE] Existing data found ({Count} user(s)). Skipping seed.", snapshot.Count);
                }
                _seeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FIRESTORE] InitializeAsync query failed. Seeding in-memory fallback.");
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

        private async Task SeedDefaultsFirestoreAsync(FirestoreDb db)
        {
            var adminUser = new User
            {
                UserId = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                FullName = "Administrator",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            await db.Collection("users").Document("1").SetAsync(adminUser);

            var normalUser = new User
            {
                UserId = 2,
                Username = "johndoe",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                FullName = "John Doe",
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };
            await db.Collection("users").Document("2").SetAsync(normalUser);

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
                await db.Collection("properties").Document(p.PropertyId).SetAsync(p);
            }
        }

        #region Users
        public async Task<List<User>> GetUsersAsync()
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var snap = await db.Collection("users").GetSnapshotAsync();
                    return snap.Documents.Select(d => d.ConvertTo<User>()).OrderBy(u => u.UserId).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetUsersAsync failed. Falling back to in-memory.");
                }
            }
            return _users.Values.OrderBy(u => u.UserId).ToList();
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var doc = await db.Collection("users").Document(id.ToString()).GetSnapshotAsync();
                    return doc.Exists ? doc.ConvertTo<User>() : null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetUserByIdAsync({Id}) failed.", id);
                }
            }
            return _users.TryGetValue(id, out var u) ? u : null;
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var query = await db.Collection("users").WhereEqualTo("Username", username).GetSnapshotAsync();
                    var doc = query.Documents.FirstOrDefault();
                    return doc != null ? doc.ConvertTo<User>() : null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetUserByUsernameAsync('{Username}') failed.", username);
                }
            }
            return _users.Values.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        public async Task AddUserAsync(User user)
        {
            if (user.UserId == 0)
            {
                var users = await GetUsersAsync();
                user.UserId = users.Count > 0 ? users.Max(u => u.UserId) + 1 : 1;
            }

            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("users").Document(user.UserId.ToString()).SetAsync(user);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] AddUserAsync failed for user {UserId}.", user.UserId);
                }
            }
            _users[user.UserId] = user;
        }

        public async Task UpdateUserAsync(User user)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("users").Document(user.UserId.ToString()).SetAsync(user, SetOptions.Overwrite);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] UpdateUserAsync failed for user {UserId}.", user.UserId);
                }
            }
            _users[user.UserId] = user;
        }

        public async Task DeleteUserAsync(int id)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("users").Document(id.ToString()).DeleteAsync();
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] DeleteUserAsync failed for user {Id}.", id);
                }
            }
            _users.TryRemove(id, out _);
        }
        #endregion

        #region Properties
        public async Task<List<Property>> GetPropertiesAsync()
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var snap = await db.Collection("properties").GetSnapshotAsync();
                    return snap.Documents.Select(d => d.ConvertTo<Property>()).OrderBy(p => p.PropertyId).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetPropertiesAsync failed.");
                }
            }
            return _properties.Values.OrderBy(p => p.PropertyId).ToList();
        }

        public async Task<Property?> GetPropertyByIdAsync(string propertyId)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var doc = await db.Collection("properties").Document(propertyId).GetSnapshotAsync();
                    return doc.Exists ? doc.ConvertTo<Property>() : null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetPropertyByIdAsync('{PropertyId}') failed.", propertyId);
                }
            }
            return _properties.TryGetValue(propertyId, out var p) ? p : null;
        }

        public async Task AddPropertyAsync(Property property)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("properties").Document(property.PropertyId).SetAsync(property);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] AddPropertyAsync failed.");
                }
            }
            _properties[property.PropertyId] = property;
        }

        public async Task UpdatePropertyAsync(Property property)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("properties").Document(property.PropertyId).SetAsync(property, SetOptions.Overwrite);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] UpdatePropertyAsync failed.");
                }
            }
            _properties[property.PropertyId] = property;
        }

        public async Task DeletePropertyAsync(string propertyId)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("properties").Document(propertyId).DeleteAsync();
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] DeletePropertyAsync failed.");
                }
            }
            _properties.TryRemove(propertyId, out _);
        }
        #endregion

        #region LendingRecords
        public async Task<List<LendingRecord>> GetLendingRecordsAsync()
        {
            List<LendingRecord> records;
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var snap = await db.Collection("lendings").GetSnapshotAsync();
                    records = snap.Documents.Select(d => d.ConvertTo<LendingRecord>()).OrderByDescending(l => l.LendingId).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetLendingRecordsAsync failed.");
                    records = _lendings.Values.OrderByDescending(l => l.LendingId).ToList();
                }
            }
            else
            {
                records = _lendings.Values.OrderByDescending(l => l.LendingId).ToList();
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
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var doc = await db.Collection("lendings").Document(lendingId.ToString()).GetSnapshotAsync();
                    if (doc.Exists) record = doc.ConvertTo<LendingRecord>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetLendingRecordByIdAsync({Id}) failed.", lendingId);
                    if (_lendings.TryGetValue(lendingId, out var r)) record = r;
                }
            }
            else
            {
                if (_lendings.TryGetValue(lendingId, out var r)) record = r;
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

            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("lendings").Document(record.LendingId.ToString()).SetAsync(record);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] AddLendingRecordAsync failed.");
                }
            }
            _lendings[record.LendingId] = record;
        }

        public async Task UpdateLendingRecordAsync(LendingRecord record)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("lendings").Document(record.LendingId.ToString()).SetAsync(record, SetOptions.Overwrite);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] UpdateLendingRecordAsync failed.");
                }
            }
            _lendings[record.LendingId] = record;
        }
        #endregion

        #region ConditionReports
        public async Task<List<ConditionReport>> GetConditionReportsAsync()
        {
            List<ConditionReport> reports;
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var snap = await db.Collection("condition_reports").GetSnapshotAsync();
                    reports = snap.Documents.Select(d => d.ConvertTo<ConditionReport>()).OrderByDescending(r => r.ReportId).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetConditionReportsAsync failed.");
                    reports = _conditionReports.Values.OrderByDescending(r => r.ReportId).ToList();
                }
            }
            else
            {
                reports = _conditionReports.Values.OrderByDescending(r => r.ReportId).ToList();
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
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var doc = await db.Collection("condition_reports").Document(reportId.ToString()).GetSnapshotAsync();
                    if (doc.Exists) report = doc.ConvertTo<ConditionReport>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetConditionReportByIdAsync({Id}) failed.", reportId);
                    if (_conditionReports.TryGetValue(reportId, out var r)) report = r;
                }
            }
            else
            {
                if (_conditionReports.TryGetValue(reportId, out var r)) report = r;
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

            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("condition_reports").Document(report.ReportId.ToString()).SetAsync(report);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] AddConditionReportAsync failed.");
                }
            }
            _conditionReports[report.ReportId] = report;
        }

        public async Task UpdateConditionReportAsync(ConditionReport report)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("condition_reports").Document(report.ReportId.ToString()).SetAsync(report, SetOptions.Overwrite);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] UpdateConditionReportAsync failed.");
                }
            }
            _conditionReports[report.ReportId] = report;
        }
        #endregion

        #region ConditionHistories
        public async Task<List<ConditionHistory>> GetConditionHistoriesAsync()
        {
            List<ConditionHistory> histories;
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var snap = await db.Collection("condition_histories").GetSnapshotAsync();
                    histories = snap.Documents.Select(d => d.ConvertTo<ConditionHistory>()).OrderByDescending(h => h.ConditionHistoryId).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetConditionHistoriesAsync failed.");
                    histories = _conditionHistories.Values.OrderByDescending(h => h.ConditionHistoryId).ToList();
                }
            }
            else
            {
                histories = _conditionHistories.Values.OrderByDescending(h => h.ConditionHistoryId).ToList();
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

            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("condition_histories").Document(history.ConditionHistoryId.ToString()).SetAsync(history);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] AddConditionHistoryAsync failed.");
                }
            }
            _conditionHistories[history.ConditionHistoryId] = history;
        }
        #endregion

        #region MaintenanceRecords
        public async Task<List<MaintenanceRecord>> GetMaintenanceRecordsAsync()
        {
            List<MaintenanceRecord> records;
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    var snap = await db.Collection("maintenance_records").GetSnapshotAsync();
                    records = snap.Documents.Select(d => d.ConvertTo<MaintenanceRecord>()).OrderByDescending(m => m.MaintenanceId).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] GetMaintenanceRecordsAsync failed.");
                    records = _maintenanceRecords.Values.OrderByDescending(m => m.MaintenanceId).ToList();
                }
            }
            else
            {
                records = _maintenanceRecords.Values.OrderByDescending(m => m.MaintenanceId).ToList();
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

            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("maintenance_records").Document(record.MaintenanceId.ToString()).SetAsync(record);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] AddMaintenanceRecordAsync failed.");
                }
            }
            _maintenanceRecords[record.MaintenanceId] = record;
        }

        public async Task UpdateMaintenanceRecordAsync(MaintenanceRecord record)
        {
            var db = GetDb();
            if (db != null)
            {
                try
                {
                    await db.Collection("maintenance_records").Document(record.MaintenanceId.ToString()).SetAsync(record, SetOptions.Overwrite);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIRESTORE] UpdateMaintenanceRecordAsync failed.");
                }
            }
            _maintenanceRecords[record.MaintenanceId] = record;
        }
        #endregion
    }
}
