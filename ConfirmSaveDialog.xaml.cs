using System.Windows;
using System.Windows.Input;

namespace Mermaid
{
    public partial class ConfirmSaveDialog : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        public ConfirmSaveDialog(string fileName)
        {
            InitializeComponent();
            MessageText.Text = $"Do you want to save changes to {fileName}?";
            
            // Enable dragging
            this.MouseLeftButtonDown += (s, e) => this.DragMove();

            // Set initial focus to Save button so arrow keys work immediately
            this.Loaded += (s, e) => SaveButton.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void DontSave_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }
    }
}