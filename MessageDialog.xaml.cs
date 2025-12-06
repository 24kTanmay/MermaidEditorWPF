using System.Windows;

namespace Mermaid
{
    public partial class MessageDialog : Window
    {
        public MessageDialog(string title, string message)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            
            // Ensure resources are available (in case it's opened before main window fully loads, though unlikely)
            if (Application.Current.MainWindow != null)
            {
                this.Resources.MergedDictionaries.Add(Application.Current.MainWindow.Resources);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}