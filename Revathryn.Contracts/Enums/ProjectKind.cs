namespace Revathryn.Contracts.Enums
{
    /// <summary>Identifies the kind of project structure discovered within a repository.</summary>
    public enum ProjectKind
    {
        /// <summary>A solution file such as .sln or .slnx.</summary>
        Solution = 0,

        /// <summary>A project file such as .csproj.</summary>
        Project = 1
    }
}
