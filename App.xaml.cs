using System.Configuration;
using System.Data;
using System.Windows;
using System.Reflection;

namespace Mermaid
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string? StartupFile = null;

        public App()
        {
            // Fix for context menus opening right-aligned (left-handed)
            try
            {
                var menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
                if (menuDropAlignmentField != null)
                {
                    menuDropAlignmentField.SetValue(null, false);
                }
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length > 0)
            {
                StartupFile = e.Args[0];
            }
        }
    }
}
