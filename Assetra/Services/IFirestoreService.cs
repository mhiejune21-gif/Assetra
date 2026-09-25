using System.Collections.Generic;
using System.Threading.Tasks;
using Assetra.Models;

namespace Assetra.Services
{
    public interface IFirestoreService
    {
        Task InitializeAsync();

        // Users
        Task<List<User>> GetUsersAsync();
        Task<User?> GetUserByIdAsync(int id);
        Task<User?> GetUserByUsernameAsync(string username);
        Task AddUserAsync(User user);
        Task UpdateUserAsync(User user);
        Task DeleteUserAsync(int id);

        // Properties (Tools / Equipment)
        Task<List<Property>> GetPropertiesAsync();
        Task<Property?> GetPropertyByIdAsync(string propertyId);
        Task AddPropertyAsync(Property property);
        Task UpdatePropertyAsync(Property property);
        Task DeletePropertyAsync(string propertyId);

        // Lending Records
        Task<List<LendingRecord>> GetLendingRecordsAsync();
        Task<LendingRecord?> GetLendingRecordByIdAsync(int lendingId);
        Task AddLendingRecordAsync(LendingRecord record);
        Task UpdateLendingRecordAsync(LendingRecord record);

        // Condition Reports
        Task<List<ConditionReport>> GetConditionReportsAsync();
        Task<ConditionReport?> GetConditionReportByIdAsync(int reportId);
        Task AddConditionReportAsync(ConditionReport report);
        Task UpdateConditionReportAsync(ConditionReport report);

        // Condition Histories
        Task<List<ConditionHistory>> GetConditionHistoriesAsync();
        Task AddConditionHistoryAsync(ConditionHistory history);

        // Maintenance Records
        Task<List<MaintenanceRecord>> GetMaintenanceRecordsAsync();
        Task AddMaintenanceRecordAsync(MaintenanceRecord record);
        Task UpdateMaintenanceRecordAsync(MaintenanceRecord record);
    }
}
