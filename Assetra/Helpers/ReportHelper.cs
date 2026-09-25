using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assetra.Models;
using Assetra.Services;

namespace Assetra.Helpers
{
    public static class ReportHelper
    {
        public static async Task GenerateReportScheduleAsync(IFirestoreService firestoreService, LendingRecord record)
        {
            int days = (record.DueDate.Date - record.DateBorrowed.Date).Days;
            List<int> offsets = new List<int>();

            if (days >= 3 && days < 8)
            {
                offsets.Add(days / 2); // 1 report midway
            }
            else if (days >= 8 && days < 15)
            {
                offsets.Add(days / 3);
                offsets.Add((2 * days) / 3); // 2 reports
            }
            else if (days >= 15 && days <= 30)
            {
                if (days == 30)
                {
                    offsets.AddRange(new[] { 10, 20, 27 });
                }
                else
                {
                    offsets.Add(days / 3);
                    offsets.Add((2 * days) / 3);
                    offsets.Add(days - 3); // 3 reports
                }
            }
            else if (days > 30)
            {
                for (int offset = 10; offset < days; offset += 10)
                {
                    offsets.Add(offset);
                }
            }

            for (int i = 0; i < offsets.Count; i++)
            {
                var report = new ConditionReport
                {
                    LendingId = record.LendingId,
                    IsScheduled = true,
                    ReportNumber = i + 1,
                    ScheduledDate = record.DateBorrowed.Date.AddDays(offsets[i]),
                    Status = "Pending"
                };
                await firestoreService.AddConditionReportAsync(report);
            }
        }
    }
}
