using static Revathryn.Contracts.Consts;

namespace Revathryn.Contracts
{
    public sealed class RevathrynSettings
    {
        public RevathrynSettings()
        {
            string rootPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            BaseSettingsPath = Path.Combine(rootPath, DefaultProgramDataPath);
        }

        public RevathrynSettings(string baseSettingsPath)
        {
            BaseSettingsPath = baseSettingsPath;
        }

        public string BaseSettingsPath { get; }
    }
}
