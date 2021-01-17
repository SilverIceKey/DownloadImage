using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
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
using DragEventArgs = System.Windows.DragEventArgs;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using MessageBox = System.Windows.MessageBox;

namespace DownloadImage.domain
{
    /// <summary>
    /// BookDownload.xaml 的交互逻辑
    /// </summary>
    public partial class BookDownload : Page, HttpRequestHelper.IDownloadError, DownloadCallback
    {
        private string BookDownloadPath;
        private string ExcelPath;
        public List<ComicModel> XlsData = new List<ComicModel>();
        private FileStream fs = null;
        private IWorkbook workbook = null;
        private IniUtils configUtils = new IniUtils(AppDomain.CurrentDomain.BaseDirectory + "config.ini");
        private IniUtils downloadUtils = new IniUtils(AppDomain.CurrentDomain.BaseDirectory + "download.ini");
        private CancellationTokenSource cancellationToken = new CancellationTokenSource();
        private Thread checkThread;
        public BookDownload()
        {
            InitializeComponent();
            Dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
            if (!string.IsNullOrEmpty(configUtils.IniReadvalue("DownloadPath", "Path")))
            {
                BookDownloadPath = configUtils.IniReadvalue("DownloadPath", "Path");
                DownloadPath.Text = configUtils.IniReadvalue("DownloadPath", "Path"); 
                if (!new DirectoryInfo(BookDownloadPath).Exists)
                {
                    SelectDownloadPath_OnClick(null, null);
                }
            }
            else
            { 
                SelectDownloadPath_OnClick(null, null);
            }

            
        }

        private void SelectDownloadPath_OnClick(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog openFileDialog = new FolderBrowserDialog(); //选择文件夹

            if (openFileDialog.ShowDialog() == DialogResult.OK
            ) //注意，此处一定要手动引入System.Window.Forms空间，否则你如果使用默认的DialogResult会发现没有OK属性
            {
                configUtils.IniWritevalue("DownloadPath", "Path", openFileDialog.SelectedPath);
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
                checkThread = new Thread(checkData);
                checkThread.Start();
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
                HtmlNode htmlNode = document.DocumentNode.SelectSingleNode("//div[@id='info']/h2/span[@class='pretty']");
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
            char[] chars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < chars.Length; i++)
            {
                title = title.Replace(chars[i].ToString(), "");
            }
            title = title.Replace("  ", "");
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
                srcDocument.LoadHtml(HttpRequestHelper.HttpGet((url.EndsWith("/")?url:(url+"/")) + "1/", ""));
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
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
               {
                   StartDownload.IsEnabled = false;
                   SelectDownloadPath.IsEnabled = false;
                   SelectExcel.IsEnabled = false;
               });
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
                        comicModel.CurDownloadPage = getCurrentDownloadPages(BookDownloadPath, comicModel.ComicName);
                        comicModel.IsDownload = comicModel.ComicPage == comicModel.CurDownloadPage;
                        comicModel.DownloadStatus = comicModel.ComicPage == comicModel.CurDownloadPage ? "下载完成" : "未开始";
                        LogOutWrite("漫画信息", "链接：" + ComicUrl + " 漫画名称：" + comicModel.ComicName + " 漫画页码：" + comicModel.ComicPage + " 解析时间：" + (TimeUtils.GetTimeStamp() - startTime) + "ms");
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
                                LogOutWrite("漫画信息", "链接：" + ComicUrl + " 漫画名称：" + comicModel.ComicName + " 漫画页码：" + comicModel.ComicPage + " 解析时间：" + (TimeUtils.GetTimeStamp() - startTime) + "ms");
                                comicModel.ComicPageUrl = getComicPageUrl(ComicUrl, document);
                                comicModel.CurDownloadPage = getCurrentDownloadPages(BookDownloadPath, comicModel.ComicName);
                                comicModel.IsDownload = comicModel.ComicPage == comicModel.CurDownloadPage;
                                comicModel.DownloadStatus = comicModel.ComicPage == comicModel.CurDownloadPage ? "下载完成" : "未开始";
                                datas.Add(comicModel);
                            }
                        }
                    }
                }
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    ListView.ItemsSource = datas;
                    XlsData = datas;
                    StackPanel.Visibility = Visibility.Hidden;
                    StartDownload.IsEnabled = true;
                    SelectDownloadPath.IsEnabled = true;
                    SelectExcel.IsEnabled = true;
                    if (isAllDownload())
                    {
                        MessageBox.Show("已经全部下载完成");
                        StartDownloadText.Text = "已完成";
                        StartDownload.IsEnabled = false;
                        SelectDownloadPath.IsEnabled = true;
                        SelectExcel.IsEnabled = true;
                    }
                });

            }
            catch (Exception ex) //打印错误信息
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
               {
                   MessageBox.Show(ex.Message);
                   checkData();
                   StartDownload.IsEnabled = false;
                   SelectDownloadPath.IsEnabled = true;
                   SelectExcel.IsEnabled = true;
               });
            }
        }

        public int getCurrentDownloadPages(string path,string comicName)
        {
            DirectoryInfo[] directoryInfos = new DirectoryInfo(path).GetDirectories();
            int CurrentDownloadPages = 0;
            foreach (var directoryInfo in directoryInfos)
            {
                if (directoryInfo.GetDirectories().Length!=0)
                {
                    CurrentDownloadPages = getCurrentDownloadPages(directoryInfo.FullName, comicName);
                    if (CurrentDownloadPages != 0)
                    {
                        return CurrentDownloadPages;
                    }
                }
                //LogOutWrite("目录与漫画名字", directoryInfo.Name+":"+ comicName);
                if (directoryInfo.Name==comicName)
                {
                    return directoryInfo.GetFiles().Length;
                }
            }

            return CurrentDownloadPages;
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

        private void LogOutWrite(string tag, string log)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                LogOut.Text += tag + ":" + log + "\r\n";
                LogLayout.ScrollToBottom();
            });
        }

        private volatile int CurrentBook = 0;
        private void downloadBook()
        {
            // foreach (ComicModel comicModel in XlsData)
            // {
            //     if (!comicModel.IsDownload)
            //     {
            //         // TaskFactory taskFactory = new TaskFactory(token);
            //         // taskFactory.StartNew(ThreadDownload, XlsData[i]);
            //         ThreadPool.QueueUserWorkItem(o => ThreadDownload(token, comicModel));
            //     }
            // }

            while (!isAllDownload()&& CurrentBook < XlsData.Count)
            {
                if (!XlsData[CurrentBook].IsDownload && "未开始".Equals(XlsData[CurrentBook].DownloadStatus))
                {
                    CancellationToken token = cancellationToken.Token;
                    try
                    {
                        ThreadPool.QueueUserWorkItem(o => ThreadDownload(token, XlsData[CurrentBook]));
                    }
                    catch(Exception e)
                    {

                    }
                }
            }

            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                for (int i = 0; i < XlsData.Count; i++)
                {
                    downloadUtils.IniWritevalue(XlsData[i].ComicUrl, "curDownloadPage", XlsData[i].CurDownloadPage.ToString());
                }
                MessageBox.Show("下载完成");
                StartDownloadText.Text = "下载完成";
                StartDownload.IsEnabled = false;
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

        private int MaxDownloadNum = 4;
        private volatile int CurrentDownloadNum = 0;
        private void ThreadDownload(CancellationToken token, object comicModel)
        {
            lock (this)
            {
                ComicModel downloadModel = (ComicModel)comicModel;
                try
                {
                    while (!downloadModel.IsDownload)
                    {
                        if (CurrentDownloadNum == MaxDownloadNum)
                        {
                            continue;
                        }
                        if (token.IsCancellationRequested)
                        {
                            token.ThrowIfCancellationRequested();
                            break;
                        }

                        CurrentDownloadNum++;
                        LogOutWrite("下载信息", "链接：" + downloadModel.ComicPageUrl[downloadModel.CurDownloadPage] + " 漫画名称：" + downloadModel.ComicName + " 当前页码：" + (downloadModel.CurDownloadPage + 1));
                        HttpRequestHelper.ImgSave(downloadModel.ComicPageUrl[downloadModel.CurDownloadPage],
                            BookDownloadPath, downloadModel, this);
                    }
                }
                catch (Exception e)
                {
                    //LogOutWrite("漫画信息", "链接：" + downloadModel.ComicPageUrl[downloadModel.CurDownloadPage] + " 漫画名称：" + downloadModel.ComicName + " 错误信息：" + e.Message);
                    Console.WriteLine(e.Message);
                }
            }
        }

        public void onPageSuccess()
        {
            CurrentDownloadNum--;
        }

        public void onSuccess(ComicModel comicModel)
        {
            if (CurrentBook < XlsData.Count - 1)
            {
                CurrentBook++;
            }
            LogOutWrite("下载信息", "链接：" + comicModel.ComicPageUrl[comicModel.CurDownloadPage] + " 漫画名称：" + comicModel.ComicName + " 下载完成");
        }

        public void onDownloadError(Exception exception)
        {
            StartDownload.IsEnabled = true;
        }

        private void OnDispatcherShutdownStarted(object sender, EventArgs e)
        {
            if (checkThread != null)
            {
                checkThread.Abort();
            }

            if (cancellationToken != null)
            {
                cancellationToken.Cancel();
            }
            // for (int i = 0; i < XlsData.Count; i++)
            // {
            //     downloadUtils.IniWritevalue(XlsData[i].ComicUrl, "curDownloadPage", XlsData[i].CurDownloadPage.ToString());
            // }
        }

        private void ListView_OnDrop(object sender, DragEventArgs e)
        {
            try
            {
                string file = (e.Data.GetData(System.Windows.DataFormats.FileDrop, false) as string[])[0];
                ExcelPath = file;
                ExcelPathName.Text = ExcelPath;
                checkThread = new Thread(checkData);
                checkThread.Start();
                StackPanel.Visibility = Visibility.Visible;
            }
            catch (Exception e1)
            {

            }
        }
    }
}