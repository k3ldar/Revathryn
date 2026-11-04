namespace Revathryn.Data
{
    /// <summary>DDL statements for the per-repository symbol index database.</summary>
    internal static class SymbolIndexSchema
    {
        internal const string CreateFiles = """
            CREATE TABLE IF NOT EXISTS Files (
                SourcePath TEXT NOT NULL PRIMARY KEY,
                SourceHash TEXT NOT NULL DEFAULT '',
                SourceLastWriteUtc TEXT NOT NULL,
                AnalysedUtc TEXT NOT NULL,
                AnalyserVersion INTEGER NOT NULL,
                Language TEXT NOT NULL DEFAULT '',
                LanguageVersion TEXT NOT NULL DEFAULT ''
            );
            """;

        internal const string CreateSymbols = """
            CREATE TABLE IF NOT EXISTS Symbols (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                SourcePath TEXT NOT NULL,
                Name TEXT NOT NULL DEFAULT '',
                FullyQualifiedName TEXT NOT NULL DEFAULT '',
                Kind TEXT NOT NULL DEFAULT '',
                LineNumber INTEGER NOT NULL,
                EndLineNumber INTEGER NOT NULL,
                Namespace TEXT NOT NULL DEFAULT '',
                Accessibility TEXT NOT NULL DEFAULT '',
                IsStatic INTEGER NOT NULL DEFAULT 0,
                IsAbstract INTEGER NOT NULL DEFAULT 0,
                IsSealed INTEGER NOT NULL DEFAULT 0,
                DependenciesJson TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (SourcePath) REFERENCES Files(SourcePath) ON DELETE CASCADE
            );
            """;

        internal const string CreateMembers = """
            CREATE TABLE IF NOT EXISTS Members (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                SymbolId INTEGER NOT NULL,
                Kind TEXT NOT NULL DEFAULT '',
                Name TEXT NOT NULL DEFAULT '',
                Signature TEXT NOT NULL DEFAULT '',
                LineNumber INTEGER NOT NULL,
                EndLineNumber INTEGER NOT NULL,
                Accessibility TEXT NOT NULL DEFAULT '',
                IsStatic INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (SymbolId) REFERENCES Symbols(Id) ON DELETE CASCADE
            );
            """;

        internal const string CreateIndexes = """
            CREATE INDEX IF NOT EXISTS IX_Symbols_SourcePath ON Symbols(SourcePath);
            CREATE INDEX IF NOT EXISTS IX_Symbols_FullyQualifiedName ON Symbols(FullyQualifiedName);
            CREATE INDEX IF NOT EXISTS IX_Symbols_Name ON Symbols(Name);
            CREATE INDEX IF NOT EXISTS IX_Members_SymbolId ON Members(SymbolId);
            """;
    }
}
