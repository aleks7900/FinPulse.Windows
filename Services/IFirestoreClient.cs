using System.Collections.Generic;
using System.Threading.Tasks;
using FinPulse.Windows.Models;

namespace FinPulse.Windows.Services;

public interface IFirestoreClient
{
    Task<int> UploadRecordsAsync(string uid, string collection, List<CloudEntityRecord> records);
    Task<List<CloudEntityRecord>> DownloadRecordsAsync(string uid, string collection, long sinceTimestamp);
    Task RecordTombstoneAsync(string uid, string collection, string id, long deletedAt);
    Task<CloudUserSummary> GetUserSummaryAsync(string uid);
    Task ClearUserStorageAsync(string uid);
}
