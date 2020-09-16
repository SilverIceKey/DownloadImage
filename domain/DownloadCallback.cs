using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DownloadImage.domain
{
    interface DownloadCallback
    {
        void onSuccess(ComicModel comicModel);
        void onPageSuccess();
    }
}
