namespace BTITPORequest.Models
{
    public class DashboardViewModel
    {
        // Filter
        public DateTime DateFrom { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        public DateTime DateTo { get; set; } = DateTime.Today;

        // Summary Cards
        public int TotalPO { get; set; }
        public decimal TotalAmount { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int RejectedCount { get; set; }
        public int DraftCount { get; set; }

        // Chart Data
        public List<DailyAmountData> DailyAmounts { get; set; } = new();
        public List<StatusSummaryData> StatusSummary { get; set; } = new();

        // Recent POs
        public List<PORequestModel> RecentPOs { get; set; } = new();

        // My Pending Actions
        public List<PORequestModel> PendingMyAction { get; set; } = new();

        public string CurrentUserSam { get; set; } = string.Empty;
        public string CurrentUserName { get; set; } = string.Empty;
    }

    public class DailyAmountData
    {
        public string Date { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    public class StatusSummaryData
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }
}
