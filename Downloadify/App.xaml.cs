using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Input;

namespace Downloadify
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static MainWindow main = new MainWindow();
        public static bool isExplorerOpen = true; 
        public static string selectedSavePath = "";
        protected override void OnStartup(StartupEventArgs e)
        {
            main.Show();
        }
    }
}