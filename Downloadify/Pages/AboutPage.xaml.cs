using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Downloadify.Pages
{
    /// <summary>
    /// Interaction logic for AboutPage.xaml
    /// </summary>
    public partial class AboutPage : UserControl
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        void link(string url) 
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true // Ensures the URL opens in the default browser
            });
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            link("http://www.antkh.com/");
        }

        private void Image_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            link("https://cbrd.gov.kh");
        }

        private void Image_MouseDown_2(object sender, MouseButtonEventArgs e)
        {
            link("https://mptc.gov.kh/");
        }

        private void StackPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            link("http://training.antkh.com/students/?s=4937");
        }
    }
}