namespace Revathryn.Contracts.Models
{
    /// <summary>Current process resource metrics displayed in the system summary.</summary>
    public sealed class SystemResourceMetrics
    {
        /// <summary>Approximate number of bytes currently allocated on the managed heap.</summary>
        public long ManagedHeapBytes { get; set; }

        /// <summary>Total number of bytes committed by the garbage collector.</summary>
        public long GcCommittedBytes { get; set; }

        /// <summary>Total number of bytes allocated since the process started.</summary>
        public long TotalAllocatedBytes { get; set; }

        /// <summary>Current working set size of the process in bytes.</summary>
        public long WorkingSetBytes { get; set; }

        /// <summary>Private memory size of the process in bytes.</summary>
        public long PrivateMemoryBytes { get; set; }

        /// <summary>Number of threads currently in the thread pool.</summary>
        public int ThreadPoolThreadCount { get; set; }

        /// <summary>Number of work items currently queued in the thread pool.</summary>
        public long ThreadPoolPendingWorkItemCount { get; set; }

        /// <summary>Average process CPU usage as a percentage of elapsed uptime.</summary>
        public double CpuUsagePercent { get; set; }
    }
}
