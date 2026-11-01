namespace Revathryn.Engine
{
    /// <summary>A single hardcoded built-in review definition.</summary>
    internal sealed record ReviewTypeDefinition(string Key, string Name, string Description, int Priority, string FileExtensions);
}
