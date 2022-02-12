using System;
using System.IO;
using System.Threading;

namespace DuplicateFinder.DirectoryCrawler
{
    public class FileInfoProvider
    {
        public event FileInfoEventHandler FileDone, AllFilesRead, FileProgressed;

        private Thread m_tdInfo;
        
        private int
            m_HoleProgress = 0, 
            m_FileProgress = 0;
        
        private string m_CurrentFile;
        
        private string[] m_Item;

        public int HoleProgress
        {
            get
            {
                return m_HoleProgress;
            }
        }

        public string[] Item
        {
            get
            {
                return m_Item;
            }
        }

        public string CurrentFile
        {
            get
            {
                return m_CurrentFile;
            }
        }

        public int FileProgress
        {
            get
            {
                return m_FileProgress;
            }
        }

        public void PopulateInfos(FileInfo[] files)
        {
#if DEBUG
            populateInfos(files);
#else

            m_tdInfo = new Thread(new ParameterizedThreadStart(populateInfos));
            m_tdInfo.IsBackground = true;
            m_tdInfo.Priority = ThreadPriority.BelowNormal;
            m_tdInfo.Start(files);
#endif
        }

        private void populateInfos(object tmpfiles)
        {
            string file = string.Empty;
            string md5;
            System.IO.FileInfo fileInfo;
            m_Item = new string[5];

            int len = ((FileInfo[])tmpfiles).Length;

            Hashing.MD5.HashFinished += new DuplicateFinder.Hashing.HashEventHandler(MD5_HashFinished);
            Hashing.MD5.HashProgressed += new DuplicateFinder.Hashing.HashEventHandler(MD5_HashProgressed);

            for (int i = 0; i < len; i++)
            {
                m_HoleProgress = (i + 1) * 100 / len;
                fileInfo = ((FileInfo[])tmpfiles)[i];
                m_CurrentFile = fileInfo.FullName;
                file = fileInfo.FullName;
                int chunk = 7 * 1024 * 1024;
                if (fileInfo.Length / 50 > chunk)
                    chunk = (int)(fileInfo.Length / 50);
                md5 = Hashing.MD5.MD5HashFile(file, chunk);
                m_Item[0] = fileInfo.Name;
                m_Item[1] = SpaceThousands(fileInfo.Length);
                m_Item[2] = fileInfo.Extension.Replace(".", string.Empty).ToUpper() + " File";
                m_Item[3] = md5;
                m_Item[4] = fileInfo.DirectoryName;
                OnFileDone(new FileInfoEventArgs(m_Item, m_HoleProgress, i));
            }
            OnAllFilesDone(new FileInfoEventArgs());
        }

        private string SpaceThousands(long size)
        {
            System.Globalization.NumberFormatInfo nfi = new System.Globalization.CultureInfo("en-US", false).NumberFormat;
            nfi.NumberGroupSeparator = " ";
            nfi.NumberDecimalDigits = 0;
            return size.ToString("N", nfi);
        }

        private void MD5_HashProgressed(object sender, Hashing.HashEventArgs e)
        {
            m_FileProgress = e.Progress;
            OnFileProgressed(new FileInfoEventArgs(e.Filename, e.Progress, e.Position, e.FileSize, -1));
        }

        private void MD5_HashFinished(object sender, Hashing.HashEventArgs e)
        {
            OnFileDone(new FileInfoEventArgs(e.Filename, e.Progress, -1));
        }

        protected virtual void OnFileProgressed(FileInfoEventArgs e)
        {
            if (FileProgressed != null)
                FileProgressed(this, e);
        }

        protected virtual void OnFileDone(FileInfoEventArgs e)
        {
            if (FileDone != null)
                FileDone(this, e);
        }

        protected virtual void OnAllFilesDone(FileInfoEventArgs e)
        {
            if (AllFilesRead != null)
                AllFilesRead(this, e);
        }

    }
}