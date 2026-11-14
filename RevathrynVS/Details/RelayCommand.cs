using System.Windows.Input;

namespace RevathrynVS2026.Details
{
    /// <summary>Command that runs a delegate; its ability to execute is fixed for the lifetime of the command.</summary>
    internal sealed class RelayCommand : ICommand
    {
        #region Fields
        private readonly Action execute;

        private readonly bool canExecute;
        #endregion Fields

        #region Events
        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }
        #endregion Events

        #region Constructors
        public RelayCommand(Action execute, bool canExecute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            this.execute = execute;
            this.canExecute = canExecute;
        }

        #endregion Constructors

        #region Public Methods
        public bool CanExecute(object parameter)
        {
            return canExecute;
        }

        public void Execute(object parameter)
        {
            if (canExecute)
            {
                execute();
            }
        }

        #endregion Public Methods
    }
}
