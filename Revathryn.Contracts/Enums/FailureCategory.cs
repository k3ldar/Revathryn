namespace Revathryn.Contracts.Enums
{
    /// <summary>Classifies a failure so its handling can be determined consistently.</summary>
    public enum FailureCategory
    {
        /// <summary>A temporary condition that may succeed when retried.</summary>
        Transient = 0,

        /// <summary>A persistent condition that will not succeed without a change or intervention.</summary>
        Permanent = 1,

        /// <summary>An environmental condition that requires correction outside the application.</summary>
        Environmental = 2,

        /// <summary>An invalid or incomplete configuration that requires correction.</summary>
        Configuration = 3,

        /// <summary>Persisted data is inconsistent or corrupt and must be investigated.</summary>
        DataIntegrity = 4,

        /// <summary>An unexpected programming or runtime defect that must fail safely.</summary>
        Unexpected = 5
    }
}
