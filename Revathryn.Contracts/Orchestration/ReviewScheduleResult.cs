namespace Revathryn.Contracts.Orchestration
{
    /// <summary>Outcome of a review scheduling operation.</summary>
    public class ReviewScheduleResult
    {
        /// <summary>Number of queue entries that were newly added.</summary>
        public int Added { get; set; }

        /// <summary>Number of insertions skipped because an equivalent entry was already pending.</summary>
        public int DuplicatesSkipped { get; set; }
    }
}
