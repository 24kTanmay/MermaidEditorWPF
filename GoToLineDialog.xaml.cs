using System.Windows;
using System.Windows.Input;

namespace Mermaid
{
    public partial class GoToLineDialog : Window
    {
        public int LineNumber { get; private set; } = -1;

        public GoToLineDialog()
        {
            InitializeComponent();
            LineNumberBox.Focus();
        }

        private void Go_Click(object sender, RoutedEventArgs e)
        {
            Submit();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LineNumberBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Submit();
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void Submit()
        {
            if (int.TryParse(LineNumberBox.Text, out int line))
            {
                LineNumber = line;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid line number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                LineNumberBox.SelectAll();
                LineNumberBox.Focus();
            }
        }
    }
}