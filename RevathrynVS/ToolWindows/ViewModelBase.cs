using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RevathrynVS2026.ToolWindows
{
    /// <summary>Base class that implements <see cref="INotifyPropertyChanged"/>.</summary>
    internal abstract class ViewModelBase : INotifyPropertyChanged
    {
        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion Events

        #region Protected Methods
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        #endregion Protected Methods
    }
}
