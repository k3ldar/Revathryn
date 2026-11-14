using System.Windows;
using System.Windows.Controls;

namespace RevathrynVS2026.ToolWindows
{
    internal sealed partial class ActiveFileControl : UserControl
    {
        #region Fields
        private readonly ActiveFileViewModel viewModel;
        #endregion Fields

        #region Constructors
        public ActiveFileControl(ActiveFileViewModel viewModel)
        {
            this.viewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
        }

        #endregion Constructors

        #region Private Methods
        private void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            viewModel.Refresh();
        }

        #endregion Private Methods
    }
}
