using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace Downloadify
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            AddPage(new Pages.HomePage());
        }

        public void AddPage(UserControl page)
        {
            GridPage.Children.Clear();
            GridPage.Children.Add(page);
        }

        public void ButtonHome_Click(object sender, RoutedEventArgs e)
        {
            AddPage(new Pages.HomePage());
        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            AddPage(new Pages.AboutPage());
        }
    }
}