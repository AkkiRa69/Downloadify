﻿using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace Downloadify.Components
{
    /// <summary>
    /// Interaction logic for MessageBoxSuccess.xaml
    /// </summary>
    public partial class MessageBoxSuccess : Window
    {
        public MessageBoxSuccess(string meesage = "Your video has been downloaded successfully! Enjoy watching!")
        {
            InitializeComponent();
            TextMessage.Text = meesage;
        }
    }
}
