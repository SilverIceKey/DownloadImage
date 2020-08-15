using System;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using DownloadImage.domain;

namespace DownloadImage
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Page BookDownloadPage = new BookDownload();
        private Page ImageDownloadPage = new ImageDownload();
        public MainWindow()
        {
            InitializeComponent();
            PageContent.Content = new Frame()
            {
                Content = BookDownloadPage
            };
        }

        private void Menu_OnClick(object sender, RoutedEventArgs e) => ListView.Focusable = true;

        private void UIElement_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            int index = ((ListView) sender).SelectedIndex;
            if (index == 0)
            {
                PageContent.Content = new Frame()
                {
                    Content = BookDownloadPage
                };
            }
            else
            {
                PageContent.Content = new Frame()
                {
                    Content = ImageDownloadPage
                };
            }

            Menu.IsChecked = false;
        }
    }
}
