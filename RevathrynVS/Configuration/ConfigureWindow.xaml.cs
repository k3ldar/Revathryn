using System.Windows;

using Microsoft.VisualStudio.PlatformUI;

namespace RevathrynVS2026.Configuration
{
    /// <summary>Modal window used to set the server URL.</summary>
    internal sealed partial class ConfigureWindow : DialogWindow
    {
        #region Fields
        private readonly ConfigureViewModel viewModel;
        #endregion Fields

        #region Constructors
        public ConfigureWindow(ConfigureViewModel viewModel)
        {
            this.viewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
            Title = Constants.ConfigureWindowTitle;
        }

        #endregion Constructors

        #region Private Methods
        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            if (viewModel.TryValidate())
            {
                DialogResult = true;
            }
        }

        #endregion Private Methods
    }
}
