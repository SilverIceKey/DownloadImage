using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using HtmlAgilityPack;
using MaterialDesignThemes.Wpf;
using NPOI.SS.UserModel;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using MessageBox = System.Windows.MessageBox;

namespace DownloadImage.domain
{
    /// <summary>
    /// BookDownload.xaml 的交互逻辑
    /// </summary>
    public partial class BookDownload : Page, HttpRequestHelper.IDownloadError
    {
        private string BookDownloadPath;
        private string ExcelPath;
        public List<ComicModel> XlsData = new List<ComicModel>();
        private FileStream fs = null;
        private IWorkbook workbook = null;
        private IniUtils configUtils = new IniUtils(AppDomain.CurrentDomain.BaseDirectory+"config.ini");
        private IniUtils downloadUtils = new IniUtils(AppDomain.CurrentDomain.BaseDirectory+"download.ini");
        private CancellationTokenSource cancellationToken = new CancellationTokenSource();
        public BookDownload()
        {
            InitializeComponent();
            Dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
            if (!string.IsNullOrEmpty(configUtils.IniReadvalue("DownloadPath","Path")))
            {
                BookDownloadPath = configUtils.IniReadvalue("DownloadPath", "Path");
                DownloadPath.Text = configUtils.IniReadvalue("DownloadPath", "Path");
            }
        }

        private void SelectDownloadPath_OnClick(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog openFileDialog = new FolderBrowserDialog(); //选择文件夹

            if (openFileDialog.ShowDialog() == DialogResult.OK
            ) //注意，此处一定要手动引入System.Window.Forms空间，否则你如果使用默认的DialogResult会发现没有OK属性
            {
                configUtils.IniWritevalue("DownloadPath","Path", openFileDialog.SelectedPath);
                DownloadPath.Text = openFileDialog.SelectedPath;
                BookDownloadPath = openFileDialog.SelectedPath;
            }
        }

        private void SelectExcel_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.CheckFileExists = true;
            openFileDialog.Filter = "Excel或文本（*.xls;*.xlsx )|*.xls;*.xlsx;*.txt|All files(*.*)|*.* ";
            if (openFileDialog.ShowDialog() == DialogResult.OK
            ) //注意，此处一定要手动引入System.Window.Forms空间，否则你如果使用默认的DialogResult会发现没有OK属性
            {
                ExcelPath = openFileDialog.FileName;
                ExcelPathName.Text = ExcelPath;
                Thread thread = new Thread(checkData);
                thread.Start();
                StackPanel.Visibility = Visibility.Visible;
                openFileDialog.Dispose();
            }
        }

        private string getTitle(string url)
        {
            string title = "";
            string html = HttpRequestHelper.HttpGet(url, "");
            if (url.StartsWith("https://zh.nyahentai.fun/"))
            {
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(html);
                HtmlNode htmlNode = document.DocumentNode.SelectSingleNode("//div[@id='info']/h1");
                title = htmlNode.InnerText.Replace(" - Page 2", "").Replace("[中国翻訳]", "").Replace("[DL版]", "");
                Regex regex = new Regex(@"\(C\d{2,4}\)");
                Regex regex2 = new Regex(@"\(COMIC\d{0,2}\W{0,1}\d{0,4}\)");
                if (regex.Match(title).Success)
                {
                    string Result = regex.Match(title).ToString();
                    title = title.Replace(Result, "");
                }
                if (regex2.Match(title).Success)
                {
                    string Result = regex2.Match(title).ToString();
                    title = title.Replace(Result, "");
                }
            }
            else if (url.StartsWith("https://nhentai.net"))
            {
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(html);
                HtmlNode htmlNode = document.DocumentNode.SelectSingleNode("//span[@class='pretty']");
                title = htmlNode.InnerText;
                Regex regex = new Regex(@"\(C\d{2,4}\)");
                Regex regex2 = new Regex(@"\(COMIC\d{0,2}\W{0,1}\d{0,4}\)");
                if (regex.Match(title).Success)
                {
                    string Result = regex.Match(title).ToString();
                    title = title.Replace(Result, "");
                }
                if (regex2.Match(title).Success)
                {
                    string Result = regex2.Match(title).ToString();
                    title = title.Replace(Result, "");
                }
            }

            return title.Trim();
        }

        private int getComicPage(string url, HtmlDocument document)
        {
            int comicPage = 0;
            if (url.StartsWith("https://zh.nyahentai.fun/"))
            {
                HtmlNode htmlNode = document.DocumentNode.SelectSingleNode("//span[@class='num-pages']");
                comicPage = Int32.Parse(htmlNode.InnerText);
            }
            else if (url.StartsWith("https://nhentai.net"))
            {
                HtmlNode htmlNode = document.DocumentNode.SelectSingleNode("//div[@class='thumbs']");
                comicPage = htmlNode.ChildNodes.Count;
            }

            return comicPage;
        }

        private List<string> getComicPageUrl(string url, HtmlDocument document)
        {
            List<string> comicPageUrl = new List<string>();
            if (url.StartsWith("https://zh.nyahentai.fun/"))
            {
                HtmlNode htmlNode = document.DocumentNode.SelectSingleNode("//span[@class='num-pages']");
                HtmlNode imgSrcNode =
                    document.DocumentNode.SelectSingleNode("//img[@class='current-img fit-horizontal']");
                string imgSrcUrl = imgSrcNode.Attributes["src"].Value;
                string ext = imgSrcUrl.Substring(imgSrcUrl.LastIndexOf(".") + 1);
                int count = Int32.Parse(htmlNode.InnerText);
                for (int i = 1; i <= count; i++)
                {
                    comicPageUrl.Add(imgSrcUrl.Replace("1." + ext, i + "." + ext));
                }
            }
            else if (url.StartsWith("https://nhentai.net"))
            {
                HtmlNode htmlNode = document.DocumentNode.SelectSingleNode("//div[@class='thumbs']");
                int PageNum = htmlNode.ChildNodes.Count;
                HtmlDocument srcDocument = new HtmlDocument();
                srcDocument.LoadHtml(HttpRequestHelper.HttpGet(url + "1/", ""));
                HtmlNode srcNode = srcDocument.DocumentNode.SelectSingleNode("//section[@id='image-container']/a/img");
                string srcUrl = srcNode.Attributes["src"].Value;
                string ext = srcUrl.Substring(srcUrl.LastIndexOf(".") + 1);
                for (int i = 1; i <= PageNum; i++)
                {
                    comicPageUrl.Add(srcUrl.Replace("1." + ext, i + "." + ext));
                }
            }

            return comicPageUrl;
        }

        private void checkData()
        {
            try
            {
                List<ComicModel> datas = new List<ComicModel>();
                if (ExcelPath.EndsWith(".txt"))
                {
                    string[] paths = File.ReadAllLines(ExcelPath, Encoding.Default);
                    for (int i = 0; i < paths.Length; i++)
                    {
                        Int64 startTime = TimeUtils.GetTimeStamp();
                        ComicModel comicModel = new ComicModel();
                        string ComicUrl = paths[i];
                        HtmlDocument document = new HtmlDocument();
                        LogOutWrite("漫画信息", "链接：" + ComicUrl + " 解析开始");
                        string html = HttpRequestHelper.HttpGet(ComicUrl, "");
                        if (ComicUrl.StartsWith("https://zh.nyahentai.fun/"))
                        {
                            html = HttpRequestHelper.HttpGet(ComicUrl + "list/1/", "");
                        }

                        document.LoadHtml(html);
                        comicModel.ComicName = getTitle(ComicUrl);
                        comicModel.ComicUrl = ComicUrl;
                        comicModel.ComicPage = getComicPage(ComicUrl, document);
                        comicModel.ComicPageUrl = getComicPageUrl(ComicUrl, document);
                        if (string.IsNullOrEmpty(downloadUtils.IniReadvalue(comicModel.ComicUrl, "curDownloadPage")))
                        {
                            comicModel.CurDownloadPage = 0;
                        }
                        else
                        {
                            comicModel.CurDownloadPage = Int32.Parse(downloadUtils.IniReadvalue(comicModel.ComicUrl, "curDownloadPage"));
                        }
                        comicModel.IsDownload = comicModel.ComicPage == comicModel.CurDownloadPage;
                        comicModel.DownloadStatus = "未开始";
                        LogOutWrite("漫画信息", "链接：" + ComicUrl + " 漫画名称：" + comicModel.ComicName + " 漫画页码：" + comicModel.ComicPage+1 + " 解析时间：" + (TimeUtils.GetTimeStamp() - startTime) + "ms");
                        datas.Add(comicModel);
                    }
                }
                else
                {
                    fs = new FileStream(ExcelPath, FileMode.Open, FileAccess.Read);
                    workbook = WorkbookFactory.Create(fs);
                    ISheet sheet = workbook.GetSheetAt(0);
                    if (sheet != null)
                    {
                        int rowNum = sheet.LastRowNum;
                        for (int i = 1; i <= rowNum; i++)
                        {
                            IRow firstRow = sheet.GetRow(i);
                            int cellCount = firstRow.LastCellNum; //一行最后一个cell的编号，即总的列数
                            for (int j = 0; j < cellCount; j++)
                            {
                                long startTime = TimeUtils.GetTimeStamp();
                                ComicModel comicModel = new ComicModel();
                                string ComicUrl = firstRow.Cells[j].ToString();
                                HtmlDocument document = new HtmlDocument();
                                LogOutWrite("漫画信息", "链接：" + ComicUrl + " 解析开始");
                                if (ComicUrl.StartsWith("https://zh.nyahentai.fun/"))
                                {
                                    ComicUrl = ComicUrl + "list/1/";
                                }
                                document.LoadHtml(HttpRequestHelper.HttpGet(ComicUrl, ""));
                                comicModel.ComicName = getTitle(ComicUrl);
                                comicModel.ComicUrl = ComicUrl;
                                comicModel.ComicPage = getComicPage(ComicUrl, document);
                                LogOutWrite("漫画信息", "链接："+ ComicUrl+" 漫画名称："+comicModel.ComicName +" 漫画页码："+ comicModel.ComicPage + " 解析时间：" + (TimeUtils.GetTimeStamp() - startTime) + "ms");
                                comicModel.ComicPageUrl = getComicPageUrl(ComicUrl, document);
                                int curdownloadpage = 0;
                                if (string.IsNullOrEmpty(downloadUtils.IniReadvalue(comicModel.ComicUrl, "curDownloadPage")))
                                {
                                    comicModel.CurDownloadPage = 0;
                                }
                                else
                                {
                                    comicModel.CurDownloadPage = Int32.Parse(downloadUtils.IniReadvalue(comicModel.ComicUrl, "curDownloadPage"));
                                }
                                comicModel.IsDownload = comicModel.ComicPage == comicModel.CurDownloadPage;
                                comicModel.DownloadStatus = "未开始";
                                datas.Add(comicModel);
                            }
                        }
                    }
                }
                this.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    ListView.ItemsSource = datas;
                    XlsData = datas;
                    StackPanel.Visibility = Visibility.Hidden;
                });
            }
            catch (Exception ex) //打印错误信息
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void StartDownload_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(BookDownloadPath))
            {
                MessageBox.Show("请选择下载路径");
                return;
            }

            if (string.IsNullOrEmpty(ExcelPath))
            {
                MessageBox.Show("请选择Excel文件");
                return;
            }

            StartDownloadText.Text = "下载中";
            HttpRequestHelper.listener = this;
            Thread downloadThread = new Thread(downloadBook);
            downloadThread.Start();
            StartDownload.IsEnabled = false;
            SelectDownloadPath.IsEnabled = false;
            SelectExcel.IsEnabled = false;
        }

        private void LogOutWrite(string tag,string log)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                LogOut.Text += tag + ":" + log + "\r\n";
                LogLayout.ScrollToBottom();
            });
        }

        private void downloadBook()
        {
            for (int i = 0; i < XlsData.Count; i++)
            {
                if (!XlsData[i].IsDownload)
                {
                    CancellationToken token = cancellationToken.Token;
                    TaskFactory taskFactory = new TaskFactory(token);
                    taskFactory.StartNew(ThreadDownload, XlsData[i]);
                }
            }

            while (!isAllDownload())
            {
            }

            this.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart) delegate()
            {
                for (int i = 0; i < XlsData.Count; i++)
                {
                    downloadUtils.IniWritevalue(XlsData[i].ComicUrl, "curDownloadPage", XlsData[i].CurDownloadPage.ToString());
                }
                MessageBox.Show("下载完成");
                StartDownloadText.Text = "下载完成";
                StartDownload.IsEnabled = true;
                SelectDownloadPath.IsEnabled = true;
                SelectExcel.IsEnabled = true;
            });
        }

        private bool isAllDownload()
        {
            for (int i = 0; i < XlsData.Count; i++)
            {
                if (!XlsData[i].IsDownload)
                {
                    return false;
                }
            }

            return true;
        }

        private void ThreadDownload(object comicModel)
        {
            ComicModel downloadModel = (ComicModel)comicModel;
            try
            {
                while (!downloadModel.IsDownload)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancellationToken.Token.ThrowIfCancellationRequested();
                    }
                    LogOutWrite("下载信息", "链接：" + downloadModel.ComicPageUrl[downloadModel.CurDownloadPage] + " 漫画名称：" + downloadModel.ComicName + " 当前页码：" + (downloadModel.CurDownloadPage+1));
                    HttpRequestHelper.ImgSave(downloadModel.ComicPageUrl[downloadModel.CurDownloadPage],
                        BookDownloadPath, downloadModel);
                }
            }
            catch (Exception e)
            {
                LogOutWrite("漫画信息", "链接：" + downloadModel.ComicPageUrl[downloadModel.CurDownloadPage] + " 漫画名称：" + downloadModel.ComicName + " 错误信息：" + e.Message);
                Console.WriteLine(e.Message);
            }
            
        }

        public void onDownloadError(Exception exception)
        {
            StartDownload.IsEnabled = true;
        }

        private void OnDispatcherShutdownStarted(object sender, EventArgs e)
        {
            for (int i = 0; i < XlsData.Count; i++)
            {
                downloadUtils.IniWritevalue(XlsData[i].ComicUrl, "curDownloadPage",XlsData[i].CurDownloadPage.ToString());
            }
            cancellationToken.Cancel();
        }
    }
}