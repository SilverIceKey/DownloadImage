using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DownloadImage.domain
{
    public class ComicModel : INotifyPropertyChanged
    {
        private string _comicUrl;
        private string _comicName;
        private int _comicPage;
        private List<string> _comicPageUrl;
        private int _curDownloadPage;
        private string _downloadStatus;
        private bool _isDownload;

        public string ComicUrl
        {
            get => _comicUrl;
            set
            {
                if (_comicUrl == value) return;
                _comicUrl = value;
                OnPropertyChanged();
            }
        }

        public string ComicName
        {
            get => _comicName;
            set
            {
                if (_comicName == value) return;
                _comicName = value;
                OnPropertyChanged();
            }
        }

        public int ComicPage
        {
            get => _comicPage;
            set
            {
                if (_comicPage == value) return;
                _comicPage = value;
                OnPropertyChanged();
            }
        }

        public int CurDownloadPage
        {
            get => _curDownloadPage;
            set
            {
                if (_curDownloadPage == value) return;
                _curDownloadPage = value;
                OnPropertyChanged();
            }
        }

        public List<string> ComicPageUrl
        {
            get => _comicPageUrl;
            set
            {
                if (_comicPageUrl == value) return;
                _comicPageUrl = value;
                OnPropertyChanged();
            }
        }

        public string DownloadStatus
        {
            get => _downloadStatus;
            set
            {
                if (_downloadStatus==value)return;
                _downloadStatus = value;
                OnPropertyChanged();
            }
        }
        public bool IsDownload
        {
            get => _isDownload;
            set
            {
                if (_isDownload == value) return;
                _isDownload = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}