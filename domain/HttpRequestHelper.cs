using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DownloadImage.domain
{
    class HttpRequestHelper
    {
        public interface IDownloadError
        {
            void onDownloadError(Exception exception);
        }

        public static IDownloadError listener { get; set; }
        public static string HttpPost(string Url, string postDataStr, ref bool isSuccess)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = Encoding.UTF8.GetByteCount(postDataStr);
                request.ServicePoint.Expect100Continue = false;
                //request.CookieContainer = cookie;
                Stream myRequestStream = request.GetRequestStream();
                StreamWriter myStreamWriter = new StreamWriter(myRequestStream, Encoding.GetEncoding("gb2312"));
                myStreamWriter.Write(postDataStr);
                myStreamWriter.Close();

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                //response.Cookies = cookie.GetCookies(response.ResponseUri);
                Stream myResponseStream = response.GetResponseStream();
                StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
                string retString = myStreamReader.ReadToEnd();
                myStreamReader.Close();
                myResponseStream.Close();

                return retString;
            }
            catch (Exception e)
            {
                isSuccess = false;
                Console.Write(e.Message);
                return e.Message;
            }
        }

        public static string HttpGet(string Url, string postDataStr)
        {
            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(Url + (postDataStr == "" ? "" : "?") + postDataStr);
            request.Proxy = null;
            request.Method = "GET";
            request.ContentType = "application/json";
            request.ServicePoint.Expect100Continue = false;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
            string retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();
            response.Close();
            request.Abort();

            return retString;
        }

        /// <summary> 
        /// 创建GET方式的HTTP请求 
        /// </summary> 
        //public static HttpWebResponse CreateGetHttpResponse(string url, int timeout, string userAgent, CookieCollection cookies)
        public static HttpWebResponse CreateGetHttpResponse(string url)
        {
            HttpWebRequest request = null;
            if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                //对服务端证书进行有效性校验（非第三方权威机构颁发的证书，如自己生成的，不进行验证，这里返回true）
                ServicePointManager.ServerCertificateValidationCallback =
                    new RemoteCertificateValidationCallback(CheckValidationResult);
                request = WebRequest.Create(url) as HttpWebRequest;
                request.ProtocolVersion = HttpVersion.Version10; //http版本，默认是1.1,这里设置为1.0
            }
            else
            {
                request = WebRequest.Create(url) as HttpWebRequest;
            }

            request.Method = "GET";

            //设置代理UserAgent和超时
            //request.UserAgent = userAgent;
            //request.Timeout = timeout;
            //if (cookies != null)
            //{
            //    request.CookieContainer = new CookieContainer();
            //    request.CookieContainer.Add(cookies);
            //}
            return request.GetResponse() as HttpWebResponse;
        }

        /// <summary> 
        /// 创建POST方式的HTTP请求 
        /// </summary> 
        //public static HttpWebResponse CreatePostHttpResponse(string url, IDictionary<string, string> parameters, int timeout, string userAgent, CookieCollection cookies)
        public static HttpWebResponse CreatePostHttpResponse(string url, IDictionary<string, string> parameters)
        {
            HttpWebRequest request = null;
            //如果是发送HTTPS请求 
            if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                //ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
                request = WebRequest.Create(url) as HttpWebRequest;
                //request.ProtocolVersion = HttpVersion.Version10;
            }
            else
            {
                request = WebRequest.Create(url) as HttpWebRequest;
            }

            request.Method = "POST";
            request.ContentType = "application/json";

            //设置代理UserAgent和超时
            //request.UserAgent = userAgent;
            //request.Timeout = timeout;

            //if (cookies != null)
            //{
            //    request.CookieContainer = new CookieContainer();
            //    request.CookieContainer.Add(cookies);
            //}
            //发送POST数据 
            if (!(parameters == null || parameters.Count == 0))
            {
                StringBuilder buffer = new StringBuilder();
                int i = 0;
                foreach (string key in parameters.Keys)
                {
                    if (i > 0)
                    {
                        buffer.AppendFormat("&{0}={1}", key, parameters[key]);
                    }
                    else
                    {
                        buffer.AppendFormat("{0}={1}", key, parameters[key]);
                        i++;
                    }
                }

                byte[] data = Encoding.ASCII.GetBytes(buffer.ToString());
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }

            string[] values = request.Headers.GetValues("Content-Type");
            return request.GetResponse() as HttpWebResponse;
        }

        /// <summary>
        /// 获取请求的数据
        /// </summary>
        public static string GetResponseString(HttpWebResponse webresponse)
        {
            using (Stream s = webresponse.GetResponseStream())
            {
                StreamReader reader = new StreamReader(s, Encoding.UTF8);
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// 验证证书
        /// </summary>
        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;
            return false;
        }

        private static string[] imgTypes = new string[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        /// <summary>
        /// 图片另存为
        /// </summary>
        /// <param name="url">路径</param>
        public static void ImgSave(string url, string path, ComicModel comicModel, DownloadCallback callback)
        {
            try
            {
                if (comicModel.DownloadStatus == "未开始")
                {
                    comicModel.DownloadStatus = "正在下载";
                }
                string comicName = comicModel.ComicName;
                char[] chars = Path.GetInvalidFileNameChars();
                for (int i = 0; i < chars.Length; i++)
                {
                    comicName = comicName.Replace(chars[i].ToString(), "");
                }
                string direrory = (path + "\\" + comicName + "\\").Replace("  ", "").Trim();
                if (!Directory.Exists(direrory))
                {
                    Directory.CreateDirectory(direrory);
                }
                string fileName = url.Substring(url.LastIndexOf("/") + 1) + "";
                SaveImageFromWeb(url, direrory, fileName);
                comicModel.CurDownloadPage += 1;
                callback.onPageSuccess();
                if (comicModel.CurDownloadPage == comicModel.ComicPage)
                {
                    comicModel.IsDownload = true;
                    comicModel.DownloadStatus = "下载完成";
                    callback.onSuccess(comicModel);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (e.Message.Contains("404"))
                {
                    if (url.EndsWith(".jpg"))
                    {
                        ImgSave(url.Replace(".jpg", ".png"), path, comicModel, callback);
                    }
                    else
                    {
                        ImgSave(url.Replace(".png", ".jpg"), path, comicModel, callback);
                    }

                }
                // MessageBox.Show(e.Message);
                // ImgSave(url,path,comicModel);
            }
        }

        /// <summary>
        /// 保存web图片到本地
        /// </summary>
        /// <param name="imgUrl">web图片路径</param>
        /// <param name="path">保存路径</param>
        /// <param name="fileName">保存文件名</param>
        /// <returns></returns>
        public static string SaveImageFromWeb(string imgUrl, string path, string fileName)
        {
            if (path.Equals(""))
                throw new Exception("未指定保存文件的路径");
            string imgName = imgUrl.ToString().Substring(imgUrl.ToString().LastIndexOf("/") + 1);
            string imgPath = "";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(imgUrl);
            request.Proxy = null;
            request.UserAgent = "Mozilla/6.0 (MSIE 6.0; Windows NT 5.1; Natas.Robot)";
            request.Timeout = 3000;
            request.ServicePoint.Expect100Continue = false;

            WebResponse response = request.GetResponse();
            Stream stream = response.GetResponseStream();

            if (response.ContentType.ToLower().StartsWith("image/"))
            {
                byte[] arrayByte = new byte[1024];
                int imgLong = (int)response.ContentLength;
                int l = 0;

                if (fileName == "")
                    fileName = imgName;

                FileStream fso = new FileStream(path + fileName, FileMode.Create);
                while (l < imgLong)
                {
                    int i = stream.Read(arrayByte, 0, 1024);
                    fso.Write(arrayByte, 0, i);
                    l += i;
                }

                fso.Close();
                stream.Close();
                response.Close();
                request.Abort();
                return imgPath;
            }
            else
            {
                return "";
            }
        }
    }
}