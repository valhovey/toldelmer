using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;

namespace NinaHaTelemetry {

    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary {
        public Options() => InitializeComponent();

        private void PasswordBox_Loaded(object sender, RoutedEventArgs e) {
            if (sender is PasswordBox box && box.DataContext is HaTelemetryPlugin plugin) box.Password = plugin.Password;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
            if (sender is PasswordBox box && box.DataContext is HaTelemetryPlugin plugin && plugin.Password != box.Password)
                plugin.Password = box.Password;
        }
    }
}
