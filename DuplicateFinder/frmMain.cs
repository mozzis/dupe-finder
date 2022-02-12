using System;
using System.IO;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;

namespace DuplicateFinder
{

	public partial class frmMain : Form
	{
		#region Global Variables

		private DirectoryCrawler.DirectoryCrawler DCSearch;
		private DirectoryCrawler.FileInfoProvider DCFileInfo;
		private FileInfo[] m_fileInfos;
		private Hashtable[] groupTables;
		private int
			 prg = 0,
			 len;

		private List<ListViewItem> filesToDelete;
		private List<string[]> alFiles = new List<string[]>(12);
		private List<string> undeletable = new List<string>();
		private string
			 firstFolder,
			 m_deleteFolder = @"D:\DuplicateFiles";

		private Thread m_tdDELETE;
		private bool m_bErase = false;

		#endregion

		public frmMain()
		{
			InitializeComponent();
		}

		#region MyMethods
		private void MoveToRecycleBin(string file)
		{
			Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file,
				 Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
				 Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

		}

		private void DeleteAllToRecycleBin()
		{
			int nItems = lstFiles.Items.Count;
			for (int i = 0; i < nItems; i++)
			{
				ListViewItem it = lstFiles.Items[i];
				if (null != it.Tag && (bool)it.Tag == true)
				{
					MoveToRecycleBin(it.SubItems[4].Text + "\\" + it.SubItems[0].Text);
					deleteItem(it);
				}
			}
		}

		private string SpaceThousands(long valu)
		{
			System.Globalization.NumberFormatInfo nfi = new System.Globalization.CultureInfo("en-US", false).NumberFormat;
			nfi.NumberGroupSeparator = " ";
			nfi.NumberDecimalDigits = 0;
			return valu.ToString("N", nfi);
		}

		private void SearchFiles()
		{
			long[] limits = ParseMinMaxSizes();

			long
				 skipLessThan = limits[0],
				 skipMoreThan = limits[1];


			string
				 sExt = txtExtension.Text,
				 sFolder = txtFolder.Text;

			m_fileInfos = null;
			alFiles = new List<string[]>();
			DCSearch = new DirectoryCrawler.DirectoryCrawler(sFolder, sExt);
			firstFolder = DCSearch.PathOne.ToLower();
			DCSearch.DeleteToFolder = m_deleteFolder;
			DCSearch.FolderChanged += new EventHandler(DCSearch_FolderChanged);
			DCSearch.FoldersDone += new EventHandler(DCSearch_FoldersDone);
			setText(this, "Duplicate Finder - Searching in FOLDERS");
			DCSearch.Crawl(skipLessThan, skipMoreThan);
		}

		private long[] ParseMinMaxSizes()
		{
			long
				 min = 0,
				 max = long.MaxValue;

			int
				 multiL = 1,
				 multiM = 1;


			if (nmMin.Value != 0)
			{
				if (rdbLUnit.Checked)
					multiL = 1;
				else if (rdbLKilo.Checked)
					multiL = 1024;
				else
					multiL = 1024 * 1024;

				min = (long)nmMin.Value * multiL;
			}

			if (nmMax.Value != 0)
			{
				if (rdbMUnit.Checked)
					multiM = 1;
				else if (rdbMKilo.Checked)
					multiM = 1024;
				else
					multiM = 1024 * 1024;

				max = (long)nmMax.Value * multiM;
			}
			return new long[] { min, max };
		}

		private FileInfo[] SameSize(FileInfo[] filesToCompare)
		{
			int len = filesToCompare.Length;
			List<long> alIdx = new List<long>();

			System.Collections.Hashtable HLengths = new System.Collections.Hashtable();

			foreach (FileInfo fileInfo in filesToCompare)
			{
				if (!HLengths.Contains(fileInfo.Length))
					HLengths.Add(fileInfo.Length, 1);

				else
					HLengths[fileInfo.Length] = (int)HLengths[fileInfo.Length] + 1;
			}

			foreach (DictionaryEntry hash in HLengths)
				if ((int)hash.Value == 1)
				{
					alIdx.Add((long)hash.Key);
					setText(stsMain, string.Format("Will remove File with size {0}", hash.Key));
				}

			FileInfo[] fiZ = new FileInfo[len - alIdx.Count];

			int j = 0;
			for (int i = 0; i < len; i++)
			{
				if (!alIdx.Contains(filesToCompare[i].Length))
					fiZ[j++] = filesToCompare[i];
			}
			return fiZ;
		}

		private void FindDuplicate()
		{
			DCFileInfo = new DirectoryCrawler.FileInfoProvider();
			DCFileInfo.FileProgressed += new FileInfoEventHandler(DCFileInfo_FileProgressed);
			DCFileInfo.FileDone += new FileInfoEventHandler(DCFileInfo_FileDone);
			DCFileInfo.AllFilesRead += new FileInfoEventHandler(DCFileInfo_AllFilesRead);
			setText(this, "Duplicate Finder - Getting MD5 hash for same size files ");
			DCFileInfo.PopulateInfos(m_fileInfos);
		}

		private void clearNonDuplicates()
		{
			List<string> alIdx = new List<string>();

			int itCnt = 5, len = alFiles.Count;

			System.Collections.Hashtable HHashes = new System.Collections.Hashtable();

			foreach (string[] file in alFiles)
			{
				if (!HHashes.Contains(file[3]))
					HHashes.Add(file[3], 1);

				else
					HHashes[file[3]] = (int)HHashes[file[3]] + 1;
			}

			foreach (DictionaryEntry hash in HHashes)
				if ((int)hash.Value == 1)
				{
					alIdx.Add((string)hash.Key);

					showStatus(string.Format("Will remove File with MD5 {0}", hash.Key));
				}

			string[,] sITEMS = new string[alFiles.Count, itCnt];

			string[] tmp = new string[itCnt];


			for (int i = 0; i < len; i++)
			{
				tmp = alFiles[i];

				if (!alIdx.Contains(tmp[3]))
					for (int j = 0; j < itCnt; j++)
						sITEMS[i, j] = tmp[j];
			}

			alFiles.Clear();

			for (int i = 0; i < sITEMS.GetLength(0); i++)
			{
				if (sITEMS[i, 0] != null)
				{
					tmp = new string[itCnt];

					for (int j = 0; j < itCnt; j++)
						tmp[j] = sITEMS[i, j];

					alFiles.Add(tmp);
				}
			}
		}

		private void showInformations()
		{
			long[] dups = GetDuplicateFilesInfos();
			long dupFiles = dups[0];
			long dupSizes = dups[1];
			showDuplicateInfo(dupFiles, dupSizes);
		}

		private long[] GetDuplicateFilesInfos()
		{
			long nSizes = 0;
			long nFiles = lstFiles.Items.Count;

			for (long i = 0; i < nFiles; i++)
			{
				ListViewItem it = lstFiles.Items[(int)i];
				if (null != it.Tag && (bool)it.Tag == true)
					nSizes += long.Parse(it.SubItems[1].Text.Replace(" ", string.Empty));
			}
			return new long[] { nFiles, nSizes };
		}

		private void deleteDuplicateFiles()
		{
			len = lstFiles.Items.Count;
			prg = 0;
			for (int i = 0; i < len; i++)
			{
				ListViewItem it = lstFiles.Items[i];
				if (null != it.Tag && (bool)it.Tag == true)
					deleteFile(it);
			}
			WatchFinished();
		}

		private Color Colorize(long size)
		{
			long kilo = 1024;
			long mega = kilo * kilo;
			long hunmeg = 100 * mega;
			long giga = mega * mega;

			Color[] clrs = new System.Drawing.Color[] 
            { 
                Color.LightGreen, 
                Color.White, 
                Color.LightBlue, 
                Color.Red 
            };

			if (size < kilo)
				return clrs[0];

			if (size < mega)
				return clrs[1];

			if (size < hunmeg)
				return clrs[2];

			return clrs[3];
		}

		private string TrunC(string text, int nMX)
		{
			if (text.Length <= nMX)
				return text;
			int nRetain = text.Length - nMX;
			int mid = (int)((float)text.Length / 2f);
			int limin = mid - (nRetain / 2) - 2;
			int limax = mid + (nRetain / 2) + 2;
			if ((limin * limax > 0) && (limax < text.Length))
			{
				string part1 = text.Substring(0, limin) + "...";
				string part2 = text.Substring(limax);
				return string.Concat(part1, part2);
			}
			else
				return text;
		}

		private void deleteFile(ListViewItem itemToDelete)
		{
			string file = itemToDelete.SubItems[4].Text + "\\" + itemToDelete.SubItems[0].Text;
			string s = "Moving : " + file;
			showStatus(TrunC(s, 100));

			if (!m_bErase)
			{
				DirectoryCrawler.FileMover fs = new DirectoryCrawler.FileMover(file, m_deleteFolder, false);
				fs.CopyProgressed += new FileMoverEventHandler(fs_MoveProgressed);
				fs.MoveDone += new FileMoverEventHandler(fs_MoveDone);
				fs.DeleteError += new FileMoverEventHandler(fs_DeleteError);
#if DEBUG
                fs.MoveSynchronous();
#else
				fs.Move();
#endif
			}
			else
			{
				DirectoryCrawler.FileMover fs = new DuplicateFinder.DirectoryCrawler.FileMover(file);
				fs.CopyProgressed += new FileMoverEventHandler(fs_MoveProgressed);
				fs.MoveDone += new FileMoverEventHandler(fs_MoveDone);
				fs.DeleteError += new FileMoverEventHandler(fs_DeleteError);
				fs.Delete();
			}
		}

		private void WatchFinished()
		{
			Thread td = new Thread(new ThreadStart(watchFinished));
			td.Start();
		}

		private void watchFinished()
		{
			do
			{
				Thread.Sleep(50);
			}
			while (lstFiles.CheckedItems.Count != 0);

			showProgress(prgHole, 100);
			string ans = "Done";
			if (undeletable.Count != 0)
			{
				string[] items = new string[undeletable.Count];
				undeletable.CopyTo(items, 0);
				frmUndeletable frm = new frmUndeletable(items);
				frm.ShowDialog();
			}
			else
				ans += ", No Items TO Delete";

			MessageBox.Show(ans + " : " + prg.ToString() + " files deleted");
		}

		private void FormatAddFilesToLV()
		{
			bool ischkChecked = chkSkipFirst.Checked;

			string
				 lastHASH = string.Empty,
				 HASH = string.Empty;

			Comparers.FileListComparer flCmp = new DuplicateFinder.Comparers.FileListComparer(firstFolder);
			alFiles.Sort(flCmp.FileNameComparer);
			ListViewItem[] lvCOL = new ListViewItem[alFiles.Count];

			bool
				 bIsInFirstFolder,
				 bIsOldHash,
				 bBold,
				 bIsCheck;

			int iLV = 0;
			Font fntBold = new Font("calibri", 10, FontStyle.Bold);
			Font fntNormal = new Font("calibri", 10, FontStyle.Regular);

			foreach (string[] lvFile in alFiles)
			{
				HASH = lvFile[3];
				bIsInFirstFolder = lvFile[4].ToLower().StartsWith(firstFolder) && ischkChecked;
				bIsOldHash = (HASH.CompareTo(lastHASH) == 0);
				bBold = bIsInFirstFolder;
				bIsCheck = !bBold && bIsOldHash;
				lvCOL[iLV] = new ListViewItem(lvFile);
				lvCOL[iLV].Checked = bIsCheck;
				lvCOL[iLV].BackColor = Colorize(long.Parse(lvFile[1].Replace(" ", string.Empty)));
				if (!bBold)
					lvCOL[iLV].Font = fntNormal;
				else
				{
					lvCOL[iLV].Font = fntBold;
					lvCOL[iLV].ForeColor = Color.Red;
				}
				lastHASH = HASH;
				iLV++;
			}
			addListItems(lstFiles, lvCOL);
			lvCOL = null;
		}

		#endregion

		#region DirectoryCrawler.FileInformation Events

		private void DCFileInfo_AllFilesRead(object sender, FileInfoEventArgs e)
		{
			setText(this, "Duplicate Finder - Clearing non Duplicates");
			clearNonDuplicates();
			System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;
			Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
			setText(this, "Duplicate Finder - Adding Files to Listview");
			if (alFiles.Count == 0)
			{
				lockControls(false);
				m_fileInfos = new FileInfo[0];
				return;
			}

			FormatAddFilesToLV();
			showProgress(prgFile, 100);
			showProgress(prgHole, 100);
			setText(this, "Duplicate Finder - Organizing groups...");
			groupOrganize();
			setWindowState();
			setText(this, "Duplicate Finder - Showing deletable items...");
			fillDeletableItems();
			Thread.CurrentThread.Priority = ThreadPriority.Normal;
			System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.Normal;
			setCursor(Cursors.Default);
			lockControls(false);
		}

		private void DCFileInfo_FileDone(object sender, FileInfoEventArgs e)
		{
			string[] Items = new string[5];
			e.Items.CopyTo(Items, 0);
			alFiles.Add(Items);
			if (e.Item != -1 && e.Item % 10 == 0)
			{
				showProgress(prgHole, e.TotalProgress);
				showProgress(prgFile, 100);
				showStatus("Hashed " + Items[0] + " : " + Items[3]);
			}
		}

		private void DCFileInfo_FileProgressed(object sender, FileInfoEventArgs e)
		{
			showProgress(prgFile, e.Progress);
			showStatus(TrunC(e.CurrentFile, 100));
		}

		#endregion

		#region FileMover Events
		int errors = 0;

		private void fs_DeleteError(object sender, FileMoverEventArgs e)
		{
			undeletable.Add(e.FileName);

			errors++;

			prg++;

			showProgress(prgHole, 100 * prg / len);

		}

		private void fs_MoveDone(object sender, FileMoverEventArgs e)
		{
			prg++;

			showProgress(prgHole, 100 * prg / len);

			deleteItemByString(e.FileName);

		}

		private void fs_MoveProgressed(object sender, FileMoverEventArgs e)
		{
			showProgress(prgFile, e.Progress);
		}
		#endregion

		#region DirectoryCrawler Events
		private void DCSearch_FoldersDone(object sender, EventArgs e)
		{
			int cnt = ((DirectoryCrawler.DirectoryCrawler)sender).FilesFound;
			string s = "Found " + cnt.ToString() + " files";
			showStatus(TrunC(s, 100));
			setText(this, "Duplicate Finder - Searching files with same size");
			m_fileInfos = SameSize(((DirectoryCrawler.DirectoryCrawler)sender).FileInfos);
			setText(lblFilesSameSize, m_fileInfos.Length.ToString());
			setText(this, "Duplicate Finder - Searching duplicates");
			FindDuplicate();
			setCursor(Cursors.Default);
		}

		private void DCSearch_FolderChanged(object sender, EventArgs e)
		{
			string fld = ((DirectoryCrawler.DirectoryCrawler)sender).CurrentFolder;
			int cnt = ((DirectoryCrawler.DirectoryCrawler)sender).FilesFound;
			string s = cnt.ToString() + " @ " + fld;
			showStatus(TrunC(s, 100));
		}
		#endregion

		#region Form delegates

		public delegate void LockControls(bool lockit);

		private void lockControls(bool lockit)
		{
			if (this.InvokeRequired)
				this.BeginInvoke(new LockControls(lockControls), new object[] { lockit });

			else
				btnGo.Enabled = btnAdd.Enabled = btnBrowse.Enabled = btnDrop.Enabled =
					 txtExtension.Enabled = chkSkipFirst.Enabled = !lockit;
		}

		public delegate void AddListItems(ListView lv, ListViewItem[] lvItems);

		private void addListItems(ListView lv, ListViewItem[] lvItems)
		{
			if (lv.InvokeRequired)
				lv.BeginInvoke(new AddListItems(addListItems), new object[] { lv, lvItems });
			else
				lv.Items.AddRange(lvItems);
		}

		public delegate void FillDeletableItems();

		private void fillDeletableItems()
		{
			if (lstFiles.InvokeRequired)
				lstFiles.BeginInvoke(new FillDeletableItems(fillDeletableItems));
			else
			{
				foreach (ListViewItem it in lstFiles.CheckedItems)
				{
					if (!it.Font.Strikeout)
						it.Tag = true;
					else
						it.Tag = false;
				}
				showInformations();
			}
		}

		public delegate void GroupOrganize();

		private void groupOrganize()
		{
			if (!lstFiles.InvokeRequired)
			{
				int cols = 5;
				groupTables = new Hashtable[cols];
				for (int column = 0; column < cols; column++)
					groupTables[column] = CreateGroupsTable(column);
				SetGroups(3);
				for (int i = 0; i < lstFiles.Columns.Count; i++)
					if (i != 3)
						lstFiles.Columns[i].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);

				clnHash.Width = 0;
			}
			else
				lstFiles.BeginInvoke(new GroupOrganize(groupOrganize));
		}

		public delegate void DeleteItem(ListViewItem it);

		private void deleteItem(ListViewItem it)
		{
			if (lstFiles.InvokeRequired)
				lstFiles.BeginInvoke(new DeleteItem(deleteItem), new object[] { it });

			else
			{
				Font fntStr = new Font(it.Font, FontStyle.Strikeout);
				it.Font = fntStr;
				it.Checked = false;
				it.Tag = false;
			}
		}

		public delegate void DeleteItemByString(string filename);

		private void deleteItemByString(string filename)
		{
			if (lstFiles.InvokeRequired)
				lstFiles.BeginInvoke(new DeleteItemByString(deleteItemByString), new object[] { filename });

			else
			{
				foreach (ListViewItem item in lstFiles.Items)
					if (item.SubItems[4].Text + "\\" + item.SubItems[0].Text == filename)
					{
						Font fntStr = new Font(item.Font, FontStyle.Strikeout);
						item.Font = fntStr;
						item.Checked = false;
						return;
					}
			}
		}

		public delegate void SetCursor(Cursor cursor);

		private void setCursor(Cursor cursor)
		{
			if (!this.InvokeRequired)
				this.Cursor = cursor;

			else
				this.BeginInvoke(new SetCursor(setCursor), new object[] { cursor });
		}

		public delegate void SetWindowState();

		private void setWindowState()
		{
			if (!this.InvokeRequired)
				this.WindowState = FormWindowState.Normal;

			else
				this.BeginInvoke(new SetWindowState(setWindowState));
		}

		public delegate void ShowStatus(string status);

		private void showStatus(string status)
		{
			if (this.WindowState == FormWindowState.Minimized)
				return;

			if (stsMain.InvokeRequired)
				stsMain.BeginInvoke(new ShowStatus(showStatus), new object[] { status });

			else
			{
				stsLabel.Text = TrunC(status, 100);
			}
		}

		public delegate void AddRangeToGroup(ListViewGroup[] groupsArray);

		private void addRangeToGroup(ListViewGroup[] groupsArray)
		{
			if (!lstFiles.InvokeRequired)
				lstFiles.Groups.AddRange(groupsArray);

			else
				lstFiles.BeginInvoke(new AddRangeToGroup(addRangeToGroup), new object[] { groupsArray });
		}

		public delegate void ClearGroups();

		private void clearGroups()
		{
			if (!lstFiles.InvokeRequired)
				lstFiles.Groups.Clear();

			else
				lstFiles.BeginInvoke(new ClearGroups(clearGroups));
		}

		public delegate void AddListItem(object lvi, bool check);

		private void addListItem(object lvitem, bool check)
		{
			if (lstFiles.InvokeRequired)
				lstFiles.BeginInvoke(new AddListItem(addListItem), new object[] { lvitem, check });

			else
			{
				ListViewItem lv = new ListViewItem((string[])lvitem);
				lv.Checked = check;
				lv.BackColor = Colorize(long.Parse(lv.SubItems[1].Text.Replace(" ", string.Empty)));
				lstFiles.Items.Add(lv);
			}
		}

		public delegate void ShowProgress(ProgressBar progressbar, int prg);

		private void showProgress(ProgressBar progressbar, int prg)
		{
			if (this.WindowState == FormWindowState.Minimized)
				return;

			if (progressbar.InvokeRequired)
				progressbar.BeginInvoke(new ShowProgress(showProgress), new object[] { progressbar, prg });

			else
			{
				if (prg < progressbar.Maximum)
					progressbar.Value = prg;

				else
					progressbar.Value = progressbar.Maximum;
			}
		}

		public delegate void ShowDuplicateInfo(long dupFiles, long dupSizes);

		private void showDuplicateInfo(long dupFiles, long dupSizes)
		{
			if (stsMain.InvokeRequired)
				stsMain.BeginInvoke(new ShowDuplicateInfo(showDuplicateInfo), new object[] { dupFiles, dupSizes });

			else
				stsLabel.Text = dupFiles.ToString() + " duplicate files of total size : " +
					 SpaceThousands(dupSizes / 1024) + " Kb";
		}

		public delegate void SetText(Control ctrl, string str);

		private void setText(Control ctrl, string str)
		{
			if (this.WindowState == FormWindowState.Minimized)
				return;

			if (ctrl.InvokeRequired)
				ctrl.BeginInvoke(new SetText(setText), new object[] { ctrl, str });

			else
				ctrl.Text = str;
		}

		#endregion

		#region Form events

		private void lstFiles_ItemChecked(object sender, ItemCheckedEventArgs e)
		{
			fillDeletableItems();
		}

		private void SetGroups(int column)
		{
			clearGroups();
			Hashtable groups = (Hashtable)groupTables[column];
			ListViewGroup[] groupsArray = new ListViewGroup[groups.Count];
			groups.Values.CopyTo(groupsArray, 0);
			Array.Sort(groupsArray, new DuplicateFinder.Comparers.ListViewGroupSorter(SortOrder.Ascending, column));
			addRangeToGroup(groupsArray);

			foreach (ListViewItem item in lstFiles.Items)
			{
				string subItemText = item.SubItems[column].Text;
				if (column == 0)
					subItemText = subItemText.Substring(0, 1);

				item.Group = (ListViewGroup)groups[subItemText];
			}
		}

		private Hashtable CreateGroupsTable(int column)
		{
			Hashtable groups = new Hashtable();
			foreach (ListViewItem item in lstFiles.Items)
			{
				string subItemText = item.SubItems[column].Text;
				if (!groups.Contains(subItemText))
				{
					groups.Add(subItemText, new ListViewGroup(subItemText, HorizontalAlignment.Left));
				}
			}
			return groups;
		}

		private void deleteSelectedFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (rdbDUP.Checked && (
				 m_deleteFolder.Length < 2 || !Directory.Exists(m_deleteFolder)))
			{
				MessageBox.Show(this,
					 "You must choose a folder to store duplicates, or else select Move to Recycle Bin.",
					 "Remove Duplicates");
				return;
			}
			setText(this, "Duplicate Finder - moving selected FILES to " + m_deleteFolder);
#if DEBUG
            deleteDuplicateFiles();
#else
			if (rdbRecycleBin.Checked && !chkDelete.Checked)
			{
				DeleteAllToRecycleBin();
				return;
			}
			m_tdDELETE = new Thread(new ThreadStart(deleteDuplicateFiles));
			m_tdDELETE.Start();
#endif
		}

		private void btnBrowse_Click(object sender, EventArgs e)
		{
			if (fldBrowse.ShowDialog() == DialogResult.OK)
			{
				String strPath = fldBrowse.SelectedPath;
				if (strPath == "C:\\")
				{
					String strSkip =
						 System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
					strPath += " | -" + strSkip;
					strSkip = Environment.GetEnvironmentVariable("windir");
					strPath += " | -" + strSkip;
					strSkip = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
					if (!String.IsNullOrEmpty(strSkip))
						strPath += " | -" + strSkip;
				}
				txtFolder.Text = strPath;
				btnGo.Enabled = true;
				btnAdd.Visible = true;
				btnDrop.Visible = true;
				btnAdd.Focus();
			}
			else if (txtFolder.Text.Length == 0)
			{
				btnGo.Enabled = false;
				btnAdd.Visible = false;
				btnDrop.Visible = false;
			}
		}

		private void btnBrwseDupFolder_Click(object sender, EventArgs e)
		{
			if (fldBrowse.ShowDialog() == DialogResult.OK)
			{
				txtDuplicateFolder.Text = fldBrowse.SelectedPath;
				m_deleteFolder = txtDuplicateFolder.Text;
			}
		}

		private void btnAdd_Click(object sender, EventArgs e)
		{
			if (fldBrowse.ShowDialog() == DialogResult.OK)
				txtFolder.Text += " | " + fldBrowse.SelectedPath;
		}

		private void btnGo_Click(object sender, EventArgs e)
		{
			lockControls(true);
			lstFiles.Items.Clear();
			alFiles.Clear();
			this.Cursor = Cursors.WaitCursor;
			SearchFiles();
		}

		private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			try
			{
				if (m_tdDELETE != null)
					m_tdDELETE.Abort();
			}
			catch (Exception) { }
		}

		private void lstFiles_DoubleClick(object sender, EventArgs e)
		{
			string path = lstFiles.SelectedItems[0].SubItems[4].Text + "\\" + lstFiles.SelectedItems[0].SubItems[0].Text;

			System.Diagnostics.Process.Start(path);
		}

		private void btnDrop_Click(object sender, EventArgs e)
		{
			if (fldBrowse.ShowDialog() == DialogResult.OK)
				txtFolder.Text += " | -" + fldBrowse.SelectedPath;
		}

		private void openContainingFolderToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string path = lstFiles.SelectedItems[0].SubItems[4].Text;

			System.Diagnostics.Process.Start("explorer.exe", path);
		}

		private void viewWithNotepadToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string path = lstFiles.SelectedItems[0].SubItems[4].Text + "\\" + lstFiles.SelectedItems[0].SubItems[0].Text;

			System.Diagnostics.Process.Start("notepad++.exe", path);
		}

		private void chkDelete_CheckedChanged(object sender, EventArgs e)
		{
			m_bErase = chkDelete.Checked;
			rdbDUP.Enabled = !chkDelete.Checked;
			rdbRecycleBin.Enabled = !chkDelete.Checked;
		}
		#endregion

		private void frmMain_Load(object sender, EventArgs e)
		{
			m_deleteFolder = "";
			if (Directory.Exists(@"D:\DuplicateFiles"))
				m_deleteFolder = @"D:\DuplicateFiles";
			else if (Directory.Exists(@"C:\Temp"))
				m_deleteFolder = @"C:\Temp";
			txtDuplicateFolder.Text = m_deleteFolder;
		}
	}
}
