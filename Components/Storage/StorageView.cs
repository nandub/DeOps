using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Drawing;
using System.Data;
using System.IO;
using System.Text;
using System.Windows.Forms;

using DeOps.Interface;
using DeOps.Interface.TLVex;
using DeOps.Interface.Views;

using DeOps.Implementation;
using DeOps.Implementation.Protocol;
using DeOps.Components.Link;


namespace DeOps.Components.Storage
{
    internal partial class StorageView : ViewShell
    {
        internal OpCore Core;
        internal StorageControl Storages;
        internal LinkControl Links;

        internal ulong DhtID;
        internal uint ProjectID;

        internal WorkingStorage Working;
        FolderNode RootFolder;
        FolderNode SelectedFolder;

        Dictionary<string, int> IconMap = new Dictionary<string,int>();
        List<Image> FileIcons = new List<Image>();

        internal Font RegularFont = new Font("Tahoma", 8.25F);
        internal Font BoldFont = new Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));

        internal List<ulong> HigherIDs = new List<ulong>();
        internal List<ulong> CurrentDiffs = new List<ulong>();
        internal List<ulong> FailedDiffs = new List<ulong>();

        Dictionary<ulong, RescanFolder> RescanFolderMap = new Dictionary<ulong, RescanFolder>();
        int NextRescan;

        internal bool WatchTransfers;

        internal bool IsLocal;

        ToolStripMenuItem MenuAdd;
        ToolStripMenuItem MenuLock;
        ToolStripMenuItem MenuUnlock;
        ToolStripMenuItem MenuRestore;
        ToolStripMenuItem MenuDelete;
        ToolStripMenuItem MenuDetails;

        ContainerListViewEx LastSelectedView;


        internal StorageView(StorageControl storages, ulong id, uint project)
        {
            InitializeComponent();

            Storages = storages;
            Core = Storages.Core;
            Links = Core.Links;

            DhtID = id;
            ProjectID = project;

            splitContainer1.Height = Height - toolStrip1.Height;

            if (DhtID == Core.LocalDhtID)
                if (Storages.Working.ContainsKey(ProjectID))
                {
                    Working = Storages.Working[ProjectID];
                    IsLocal = true;
                }

            MenuAdd = new ToolStripMenuItem("Add to Storage", null, FileView_Add);
            MenuLock = new ToolStripMenuItem("Lock", StorageRes.Locked, FileView_Lock);
            MenuUnlock = new ToolStripMenuItem("Unlock", StorageRes.Unlocked, FileView_Unlock);
            MenuRestore = new ToolStripMenuItem("Restore", null, FileView_Restore);
            MenuDelete = new ToolStripMenuItem("Delete", StorageRes.Reject, FileView_Delete);
            MenuDetails = new ToolStripMenuItem("Details", StorageRes.details, FileView_Details);


            toolStrip1.Renderer = new ToolStripProfessionalRenderer(new OpusColorTable());
        }

        internal override string GetTitle(bool small)
        {
            if (small)
                return "Storage";

            string title = "";

            if (IsLocal)
                title += "My ";
            else
                title += Links.GetName(DhtID) + "'s ";

            if (ProjectID != 0)
                title += Links.ProjectNames[ProjectID] + " ";

            title += "Storage";

            return title;
        }

        internal override Size GetDefaultSize()
        {
            return new Size(550, 425);
        }

        internal override Icon GetIcon()
        {
            return StorageRes.Icon;
        }

        internal override void Init()
        {
            FolderTreeView.SmallImageList = new List<Image>();
            FolderTreeView.SmallImageList.Add(StorageRes.Folder);
            FolderTreeView.SmallImageList.Add(StorageRes.GhostFolder);

            FileIcons.Add(new Bitmap(16, 16));
            FileIcons.Add(StorageRes.Ghost);
            FileIcons.Add(StorageRes.Folder);
            FileIcons.Add(StorageRes.GhostFolder);

            FileListView.OverlayImages.Add(StorageRes.Higher);
            FileListView.OverlayImages.Add(StorageRes.Lower);
            FileListView.OverlayImages.Add(StorageRes.DownloadSmall);
            FileListView.OverlayImages.Add(StorageRes.Temp);

            FolderTreeView.OverlayImages.Add(StorageRes.Higher);
            FolderTreeView.OverlayImages.Add(StorageRes.Lower);
            FolderTreeView.OverlayImages.Add(StorageRes.Temp);

            SelectedInfo.Init(this);


            // hook up events
            Storages.StorageUpdate += new StorageUpdateHandler(Storages_StorageUpdate);
            Links.LinkUpdate += new LinkUpdateHandler(Links_LinkUpdate);

            if (IsLocal)
            {
                Storages.WorkingFileUpdate += new WorkingUpdateHandler(Storages_WorkingFileUpdate);
                Storages.WorkingFolderUpdate += new WorkingUpdateHandler(Storages_WorkingFolderUpdate);
            }


            // research higher / lowers
            List<ulong> ids = new List<ulong>();
            ids.Add(DhtID);
            ids.AddRange(Storages.GetHigherIDs(DhtID, ProjectID));
            ids.AddRange(Links.GetDownlinkIDs(DhtID, ProjectID, 1));

            foreach (ulong id in ids)
                Storages.Research(id);


            // diff
            DiffCombo.Items.Add("Local");
            DiffCombo.Items.Add("Higher");
            DiffCombo.Items.Add("Lower");
            DiffCombo.Items.Add("Custom...");

            DiffCombo.SelectedIndex = 0;

            // dont show folder panel unless there are folders
            bool showFolders = false;

            if (RootFolder != null)
                foreach (FolderNode folder in RootFolder.Folders.Values)
                    if (!folder.Details.IsFlagged(StorageFlags.Archived))
                    {
                        showFolders = true;
                        break;
                    }

            FoldersButton.Checked = showFolders;
            FoldersButton_CheckedChanged(null, null); // event doesnt fire when setting false = false
        }

        private void StorageView_Load(object sender, EventArgs e)
        {
            // if SelectedInfo.InfoDisplay.DocumentText updated multiple times before load, it will show nothing

            SelectedInfo.DisplayActivated = true;
            SelectedInfo.ShowDiffs();
        }


        internal override bool Fin()
        {
            Storages.StorageUpdate -= new StorageUpdateHandler(Storages_StorageUpdate);
            Links.LinkUpdate -= new LinkUpdateHandler(Links_LinkUpdate);

            Storages.WorkingFileUpdate -= new WorkingUpdateHandler(Storages_WorkingFileUpdate);
            Storages.WorkingFolderUpdate -= new WorkingUpdateHandler(Storages_WorkingFolderUpdate);

            return true;
        }
        

        private void DiffCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            HigherIDs = Storages.GetHigherIDs(DhtID, ProjectID);

            CurrentDiffs.Clear();

            if (DiffCombo.Text == "Local")
            {
                CurrentDiffs.AddRange(HigherIDs);
                CurrentDiffs.AddRange(Links.GetDownlinkIDs(DhtID, ProjectID, 1));
            }

            else if (DiffCombo.Text == "Higher")
            {
                CurrentDiffs.AddRange(HigherIDs);
            }

            else if (DiffCombo.Text == "Lower")
            {
                CurrentDiffs.AddRange(Links.GetDownlinkIDs(DhtID, ProjectID, 1));
            }

            else if (DiffCombo.Text == "Custom...")
            {
                AddLinks form = new AddLinks(Links, ProjectID);

                if (form.ShowDialog(this) == DialogResult.OK)
                    CurrentDiffs.AddRange(form.People);

                // if cancel then no diffs are made, isnt too bad, there are situations you'd watch no diffs
            }

            FailedDiffs.Clear();

            // clear out current diffs
            RefreshView();
        }

        private void RefreshView()
        {
            FolderNode prevSelected = SelectedFolder;

            RootFolder = null;

            FolderTreeView.Nodes.Clear();
            FileListView.Items.Clear();

            // if local
            if (IsLocal)
                RootFolder = LoadWorking(FolderTreeView.virtualParent, Working.RootFolder);

            // else load from file
            else
            {
                StorageFolder root = new StorageFolder();
                root.Name = Links.ProjectNames[ProjectID] + " Storage";

                RootFolder = new FolderNode(this, root, FolderTreeView.virtualParent, false);
                FolderTreeView.Nodes.Add(RootFolder);

                if(Storages.StorageMap.ContainsKey(DhtID))
                {
                    StorageHeader header = Storages.StorageMap[DhtID].Header;

                    LoadHeader(Storages.GetFilePath(header), header.FileKey);
                }
            }

            // re-diff
            foreach (ulong id in CurrentDiffs)
                ApplyDiff(id);


            if (RootFolder != null)
                RootFolder.Expand();
            

            bool showDiffs = SelectedInfo.DiffsView; // save diffs view mode here because selectFolder resets it

            // if prev == selected, this means selected wasnt updated in refresh
            if (SelectedFolder == null || prevSelected == SelectedFolder)
                SelectFolder(RootFolder);
            else
            {
                SelectedFolder.EnsureVisible();
                RefreshFileList();
            }

            if (showDiffs)
                SelectedInfo.ShowDiffs();
        }

        private void ApplyDiff(ulong id)
        {
            
            // change log - who / not


            // read file uid
                
                // if exists locally
                    // if integrated mark as so
                    // else mark as changed, run history diff, and use last note

                // if doesnt exist
                    // add file as a temp
                    // user does - add to storage, to add it

                // if remote doesnt have any record of local file, ignore



            if (!Storages.StorageMap.ContainsKey(id))
            {
                FailedDiffs.Add(id);
                return;
            }

            StorageHeader headerx = Storages.StorageMap[id].Header;

            string path = Storages.GetFilePath(headerx);

            if (!File.Exists(path))
            {
                FailedDiffs.Add(id); 
                return;
            }

            try
            {
                FileStream filex = new FileStream(path, FileMode.Open);
                CryptoStream crypto = new CryptoStream(filex, headerx.FileKey.CreateDecryptor(), CryptoStreamMode.Read);
                PacketStream stream = new PacketStream(crypto, Core.Protocol, FileAccess.Read);

                ulong remoteUID = 0;
                FolderNode currentFolder = RootFolder;
                bool readingProject = false;

                G2Header header = null;

                while (stream.ReadPacket(ref header))
                {
                    if (header.Name == StoragePacket.Root)
                    {
                        StorageRoot root = StorageRoot.Decode(Core.Protocol, header);

                        readingProject = (root.ProjectID == ProjectID);
                    }

                    if (readingProject)
                    {
                        if (header.Name == StoragePacket.Folder)
                        {
                            StorageFolder folder = StorageFolder.Decode(Core.Protocol, header);

                            // if new UID 
                            if (remoteUID == folder.UID)
                                continue;

                            remoteUID = folder.UID;

                            bool added = false;

                            while (!added)
                            {
                                if (currentFolder.Details.UID == folder.ParentUID)
                                {
                                    // if folder exists with UID
                                    if (currentFolder.Folders.ContainsKey(remoteUID))
                                        currentFolder = currentFolder.Folders[remoteUID];

                                    // else add folder as temp, mark as changed
                                    else
                                        currentFolder = currentFolder.AddFolderInfo(folder, true);


                                    bool found = false;


                                    foreach (StorageFolder archive in currentFolder.Archived)
                                        if (Storages.ItemDiff(archive, folder) == StorageActions.None)
                                            if (archive.Date == folder.Date)
                                            {
                                                found = true;
                                                break;
                                            }

                                    if (!found)
                                    {
                                        currentFolder.Changes[id] = folder;
                                        currentFolder.UpdateOverlay();
                                    }

                                    // diff file properties, if different, add as change
                                    /*if (currentFolder.Temp || Storages.ItemDiff(currentFolder.Details, folder) != StorageActions.None)
                                    {
                                        currentFolder.Changes[id] = folder;
                                        currentFolder.UpdateOverlay();
                                    }*/

                                    added = true;
                                }
                                else if (currentFolder.Parent.GetType() == typeof(FolderNode))
                                    currentFolder = (FolderNode)currentFolder.Parent;
                                else
                                    break;
                            }
                        }

                        if (header.Name == StoragePacket.File)
                        {
                            StorageFile file = StorageFile.Decode(Core.Protocol, header);

                            // if new UID 
                            if (remoteUID == file.UID)
                                continue;

                            remoteUID = file.UID;

                            FileItem currentFile = null;

                            // if file exists with UID
                            if (currentFolder.Files.ContainsKey(remoteUID))
                                currentFile = currentFolder.Files[remoteUID];

                            // else add file as temp, mark as changed
                            else
                                currentFile = currentFolder.AddFileInfo(file, true);

                            // if file is integrated, still add, so that reject functions

                            // true if file doesnt exist in local file history
                                // if it does exist and file is newer than latest, true

                            bool found = false;


                            foreach (StorageFile archive in currentFile.Archived)
                                if (Storages.ItemDiff(archive, file) == StorageActions.None)
                                    if (archive.Date == file.Date)
                                    {
                                        found = true;
                                        break;
                                    }

                            if(!found)
                                currentFile.Changes[id] = file;
                        }
                    }
                }

                stream.Close();
            }
            catch
            {
                FailedDiffs.Add(id); 
            }
        }

        private void RemoveDiff(ulong id, FolderNode folder)
        {
            if (FailedDiffs.Contains(id))
                FailedDiffs.Remove(id);

            // remove changes from files
            List<ulong> removeUIDs = new List<ulong>();

            foreach (FileItem file in folder.Files.Values)
            {
                if (file.Changes.ContainsKey(id))
                    file.Changes.Remove(id);

                // remove entirely if temp etc..
                if (file.Temp && file.Changes.Count == 0)
                    removeUIDs.Add(file.Details.UID);
            }

            foreach (ulong uid in removeUIDs)
                folder.Files.Remove(uid);

            removeUIDs.Clear();

            // remove changes from folders
            foreach (FolderNode sub in folder.Folders.Values)
            {
                if (sub.Changes.ContainsKey(id))
                {
                    sub.Changes.Remove(id);
                    sub.UpdateOverlay();
                }

                // remove entirely if temp etc..
                if (sub.Temp && sub.Changes.Count == 0)
                {
                    removeUIDs.Add(sub.Details.UID);

                    if (folder.Nodes.Contains(sub))
                        folder.Nodes.Remove(sub);

                    if (SelectedFolder == sub)
                        SelectFolder(folder);
                }
            }

            foreach (ulong uid in removeUIDs)
                folder.Folders.Remove(uid);

            // recurse
            foreach (FolderNode sub in folder.Folders.Values)
                RemoveDiff(id, sub);
        }

        private void Links_LinkUpdate(OpLink link)
        {
            // check if command structure has changed
            List<ulong> check = new List<ulong>();
            List<ulong> highers = Storages.GetHigherIDs(DhtID, ProjectID);

            if (DiffCombo.Text == "Local")
            {
                check.AddRange(HigherIDs);
                check.AddRange(Links.GetDownlinkIDs(DhtID, ProjectID, 1));
            }

            else if (DiffCombo.Text == "Higher")
                check.AddRange(HigherIDs);

            else if (DiffCombo.Text == "Lower")
                check.AddRange(Links.GetDownlinkIDs(DhtID, ProjectID, 1));

            else // custom
                return;

            if (ListsMatch(highers, HigherIDs) && ListsMatch(check, CurrentDiffs))
                return;


            HigherIDs = highers;
            CurrentDiffs = check;
            FailedDiffs.Clear();

            RefreshView();
        }

        private bool ListsMatch(List<ulong> first, List<ulong> second)
        {
            foreach (ulong id in first)
                if (!second.Contains(id))
                    return false;

            foreach (ulong id in second)
                if (!first.Contains(id))
                    return false;

            return true;
        }

        private void Storages_StorageUpdate(OpStorage storage)
        {
            if (storage.DhtID == DhtID)
                RefreshView();

            // re-apply diff
            if (CurrentDiffs.Contains(storage.DhtID))
            {
                RemoveDiff(storage.DhtID, RootFolder);
                ApplyDiff(storage.DhtID);

                SelectedInfo.UpdateDiffView(storage.DhtID);

                RefreshFileList();
            }
        }

        private void GhostsButton_CheckedChanged(object sender, EventArgs e)
        {
            RefreshView();
        }


        private void LoadHeader(string path, RijndaelManaged key)
        {
            try
            {
                FileStream filex = new FileStream(path, FileMode.Open);
                CryptoStream crypto = new CryptoStream(filex, key.CreateDecryptor(), CryptoStreamMode.Read);
                PacketStream stream = new PacketStream(crypto, Core.Protocol, FileAccess.Read);

                FolderNode currentFolder = RootFolder;
                bool readingProject = false;

                G2Header header = null;

                while (stream.ReadPacket(ref header))
                {
                    if (header.Name == StoragePacket.Root)
                    {
                        StorageRoot root = StorageRoot.Decode(Core.Protocol, header);

                        readingProject = (root.ProjectID == ProjectID);
                    }

                    // show archived if selected, only add top uid, not copies

                    if (readingProject)
                    {
                        if (header.Name == StoragePacket.Folder)
                        {
                            StorageFolder folder = StorageFolder.Decode(Core.Protocol, header);

                            bool added = false;

                            while (!added)
                            {
                                if (currentFolder.Details.UID == folder.ParentUID)
                                {
                                    currentFolder = currentFolder.AddFolderInfo(folder, false);

                                    // re-select on re-load
                                    if (SelectedFolder != null && currentFolder.Details.UID == SelectedFolder.Details.UID)
                                    {
                                        currentFolder.Selected = true;
                                        SelectedFolder = currentFolder;
                                    }

                                    added = true;
                                }
                                else if (currentFolder.Parent.GetType() == typeof(FolderNode))
                                    currentFolder = (FolderNode)currentFolder.Parent;
                                else
                                    break;
                            }

                            if (!added)
                            {
                                Debug.Assert(false);
                                throw new Exception("Error loading CFS");
                            }
                        }

                        if (header.Name == StoragePacket.File)
                        {
                            StorageFile file = StorageFile.Decode(Core.Protocol, header);

                            currentFolder.AddFileInfo(file, false);
                        }
                    }
                }

                stream.Close();
            }
            catch (Exception ex)
            {
                Core.OperationNet.UpdateLog("Storage", "Error loading files " + ex.Message);
            }
        }

        private FolderNode LoadWorking(TreeListNode parent, LocalFolder folder)
        {
            FolderNode node = new FolderNode(this, folder.Info, parent, false);
            node.Archived = folder.Archived;
            node.Integrated = folder.Integrated;

            if (SelectedFolder != null && node.Details.UID == SelectedFolder.Details.UID)
            {
                node.Selected = true;
                SelectedFolder = node;
            }

            if (!folder.Info.IsFlagged(StorageFlags.Archived) || GhostsButton.Checked)
                Utilities.InsertSubNode(parent, node);

            if (parent.GetType() == typeof(FolderNode))
            {
                FolderNode parentNode = (FolderNode)parent;
                parentNode.Folders[folder.Info.UID] = node;
            }

            if (node.Details.IsFlagged(StorageFlags.Modified))
                node.EnsureVisible();


            foreach (LocalFolder sub in folder.Folders.Values)
                LoadWorking(node, sub);

            foreach (LocalFile file in folder.Files.Values)
            {
                FileItem item = new FileItem(this, node, file.Info, false);
                item.Archived = file.Archived;
                item.Integrated = file.Integrated;

                node.Files[file.Info.UID] = item;
            }

            return node;
        }

        private void FolderTreeView_SelectedItemChanged(object sender, EventArgs e)
        {
            if (FolderTreeView.SelectedNodes.Count == 0)
            {
                SelectedInfo.ShowDiffs();
                return;
            }

            FolderNode node = FolderTreeView.SelectedNodes[0] as FolderNode;

            if (node == null)
                return;

            SelectedInfo.ShowItem(node, null);
            SelectFolder(node);
        }

        private void SelectFolder(FolderNode folder)
        {
            if (folder == null)
                return;

            /*
             *             
             * if (!SelectedInfo.IsFile && SelectedInfo.CurrentFolder != null)
            {
                if (SelectedInfo.CurrentFolder.Details.UID == SelectedFolder.Details.UID)
                    SelectedInfo.ShowItem(SelectedFolder, null);
                else
                    SelectedInfo.ShowDefault();
            }
             * */

            bool infoSet = false;
            string infoPath = SelectedInfo.CurrentPath;
            

            SelectedFolder = folder;
            folder.Selected = true;

            string dirpath = null;
            if (Working != null)
                dirpath = Working.RootPath + folder.GetPath();


            ulong selectedUID = 0;
            if (SelectedInfo.CurrentItem != null)
                selectedUID = SelectedInfo.CurrentItem.UID;
            

            FileListView.Items.Clear();
            FileListView.SmallImageList = FileIcons;

            // add sub folders
            if (!FoldersButton.Checked && folder.Parent.GetType() == typeof(FolderNode))
            {
                FileItem dots = new FileItem(this, new FolderNode(this, new StorageFolder(), folder.Parent, false));
                dots.Folder.Details.Name = "..";
                dots.Text = "..";

                if (string.Compare("..", infoPath, true) == 0)
                {
                    infoSet = true;
                    dots.Selected = true;
                    SelectedInfo.ShowDots();
                }

                FileListView.Items.Add(dots);
            }

            foreach (FolderNode sub in folder.Nodes)
            {
                FileItem subItem = new FileItem(this, sub);

                if (string.Compare(sub.GetPath(), infoPath, true) == 0)
                {
                    infoSet = true;
                    subItem.Selected = true;
                    SelectedInfo.ShowItem(sub, null);
                }

                FileListView.Items.Add(subItem);
            }

            // if folder unlocked, add untracked folders, mark temp
            if(folder.Details.IsFlagged(StorageFlags.Unlocked) && Directory.Exists(dirpath))
                try
                {
                    foreach (string dir in Directory.GetDirectories(dirpath))
                    {
                        string name = Path.GetFileName(dir);

                        if (name.CompareTo(".history") == 0)
                            continue;

                        if (folder.GetFolder(name) != null)
                            continue;

                        StorageFolder details = new StorageFolder();
                        details.Name = name;

                        FileItem tempFolder = new FileItem(this, new FolderNode(this, details, folder, true));

                        if (string.Compare(tempFolder.GetPath(), infoPath, true) == 0)
                        {
                            infoSet = true;
                            tempFolder.Selected = true;
                            SelectedInfo.ShowItem(tempFolder.Folder, null);
                        }

                        FileListView.Items.Add(tempFolder);
                    }
                }
                catch { }

            // add tracked files
            foreach (FileItem file in folder.Files.Values)
                if (!file.Details.IsFlagged(StorageFlags.Archived) || GhostsButton.Checked)
                {
                    if (string.Compare(file.GetPath(), infoPath, true) == 0)
                    {
                        infoSet = true;
                        file.Selected = true;
                        SelectedInfo.ShowItem(folder, file);
                    }
                    else
                        file.Selected = false;

                    FileListView.Items.Add(file);
                }

            // if folder unlocked, add untracked files, mark temp
            if (folder.Details.IsFlagged(StorageFlags.Unlocked) && Directory.Exists(dirpath))
                try
                {
                    foreach (string filepath in Directory.GetFiles(dirpath))
                    {
                        string name = Path.GetFileName(filepath);

                        if (folder.GetFile(name) != null)
                            continue;


                        StorageFile details = new StorageFile();
                        details.Name = name;
                        details.InternalSize = new FileInfo(filepath).Length;

                        FileItem tempFile = new FileItem(this, folder, details, true);

                        if (string.Compare(tempFile.GetPath(), infoPath, true) == 0)
                        {
                            infoSet = true;
                            tempFile.Selected = true;
                            SelectedInfo.ShowItem(folder, tempFile);
                        }

                        FileListView.Items.Add(tempFile);
                    }
                }
                catch { }

            UpdateListItems();

            if (!infoSet)
                SelectedInfo.ShowDiffs();
        }



        internal int GetImageIndex(FileItem file)
        {
            if (file.IsFolder)
                return file.Folder.Details.IsFlagged(StorageFlags.Archived) ? 3 : 2;

            string ext = Path.GetExtension(file.Text);

            if (!IconMap.ContainsKey(ext))
            {
                IconMap[ext] = FileIcons.Count;

                Bitmap img = Win32.GetIcon(ext);


                if (img == null)
                    img = new Bitmap(16, 16);

                FileIcons.Add(img);
            }

            if (file.Details != null && file.Details.IsFlagged(StorageFlags.Archived))
                return 1;

            return IconMap[ext];
        }

        void Storages_WorkingFolderUpdate(uint project, string parent, ulong uid, WorkingChange action)
        {
            if (project != ProjectID)
                return;

            try
            {
                LocalFolder workingParent = Working.GetLocalFolder(parent);
                FolderNode treeParent = GetFolderNode(parent);

                LocalFolder workingFolder;
                workingParent.Folders.TryGetValue(uid, out workingFolder);

                FolderNode treeFolder;
                treeParent.Folders.TryGetValue(uid, out treeFolder);


                // working folder can only be null on update or remove action


                if (action == WorkingChange.Created)
                {
                    // create
                    if (treeFolder == null)
                    {
                        LoadWorking(treeParent, workingFolder);
                    }

                    // convert
                    else if(treeFolder.Temp)
                    {
                        treeFolder.Temp = false;
                        treeFolder.Details = workingFolder.Info;
                        treeFolder.Archived = workingFolder.Archived;
                        // changes remains the same
                        treeFolder.Update();
                    }
                }

                else if (action == WorkingChange.Updated)
                {
                    if (workingFolder != null)
                    {
                        if (treeFolder != null)
                        {
                            // check if should be removed
                            if (workingFolder.Info.IsFlagged(StorageFlags.Archived) && !GhostsButton.Checked)
                            {
                                if (treeParent.Nodes.Contains(treeFolder))
                                {
                                    treeParent.Nodes.Remove(treeFolder);

                                    if (SelectedFolder == treeFolder)
                                        SelectFolder(treeParent);
                                }
                            }

                            // update
                            else
                            {
                                treeFolder.Details = workingFolder.Info;
                                treeFolder.Update();

                                if (!SelectedInfo.IsFile && treeFolder == SelectedInfo.CurrentFolder)
                                    SelectedInfo.ShowItem(treeFolder, null);
                            }
                        }

                        // check if should be added
                        else if (!workingFolder.Info.IsFlagged(StorageFlags.Archived) || GhostsButton.Checked)
                        {
                            LoadWorking(treeParent, workingFolder);

                            MarkRescanFolder(workingFolder.Info.UID, treeParent.GetPath(), null, true);
                        }
                    }

                    // local temp folder
                    else
                    {
                        // check if folder a child of selected, refresh view if so
                        if ((treeParent != null && treeParent == SelectedFolder) ||
                            (treeFolder != null && treeFolder == SelectedFolder)) // re-naming un-tracked
                        {
                            RefreshFileList();
                            return;
                        }
                    }

                    
                }

                else if (action == WorkingChange.Removed)
                {
                    // rescan only needed here because changes aren't kept for similar files, so if folder is removed
                    // a rescan needs to be done to re-build changes for sub directories / files

                    string reselectPath = null;

                    if (SelectedFolder.ParentPathContains(treeFolder.Details.UID))
                        reselectPath = SelectedFolder.GetPath();

                   
                    if (treeParent != null && treeParent.Folders.ContainsKey(treeFolder.Details.UID))
                    {
                        treeParent.Folders.Remove(treeFolder.Details.UID);
                        treeParent.Nodes.Remove(treeFolder);
                    }

                    // if selected folders parent's contains deleted uid, reselect
                    if (reselectPath != null)
                    {
                        FolderNode node = null;

                        while (node == null)
                        {
                            node = GetFolderNode(reselectPath);

                            if (node != null)
                            {
                                SelectFolder(node);
                                break;
                            }
                            else if (reselectPath == "")
                            {
                                SelectFolder(RootFolder);
                                break;
                            }

                            reselectPath = Utilities.StripOneLevel(reselectPath);
                        }
                    }
                    
                    if(treeParent != null)
                        MarkRescanFolder(treeFolder.Details.UID, treeParent.GetPath(), null, true);

                }

                if (treeParent != null && treeParent == SelectedFolder)
                    RefreshFileList();

                FolderTreeView.Invalidate();

                CheckWorkingStatus();
            }
            catch (Exception ex)
            {
                Core.OperationNet.UpdateLog("Storage", "Interface:WorkingFolderUpdate: " + ex.Message);
            }
        }

        private void MarkRescanFolder(ulong uid, string path, FolderNode node, bool recurse)
        {
            RescanFolderMap[uid] = new RescanFolder(path, node, recurse);

            NextRescan = 2;

            RescanLabel.Visible = true;

        }

        void Storages_WorkingFileUpdate(uint project, string dir, ulong uid, WorkingChange action)
        {
            if (project != ProjectID)
                return;

             try
             {
                 LocalFolder workingFolder = Working.GetLocalFolder(dir);
                 FolderNode treeFolder = GetFolderNode(dir);

                 if (treeFolder == null || workingFolder == null)
                     return;


                 if (action == WorkingChange.Created)
                 {
                     LocalFile workingFile;
                     workingFolder.Files.TryGetValue(uid, out workingFile);

                     if (workingFile != null)
                     {
                         FileItem treeFile = new FileItem(this, treeFolder, workingFile.Info, false);
                         treeFile.Archived = workingFile.Archived;

                         treeFolder.Files[treeFile.Details.UID] = treeFile;
                     }
                 }

                 else if (action == WorkingChange.Updated)
                 {
                     FileItem treeFile;
                     treeFolder.Files.TryGetValue(uid, out treeFile);

                     LocalFile workingFile;
                     workingFolder.Files.TryGetValue(uid, out workingFile);

                     if (workingFile != null)
                         if (treeFile != null)
                         {
                             treeFile.Details = workingFile.Info;
                             treeFile.Update();
                             UpdateListItems();

                             if (treeFile == SelectedInfo.CurrentFile)
                                 SelectedInfo.ShowItem(SelectedFolder, treeFile);
                         }

                         // not there, add
                         else
                         {
                             treeFile = new FileItem(this, treeFolder, workingFile.Info, false);
                             treeFile.Archived = workingFile.Archived;

                             treeFolder.Files[treeFile.Details.UID] = treeFile;
                         }
                 }

                 else if (action == WorkingChange.Removed)
                 {
                     FileItem treeFile;
                     treeFolder.Files.TryGetValue(uid, out treeFile);

                     if (treeFile != null && treeFolder.Files.ContainsKey(treeFile.Details.UID))
                         treeFolder.Files.Remove(treeFile.Details.UID);
                 }

                 if (treeFolder == SelectedFolder)
                     SelectFolder(treeFolder);  // doesnt 


                 CheckWorkingStatus();
             }
             catch (Exception ex)
             {
                 Core.OperationNet.UpdateLog("Storage", "Interface:WorkingFileUpdate: " + ex.Message);
             }
        }

        internal FolderNode GetFolderNode(string path)
        {
            // path: \a\b\c

            string[] folders = path.Split('\\');

            FolderNode current = RootFolder;

            foreach (string name in folders)
                if (name != "")
                {
                    bool found = false;

                    foreach (FolderNode sub in current.Folders.Values)
                        if (string.Compare(name, sub.Text, true) == 0)
                        {
                            found = true;
                            current = sub;
                            break;
                        }

                    if (!found)
                        return null;
                }

            return current;
        }

        private void SaveLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Storages.SaveLocal(ProjectID);

            RefreshView();

            CheckWorkingStatus();
        }

        private void DiscardLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Working = Storages.Discard(ProjectID);

            RefreshView();

            CheckWorkingStatus();
        }

        private void FolderTreeView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            FolderNode node = FolderTreeView.GetNodeAt(e.Location) as FolderNode;

            if (node == null)
                return;

            ContextMenuStripEx menu = new ContextMenuStripEx();

            if (!node.Temp)
            {
                if (node.Details.IsFlagged(StorageFlags.Unlocked))
                    menu.Items.Add(MenuLock);
                else
                    menu.Items.Add(MenuUnlock);
            }
            else
                menu.Items.Add(MenuAdd); // remove in web interface\


            if (node != RootFolder && !node.Temp)
                menu.Items.Add(MenuDetails);

            if (IsLocal && node != RootFolder && !node.Temp)
            {
                menu.Items.Add("-");

                if (node.Details.IsFlagged(StorageFlags.Archived))
                    menu.Items.Add(MenuRestore);

                menu.Items.Add(MenuDelete);
            }


            if (menu.Items.Count > 0)
            {
                LastSelectedView = FolderTreeView;
                menu.Show(FolderTreeView, e.Location);
            }
        }

        private void FileListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            FileItem clicked = FileListView.GetItemAt(e.Location) as FileItem;

            ContextMenuStripEx menu = new ContextMenuStripEx();

            if (clicked == null)
            {
                SelectedInfo.ShowDiffs();

                if (Working != null)
                {
                    menu.Items.Add(new ToolStripMenuItem("Create Folder", StorageRes.Folder, new EventHandler(Folder_Create)));
                    menu.Show(FileListView, e.Location);
                }

                return;
            }


            // add to storage, lock, unlock, restore, delete
            bool firstLoop = true;
            List<ToolStripMenuItem> potentialMenus = new List<ToolStripMenuItem>();

            foreach (FileItem item in FileListView.SelectedItems)
            {
                if (item.Text.CompareTo("..") == 0)
                    continue;

                // create list of items to add, intersect with current list, first run is free
                potentialMenus.Clear();

                // add
                if (IsLocal && item.Temp)
                    potentialMenus.Add(MenuAdd);

                // lock / unlock
                if (!item.Temp)
                    if (item.IsFolder)
                    {
                        if (item.Folder.Details.IsFlagged(StorageFlags.Unlocked))
                            potentialMenus.Add(MenuLock);
                        else
                            potentialMenus.Add(MenuUnlock);
                    }
                    else if (item.Details != null)
                    {
                        if (item.IsUnlocked())
                            potentialMenus.Add(MenuLock);
                        else
                            potentialMenus.Add(MenuUnlock);
                    }

                // details
                if (!item.Temp && FileListView.SelectedItems.Count == 1)
                    potentialMenus.Add(MenuDetails);

                // delete / restore
                if (IsLocal && !item.Temp)
                    if (item.IsFolder)
                    {
                        if (item.Folder.Details.IsFlagged(StorageFlags.Archived))
                            potentialMenus.Add(MenuRestore);

                        potentialMenus.Add(MenuDelete);
                    }

                    else if (item.Details != null)
                    {
                        if (item.Details.IsFlagged(StorageFlags.Archived))
                            potentialMenus.Add(MenuRestore);

                        potentialMenus.Add(MenuDelete);
                    }

                // initial list
                if (firstLoop)
                {
                    foreach (ToolStripMenuItem potential in potentialMenus)
                        menu.Items.Add(potential);

                    firstLoop = false;
                    continue;
                }

                // intersect both ways
                foreach (ToolStripMenuItem potential in potentialMenus)
                    if (!menu.Items.Contains(potential))
                        menu.Items.Remove(potential);

                List<ToolStripMenuItem> selfRemove = new List<ToolStripMenuItem>();
                foreach (ToolStripMenuItem current in menu.Items)
                    if (!potentialMenus.Contains(current))
                        selfRemove.Add(current);

                 foreach (ToolStripMenuItem current in selfRemove)
                     menu.Items.Remove(current);
            }


            // place '-' before restore or delete
            int i = 0;
            bool separator = false;
            foreach (ToolStripMenuItem item in menu.Items)
            {
                if (item.Text == "Restore" || item.Text == "Delete")
                {
                    separator = true;
                    break;
                }

                i++;
            }

            if (separator && i > 0)
                menu.Items.Insert(i, new ToolStripSeparator());

            
            /* // add
                if (IsLocal && clicked.Temp)
                    menu.Items.Add(new FileMenuItem("Add to Storage", clicked, null, File_Add)); // remove in web interface\

                // lock
                if (!clicked.Temp)
                    if (clicked.IsFolder)
                    {
                        if (clicked.Folder.Details.IsFlagged(StorageFlags.Unlocked))
                            menu.Items.Add(new FolderMenuItem("Lock", clicked.Folder, StorageRes.Locked, Folder_Lock));
                        else
                            menu.Items.Add(new FolderMenuItem("Unlock", clicked.Folder, StorageRes.Unlocked, Folder_Unlock));
                    }
                    else if (clicked.Details != null)
                    {
                        if (clicked.IsUnlocked())
                            menu.Items.Add(new FileMenuItem("Lock", clicked, StorageRes.Locked, File_Lock));
                        else
                            menu.Items.Add(new FileMenuItem("Unlock", clicked, StorageRes.Unlocked, File_Unlock));
                    }

                // details
                if (!clicked.Temp)
                    if (clicked.IsFolder)
                        menu.Items.Add(new FolderMenuItem("Details", clicked.Folder, StorageRes.details, Folder_Details));
                    else
                        menu.Items.Add(new FileMenuItem("Details", clicked, StorageRes.details, File_Details));

                // delete / restore
                if (IsLocal && !clicked.Temp)
                    if (clicked.IsFolder)
                    {
                        menu.Items.Add("-");

                        if (clicked.Folder.Details.IsFlagged(StorageFlags.Archived))
                            menu.Items.Add(new FolderMenuItem("Restore", clicked.Folder, null, Folder_Restore));

                        menu.Items.Add(new FolderMenuItem("Delete", clicked.Folder, StorageRes.Reject, Folder_Delete));
                    }

                    else if (clicked.Details != null)
                    {
                        menu.Items.Add("-");

                        if (clicked.Details.IsFlagged(StorageFlags.Archived))
                            menu.Items.Add(new FileMenuItem("Restore", clicked, null, File_Restore));

                        menu.Items.Add(new FileMenuItem("Delete", clicked, StorageRes.Reject, File_Delete));
                    }
                */



            if (menu.Items.Count > 0)
            {
                LastSelectedView = FileListView;
                menu.Show(FileListView, e.Location);
            }
        }

        void FileView_Add(object sender, EventArgs e)
        { 
            
            // folder view
            if (LastSelectedView == FolderTreeView)
            {

                if(FolderTreeView.SelectedNodes.Count > 0)
                    AddNewFolder((FolderNode)FolderTreeView.SelectedNodes[0]);
                
            }

            // file view
            else
            {
                foreach (FileItem item in FileListView.SelectedItems)
                {
                    if (item.IsFolder)
                    {
                        AddNewFolder(item.Folder);
                        continue;
                    }

                    // if no changes then temp local, add path
                    if (item.Changes.Count == 0)
                        Working.TrackFile(item.GetPath()); // add through working path

                    // if 1 change, then file is remote, add item
                    else if (item.Changes.Count == 1)
                        Working.TrackFile(item.Folder.GetPath(), (StorageFile)GetFirstItem(item.Changes));

                    // if more than 1 change, them multiple remotes
                    else if (item.Changes.Count > 1)
                        MessageBox.Show("Select specific file from changes below to add", item.Details.Name);
                }
            }
           
        }

        private StorageItem GetFirstItem(Dictionary<ulong, StorageItem> dictionary)
        {
            Dictionary<ulong, StorageItem>.Enumerator enumer = dictionary.GetEnumerator();

            if (enumer.MoveNext())
                return enumer.Current.Value;

            return null;
        }

        void AddNewFolder(FolderNode folder)
        {
            // if no changes then temp local, add path
            if (folder.Changes.Count == 0)
                Working.TrackFolder(folder.GetPath()); // add through working path

            // if 1 change, then file is remote, add item
            else if (folder.Changes.Count == 1)
                Working.TrackFolder(folder.GetPath(), (StorageFolder)GetFirstItem(folder.Changes));

            // if more than 1 change, them multiple remotes
            else if (folder.Changes.Count > 1)
                MessageBox.Show("Select specific folder from changes below to add");
        }

        void FileView_Restore(object sender, EventArgs e)
        {
            // folder view
            if (LastSelectedView == FolderTreeView)
            {

                if (FolderTreeView.SelectedNodes.Count > 0)
                {
                    FolderNode folder = (FolderNode)FolderTreeView.SelectedNodes[0];
                    Working.RestoreFolder(folder.GetPath());
                }

            }

            // file view
            else
            {
                foreach (FileItem item in FileListView.SelectedItems)
                {
                    if (item.IsFolder)
                    {
                        Working.RestoreFolder(item.Folder.GetPath());
                        continue;
                    }

                    Working.RestoreFile(item.Folder.GetPath(), item.Details.Name);
                }
            }

        }

        void FileView_Delete(object sender, EventArgs e)
        {
            List<string> tempFolders = new List<string>();
            List<string> tempFiles = new List<string>();

            List<FolderNode> folders = new List<FolderNode>();
            List<FileItem> files = new List<FileItem>();


            // folder view
            if (LastSelectedView == FolderTreeView)
            {

                if (FolderTreeView.SelectedNodes.Count > 0)
                {
                    FolderNode folder = (FolderNode)FolderTreeView.SelectedNodes[0];

                    if (folder.Temp)
                        tempFolders.Add(Working.RootPath + folder.GetPath());
                    else
                        folders.Add(folder);
                }

            }

            // file view
            else
            {
                foreach (FileItem item in FileListView.SelectedItems)
                {
                    if (item.IsFolder)
                    {
                        if (item.Folder.Temp)
                            tempFolders.Add(Working.RootPath + item.Folder.GetPath());
                        else
                            folders.Add(item.Folder);

                        continue;
                    }

                    if (item.Temp)
                        tempFiles.Add(Working.RootPath + item.GetPath());
                    else
                        files.Add(item);
                }
            }

            try
            {
                // handle temps
                if (tempFiles.Count > 0 || tempFolders.Count > 0)
                {
                    string message = "";

                    foreach (string name in tempFiles)
                        message += name + "\n";
                    foreach (string name in tempFolders)
                        message += name + "\n";

                    DialogResult result = MessageBox.Show(this, "Are you sure you want to delete?\n '" + message, "Delete", MessageBoxButtons.YesNoCancel);

                    if (result == DialogResult.Cancel)
                        return;

                    if (result == DialogResult.Yes)
                    {
                        foreach (string path in tempFiles)
                            File.Delete(path);
                        foreach (string path in tempFolders)
                            Directory.Delete(path, true);
                    }
                }

                // handle rest
                if (files.Count > 0 || folders.Count > 0)
                {
                    string message = "";

                    foreach (FileItem file in files)
                        message += file.Details.Name + "\n";
                    foreach (FolderNode node in folders)
                        message += node.Details.Name + "\n";

                    DialogResult result = MessageBox.Show(this, "Would you like to keep these files archived?\n" + message, "Delete", MessageBoxButtons.YesNoCancel);

                    if (result == DialogResult.Cancel)
                        return;

                    // archive
                    if (result == DialogResult.Yes)
                    {
                        foreach (FileItem file in files)
                            Working.ArchiveFile(file.Folder.GetPath(), file.Details.Name);
                        foreach (FolderNode folder in folders)
                            Working.ArchiveFolder(folder.GetPath());
                    }

                    // delete
                    if (result == DialogResult.No)
                    {
                        foreach (FileItem file in files)
                            Working.DeleteFile(file.Folder.GetPath(), file.Details.Name);
                        foreach (FolderNode folder in folders)
                            Working.DeleteFolder(folder.GetPath());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        void FileView_Lock(object sender, EventArgs e)
        {
            List<LockError> errors = new List<LockError>();
            List<FolderNode> folders = new List<FolderNode>();

            // folder view
            if (LastSelectedView == FolderTreeView)
            {

                if (FolderTreeView.SelectedNodes.Count > 0)
                    folders.Add( (FolderNode)FolderTreeView.SelectedNodes[0]);

            }

            // file view
            else
            {
                foreach (FileItem item in FileListView.SelectedItems)
                {
                    if (item.IsFolder)
                    {
                        folders.Add(item.Folder);
                        continue;
                    }

                    Storages.LockFileCompletely(DhtID, ProjectID, item.Folder.GetPath(), item.Archived, errors);
                }
            }

            // lock folders
            bool lockSubs = false;

            foreach(FolderNode folder in folders)
                if (folder.Nodes.Count > 0)
                {
                    if (MessageBox.Show("Lock sub-folders as well?", "Lock", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        lockSubs = true;

                    break;
                }

            foreach (FolderNode folder in folders)
                LockFolder(folder, lockSubs, errors);

            RefreshFileList();
            
            LockMessage.Alert(this, errors);      
        }


        void FileView_Unlock(object sender, EventArgs e)
        {
            List<LockError> errors = new List<LockError>();
            List<FolderNode> folders = new List<FolderNode>();

            // folder view
            if (LastSelectedView == FolderTreeView)
            {

                if (FolderTreeView.SelectedNodes.Count > 0)
                    folders.Add((FolderNode)FolderTreeView.SelectedNodes[0]);

            }

            // file view
            else
            {
                foreach (FileItem item in FileListView.SelectedItems)
                {
                    if (item.IsFolder)
                    {
                        folders.Add(item.Folder);
                        continue;
                    }

                    Storages.UnlockFile(DhtID, ProjectID, item.Folder.GetPath(), (StorageFile)item.Details, false, errors);
                }
            }

            // unlock folders
            bool unlockSubs = false;

            foreach (FolderNode folder in folders)
                if (folder.Nodes.Count > 0)
                {
                    if (MessageBox.Show("Unlock sub-folders as well?", "Unlock", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        unlockSubs = true;

                    break;
                }

            foreach (FolderNode folder in folders)
                UnlockFolder(folder, unlockSubs, errors);

            RefreshFileList();
            
            // set watch unlocked directories for changes
            if (Working != null)
                Working.StartWatchers();
            
            LockMessage.Alert(this, errors);
        }

        internal string UnlockFile(FileItem file)
        {
            List<LockError> errors = new List<LockError>();

            string path = Storages.UnlockFile(DhtID, ProjectID, file.Folder.GetPath(), (StorageFile)file.Details, false, errors);

            LockMessage.Alert(this, errors);

            file.Update();
            SelectedInfo.RefreshItem();

            return path;
        }

        internal void LockFile(FileItem file)
        {
            List<LockError> errors = new List<LockError>();

            Storages.LockFileCompletely(DhtID, ProjectID, file.Folder.GetPath(), file.Archived, errors);

            LockMessage.Alert(this, errors);

            file.Update();
            SelectedInfo.RefreshItem();
        }

        void FileView_Details(object sender, EventArgs e)
        {
            DetailsForm details = null;

            // folder view
            if (LastSelectedView == FolderTreeView)
            {

                if (FolderTreeView.SelectedNodes.Count > 0)
                {
                    details = new DetailsForm(this, (FolderNode)FolderTreeView.SelectedNodes[0]);
                    details.ShowDialog(this);
                    return;
                }

            }

            // file view
            else
            {
                foreach (FileItem item in FileListView.SelectedItems)
                {
                    if (item.IsFolder)
                    {
                        details = new DetailsForm(this, item.Folder);
                        details.ShowDialog(this);
                        return;

                        continue;
                    }

                    details = new DetailsForm(this, SelectedFolder, item);
                    details.ShowDialog(this);
                    return;
                }
            }
        }

        void Folder_Create(object sender, EventArgs args)
        {
            GetTextDialog dialog = new GetTextDialog("Create Folder", "Enter a name for the new folder", "New Folder");

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    if (dialog.ResultBox.Text.Length == 0)
                        throw new Exception("Folder needs to have a name");

                    if (SelectedFolder.GetFolder(dialog.ResultBox.Text) != null)
                        throw new Exception("Folder with same name already exists");

                    if (dialog.ResultBox.Text.IndexOf('\\') != -1)
                        throw new Exception("Folder name contains invalid characters");

                    Working.TrackFolder(SelectedFolder.GetPath() + "\\" + dialog.ResultBox.Text);

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        internal void LockFolder(FolderNode folder, bool subs, List<LockError> errors)
        {
            string path = folder.GetPath();

            string wholepath = Storages.GetRootPath(DhtID, ProjectID) + path;

            List<string> stillLocked = new List<string>();

            foreach (FileItem file in folder.Files.Values)
                if (!file.Temp)
                {
                    Storages.LockFileCompletely(DhtID, ProjectID, path, file.Archived, errors);

                    string filepath = wholepath + "\\" + file.Details.Name;
                    if (File.Exists(filepath))
                        stillLocked.Add(filepath);
                }

            folder.Details.RemoveFlag(StorageFlags.Unlocked);
            folder.Update();

            if (subs)
                foreach (FolderNode subfolder in folder.Folders.Values)
                    if (!subfolder.Temp)
                        LockFolder(subfolder, subs, errors);

            Storages.DeleteFolder(wholepath, errors, stillLocked);
        }

        internal void UnlockFolder(FolderNode folder, bool subs, List<LockError> errors)
        {
            string path = folder.GetPath();
            string root = Storages.GetRootPath(DhtID, ProjectID);

            if(!Storages.CreateFolder(root + path, errors, subs))
                return;

            if (Directory.Exists(root + path))
            {
                // set flag
                folder.Details.SetFlag(StorageFlags.Unlocked);
                folder.Update();

                // unlock files
                foreach (FileItem file in folder.Files.Values)
                    if (!file.Temp && !file.Details.IsFlagged(StorageFlags.Archived))
                        Storages.UnlockFile(DhtID, ProjectID, path, (StorageFile)file.Details, false, errors);

                // unlock subfolders
                if (subs)
                    foreach (FolderNode subfolder in folder.Folders.Values)
                        if (!subfolder.Details.IsFlagged(StorageFlags.Archived))
                            UnlockFolder(subfolder, subs, errors);
            }
        }

        private void SecondTimer_Tick(object sender, EventArgs e)
        {
            // status of hashing
            CheckWorkingStatus();

            // keeps track of lil download icon on file
            if (WatchTransfers)
                UpdateListItems();

            SelectedInfo.SecondTimer();

            // rescan remote files against specific locals
            // happens when local file / folders are changed
            if (NextRescan > 0 && !Storages.HashingActive())
            {
                NextRescan--;

                if (NextRescan == 0)
                {
                    foreach (RescanFolder folder in RescanFolderMap.Values)
                        if (folder.Node != null)
                            folder.Node.Changes.Clear();

                    foreach (ulong id in CurrentDiffs)
                        RescanDiff(id);


                    // if selected folder / selected items / info panel in uid map - refresh
                    if (RescanFolderMap.ContainsKey(SelectedFolder.Details.UID))
                        RefreshFileList();

                    foreach (FileItem item in FileListView.Items)
                        if (item.IsFolder && RescanFolderMap.ContainsKey(item.Details.UID))
                            item.Update();

                    if (SelectedInfo.CurrentFolder != null && RescanFolderMap.ContainsKey(SelectedInfo.CurrentFolder.Details.UID))
                        SelectedInfo.RefreshItem();


                    // clear maps
                    RescanFolderMap.Clear();

                    RescanLabel.Visible = false;
                }
            }
        }

        private void RescanDiff(ulong id)
        {
            /*
             * see if uid exists in rescan map
            if file 
                add to changes
            if folder
                add temp if uid doesnt exist
                set inTarget[uid], add further uids if recurse set
             */


            if (!Storages.StorageMap.ContainsKey(id))
                return;

            StorageHeader headerx = Storages.StorageMap[id].Header;

            string path = Storages.GetFilePath(headerx);

            if (!File.Exists(path))
                return;

            try
            {
                FileStream filex = new FileStream(path, FileMode.Open);
                CryptoStream crypto = new CryptoStream(filex, headerx.FileKey.CreateDecryptor(), CryptoStreamMode.Read);
                PacketStream stream = new PacketStream(crypto, Core.Protocol, FileAccess.Read);

                ulong remoteUID = 0;
                FolderNode currentFolder = RootFolder;
                bool readingProject = false;

                G2Header header = null;

                List<ulong> Recursing = new List<ulong>();


                while (stream.ReadPacket(ref header))
                {
                    if (header.Name == StoragePacket.Root)
                    {
                        StorageRoot root = StorageRoot.Decode(Core.Protocol, header);

                        readingProject = (root.ProjectID == ProjectID);
                    }

                    if (readingProject)
                    {
                        if (header.Name == StoragePacket.Folder)
                        {
                            StorageFolder folder = StorageFolder.Decode(Core.Protocol, header);

                            // if new UID 
                            if (remoteUID == folder.UID)
                                continue;

                            remoteUID = folder.UID;

                           if (RescanFolderMap.ContainsKey(remoteUID))
                                if (RescanFolderMap[remoteUID].Recurse)
                                    Recursing.Add(remoteUID);

                            if (Recursing.Count > 0 || RescanFolderMap.ContainsKey(remoteUID))
                            {
                                bool added = false;

                                while (!added)
                                {
                                    if (currentFolder.Details.UID == folder.ParentUID)
                                    {
                                        // if folder exists with UID
                                        if (currentFolder.Folders.ContainsKey(remoteUID))
                                            currentFolder = currentFolder.Folders[remoteUID];

                                        // else add folder as temp, mark as changed
                                        else
                                            currentFolder = currentFolder.AddFolderInfo(folder, true);

                                        // diff file properties, if different, add as change
                                        if (currentFolder.Temp || Storages.ItemDiff(currentFolder.Details, folder) != StorageActions.None)
                                        {
                                            currentFolder.Changes[id] = folder;
                                            currentFolder.UpdateOverlay();
                                        }

                                        added = true;
                                    }
                                    else if (currentFolder.Parent.GetType() == typeof(FolderNode))
                                    {
                                        // if current folder id equals recurse marker stop recursing
                                        if (Recursing.Contains(currentFolder.Details.UID))
                                            Recursing.Remove(currentFolder.Details.UID);

                                        currentFolder = (FolderNode)currentFolder.Parent;
                                    }
                                    else
                                        break;
                                }
                            }
                        }

                        if (header.Name == StoragePacket.File)
                        {
                            StorageFile file = StorageFile.Decode(Core.Protocol, header);

                            // if new UID 
                            if (remoteUID == file.UID)
                                continue;

                            remoteUID = file.UID;

                            if (Recursing.Count > 0 /*|| RescanFileMap.ContainsKey(remoteUID)*/)
                            {
                                FileItem currentFile = null;

                                // if file exists with UID
                                if (currentFolder.Files.ContainsKey(remoteUID))
                                    currentFile = currentFolder.Files[remoteUID];

                                // else add file as temp, mark as changed
                                else
                                    currentFile = currentFolder.AddFileInfo(file, true);

                                //crit check if file is integrated

                                if (currentFile.Temp || Storages.ItemDiff(currentFile.Details, file) != StorageActions.None)
                                    currentFile.Changes[id] = file;
                            }
                        }
                    }
                }

                stream.Close();
            }
            catch
            {
            }
        }

        private void CheckWorkingStatus()
        {
            if (!IsLocal)
                return;

            if (Storages.HashingActive())
            {
                if (!ChangesLabel.Visible || SaveLink.Visible)
                {
                    ChangesLabel.Visible = true;
                    SaveLink.Visible = false;
                    DiscardLink.Visible = false;

                    splitContainer1.Height = Height - 18 - toolStrip1.Height;
                }

                ChangesLabel.Text = "Processing " + (Storages.HashQueue.Count).ToString() + " Changes...";
            }

            else if (Working.Modified)
            {
                if (!SaveLink.Visible)
                {
                    ChangesLabel.Visible = true;
                    SaveLink.Visible = true;
                    DiscardLink.Visible = true;

                    splitContainer1.Height = Height - 18 - toolStrip1.Height;

                    ChangesLabel.Text = "Changes";
                }
            }
            else
            {
                if (ChangesLabel.Visible)
                {
                    ChangesLabel.Visible = false;
                    SaveLink.Visible = false;
                    DiscardLink.Visible = false;
                    splitContainer1.Height = Height - toolStrip1.Height;
                }
            }
        }

        private void FoldersButton_CheckedChanged(object sender, EventArgs e)
        {
            splitContainer1.Panel1Collapsed = !FoldersButton.Checked;

            RefreshFileList(); // puts .. dir if needed
        }

        private void FileListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (FileListView.SelectedItems.Count == 0)
            {
                SelectedInfo.ShowDiffs();
                return;
            }

            FileItem file = (FileItem)FileListView.SelectedItems[0];

            if (file.IsFolder)
                SelectedInfo.ShowItem(file.Folder, null);
            else
                SelectedInfo.ShowItem(file.Folder, file);
        }

        private void FileListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (FileListView.SelectedItems.Count == 0)
                return;

            FileItem file = (FileItem)FileListView.SelectedItems[0];


            // if folder
            if (file.IsFolder)
            {
                if (file.Temp)
                    return;

                FolderNode node = file.Text == ".." ? (FolderNode)file.Folder.Parent : file.Folder;

                SelectedInfo.ShowItem(node, null);
                SelectFolder(node);

                return;
            }

            // if file exists in storage, or if the file is a local temp file
            if (Storages.FileExists((StorageFile)file.Details) || (file.Temp && file.Changes.Count == 0))
                OpenFile(file);

            else
            {
                Storages.DownloadFile(DhtID, (StorageFile)file.Details);

               
                UpdateListItems();
                SelectedInfo.ShowItem(SelectedFolder, file);
            }
               
        }

        internal void UpdateListItems()
        {
            // if any item in list is transferring, set watch transfers

            // set file icons and overlays, call invalidate

            foreach (FileItem item in FileListView.Items)
            {
                item.Update();

                // should only be called when images are displayed            
                item.ImageIndex = GetImageIndex(item);

                item.UpdateOverlay();

                if (!item.IsFolder && Storages.FileStatus((StorageFile)item.Details) != null)
                {
                    item.Overlays.Add(2);
                    WatchTransfers = true;
                }
            }

            FileListView.Invalidate();
        }

        internal void OpenFile(FileItem file)
        {
            string path = null;


            // if temp and changes exist (then remote)
            if (file.Temp)
            {
                // remote file that doesnt exist local yet
                if (file.Changes.Count > 0)
                {
                    foreach (ulong id in file.Changes.Keys)
                        if (file.Changes[id] == file.Details)
                            path = UnlockFile(file);
                }

                // local temp file
                else
                    path = Storages.GetRootPath(DhtID, ProjectID) + file.GetPath();
            }

            // non temp file
            else
                path = UnlockFile(file);

            if (path != null && File.Exists(path))
                Process.Start(path);

            file.Update();
            FileListView.Invalidate();
        }

        private void FileListView_DragEnter(object sender, DragEventArgs e)
        {
            if(Working != null)
                e.Effect = DragDropEffects.All;
        }

        private void FileListView_DragDrop(object sender, DragEventArgs e)
        {
            if (SelectedFolder == null)
                return;

            if (DragSelect != null)
            {
                Dragging = false;
                DragSelect = null;
                return;
            }

            if (Working == null) // can only drop files into local repo
                return;

            // Handle FileDrop data.
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Assign the file names to a string array, in 
                // case the user has selected multiple files.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                try
                {
                    foreach(string sourcePath in files)
                    {

                        string name = Path.GetFileName(sourcePath);
                        string destPath = SelectedFolder.GetPath();

                        // copy
                        Directory.CreateDirectory(Working.RootPath + destPath);
                        File.Copy(sourcePath, Working.RootPath + destPath + "\\" + name);

                        // track
                        Working.TrackFile(destPath + "\\" + name);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }
            }
        }

        FileItem DragSelect;
        bool Dragging;
        Point DragStart;

        private void FileListView_MouseDown(object sender, MouseEventArgs e)
        {
            FileItem file = FileListView.GetItemAt(e.Location) as FileItem;


            if (file == null || file.IsFolder)
                return;

            DragSelect = file;
            DragStart = e.Location;
        }
        
        private void FileListView_MouseMove(object sender, MouseEventArgs e)
        {
            if (DragSelect != null && !Dragging && GetDistance(DragStart, e.Location) > 4)
            {
                string path = Storages.GetRootPath(DhtID, ProjectID) + DragSelect.GetPath();
                string[] paths = new string[] { path };

                DataObject data = new DataObject(DataFormats.FileDrop, paths);

                FileListView.DoDragDrop(data, DragDropEffects.Copy);
                Dragging = true;
            }
        }

        private int GetDistance(Point start, Point end)
        {
            int x = end.X - start.X;
            int y = end.Y - start.Y;

            return (int)Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
        }

        private void FileListView_MouseUp(object sender, MouseEventArgs e)
        {
            DragSelect = null;
            Dragging = false;
        }

        private void FileListView_DragLeave(object sender, EventArgs e)
        {

        }

        private void FileListView_DragOver(object sender, DragEventArgs e)
        {

        }

        private void FileListView_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {

        }

        private void FileListView_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if(e.Action == DragAction.Cancel || e.Action == DragAction.Drop)
            {
                StorageFile file = (StorageFile) DragSelect.Details;

                if (e.Action == DragAction.Drop && Storages.FileExists(file))
                    UnlockFile(DragSelect);
                
                DragSelect = null;
                Dragging = false;
            }
        }

        internal void RefreshFileList()
        {
            SelectFolder(SelectedFolder);
        }


    }

    internal class FolderNode : TreeListNode 
    {
        internal bool Temp;
        internal StorageFolder Details;
        StorageView View;

        internal LinkedList<StorageItem> Archived = new LinkedList<StorageItem>();
        internal Dictionary<ulong, StorageItem> Integrated = new Dictionary<ulong, StorageItem>();
        internal Dictionary<ulong, StorageItem> Changes = new Dictionary<ulong, StorageItem>();

        internal Dictionary<ulong, FolderNode> Folders = new Dictionary<ulong, FolderNode>();
        internal Dictionary<ulong, FileItem> Files = new Dictionary<ulong, FileItem>();

        //Nodes - contains sub-folders

        internal FolderNode(StorageView view, StorageFolder folder, TreeListNode parent, bool temp)
        {
            Parent = parent; // needs to be set because some nodes (archived) aren't proper children of parents (not in Nodes list), still needed though so getPath works

            if (parent == null)
                throw new Exception("Parent set to null");

            View = view;

            Details = folder;
            Temp = temp;

            Update();
        }

        internal FolderNode GetFolder(string name)
        {
            foreach (FolderNode folder in Folders.Values)
                if (string.Compare(name, folder.Text, true) == 0)
                    return folder;

            return null;
        }

        internal FileItem GetFile(string name)
        {
            foreach (FileItem file in Files.Values)
                if (string.Compare(name, file.Text, true) == 0)
                    return file;

            return null;
        }

        internal void Update()
        {
            Text = Details.Name;

            if (Details.IsFlagged(StorageFlags.Modified))
                ForeColor = Color.DarkRed;
            else if (Temp || Details.IsFlagged(StorageFlags.Archived))
                ForeColor = Color.Gray;
            else
                ForeColor = Color.Black;

            if (Details.IsFlagged(StorageFlags.Unlocked))
                this.Font = View.BoldFont;
            else
                this.Font = View.RegularFont;

            ImageIndex = Details.IsFlagged(StorageFlags.Archived) ? 1 : 0;


            UpdateOverlay();

            if(TreeList != null)
                TreeList.Invalidate();
        }

        internal string GetPath()
        {
            string path = "";

            FolderNode up = this;

            while (up.Parent.GetType() == typeof(FolderNode))
            {
                path = "\\" + up.Details.Name + path;
                up = up.Parent as FolderNode;
            }

            return path;
        }



        internal FolderNode AddFolderInfo(StorageFolder info, bool remote)
        {
            if (!Folders.ContainsKey(info.UID))
            {
                Folders[info.UID] = new FolderNode(View, info, this, remote);

                if (!info.IsFlagged(StorageFlags.Archived) || View.GhostsButton.Checked)
                    Utilities.InsertSubNode(this, Folders[info.UID]);
            }

            FolderNode folder = Folders[info.UID];

            if (!remote)
            {
                if (info.IntegratedID != 0)
                    folder.Integrated[info.IntegratedID] = info;
                else
                    folder.Archived.AddLast(info);
            }

            return folder;
        }

        internal FileItem AddFileInfo(StorageFile info, bool remote)
        {
            if (!Files.ContainsKey(info.UID))
                Files[info.UID] = new FileItem(View, this, info, remote);

            FileItem file = Files[info.UID];

            if (!remote)
            {
                if (info.IntegratedID != 0)
                    file.Integrated[info.IntegratedID] = info;
                else
                    file.Archived.AddLast(info);
            }

            return file;
        }

        internal void UpdateOverlay()
        {
            if (Overlays == null)
                Overlays = new List<int>();

            Overlays.Clear();

            if (Temp)
                Overlays.Add(2);

            bool showHigher = false, showLower = false;

            foreach (ulong id in GetRealChanges().Keys)
                if (View.HigherIDs.Contains(id))
                    showHigher = true;
                else
                    showLower = true;

            if (showHigher)
                this.Overlays.Add(0);

            if (showLower)
                this.Overlays.Add(1);
        }

        internal Dictionary<ulong, StorageItem> GetRealChanges()
        {
            Dictionary<ulong, StorageItem> realChanges = new Dictionary<ulong, StorageItem>();

            foreach (ulong id in Changes.Keys)
            {
                bool add = true;
                StorageItem change = Changes[id];

                // dont display if current file is equal to this change
                if (!Temp && View.Storages.ItemDiff(change, Details) == StorageActions.None)
                    add = false;

                // dont display if change is integrated
                if (Integrated.ContainsKey(id))
                    if (View.Storages.ItemDiff(change, Integrated[id]) == StorageActions.None)
                        add = false;

                if (add)
                    realChanges.Add(id, change);
            }

            return realChanges;
        }

        public override string ToString()
        {
            return Details.Name;
        }

        internal bool ParentPathContains(ulong uid)
        {
            if (Details.UID == uid)
                return true;

            if (Parent.GetType() == typeof(FolderNode))
                return ((FolderNode)Parent).ParentPathContains(uid);

            return false;
        }
    }

    internal class FileItem : ContainerListViewItem
    {
        internal StorageItem Details;
        StorageView View;

        internal LinkedList<StorageItem> Archived = new LinkedList<StorageItem>();
        internal Dictionary<ulong, StorageItem> Integrated = new Dictionary<ulong, StorageItem>();
        internal Dictionary<ulong, StorageItem> Changes = new Dictionary<ulong, StorageItem>();

        internal bool Temp;
        internal bool IsFolder;
        internal FolderNode Folder;

        internal FileItem(StorageView view, FolderNode parent, StorageFile file, bool temp)
        {
            View = view;
            Folder = parent;

            SubItems.Add("");
            SubItems.Add("");

            Details = file;
            Temp = temp;

            Update();
        }

        internal FileItem(StorageView view, FolderNode folder)
        {
            View = view;
            Folder = folder;

            SubItems.Add("");
            SubItems.Add("");

            IsFolder = true;
            ImageIndex = folder.Details.IsFlagged(StorageFlags.Archived) ? 3 : 2;
            
            Details = folder.Details;
            Changes = folder.Changes;
            Temp = folder.Temp;

            Update();
        }

        internal void Update()
        {
            Text = Details.Name;

            if (Details.IsFlagged(StorageFlags.Modified))
                ForeColor = Color.DarkRed;
            else if (Temp || Details.IsFlagged(StorageFlags.Archived))
                ForeColor = Color.Gray;
            else
                ForeColor = Color.Black;

            if (IsUnlocked())
                this.Font = View.BoldFont;
            else
                this.Font = View.RegularFont;

            if (Details.GetType() == typeof(StorageFile))
                SubItems[0].Text = Utilities.ByteSizetoString(((StorageFile)Details).InternalSize);

            if (!Temp)
                SubItems[1].Text = Details.Date.ToString();
        }

        internal bool IsUnlocked()
        {
            return Details.IsFlagged(StorageFlags.Unlocked) ||
                View.Storages.IsHistoryUnlocked(View.DhtID, View.ProjectID, Folder.GetPath(), Archived);
        }

        internal string GetPath()
        {
            if (IsFolder)
                return Folder.GetPath();

            return Folder.GetPath() + "\\" + Details.Name;
        }

        internal void UpdateOverlay()
        {
            if (Overlays == null)
                Overlays = new List<int>();

            Overlays.Clear();

            if(Temp)
                Overlays.Add(3);

            bool showHigher = false, showLower = false;

            foreach (ulong id in GetRealChanges().Keys)
                if (View.HigherIDs.Contains(id))
                    showHigher = true;
                else
                    showLower = true;

            if (showHigher)
                Overlays.Add(0);

            if (showLower)
                Overlays.Add(1);
        }

        internal Dictionary<ulong, StorageItem> GetRealChanges()
        {
            Dictionary<ulong, StorageItem> realChanges = new Dictionary<ulong, StorageItem>();

            foreach (ulong id in Changes.Keys)
            {
                bool add = true;
                StorageItem change = Changes[id];

                // dont display if current file is equal to this change
                if (!Temp && View.Storages.ItemDiff(change, Details) == StorageActions.None)
                    add = false;

                // dont display if change is integrated
                if (Integrated.ContainsKey(id))
                    if (View.Storages.ItemDiff(change, Integrated[id]) == StorageActions.None)
                        add = false;

                if (add)
                    realChanges.Add(id, change);
            }

            return realChanges;
        }

        internal Dictionary<ulong, StorageItem> GetRealIntegrated()
        {
            Dictionary<ulong, StorageItem> realIntegrated = new Dictionary<ulong, StorageItem>();

            foreach (ulong id in Integrated.Keys)
            {
                bool add = true;
                StorageItem integrate = Integrated[id];

                // dont display if current file is equal to this change
                if (View.Storages.ItemDiff(integrate, Details) == StorageActions.None)
                    add = false;

                // dont display if this file isnt the lastest from the person id
                if (Changes.ContainsKey(id))
                    if (View.Storages.ItemDiff(integrate, Changes[id]) != StorageActions.None)
                        add = false;

                if (add)
                    realIntegrated.Add(id, integrate);
            }

            return realIntegrated;
        }

        public override string ToString()
        {
            return Details.Name.ToString();
        }
    }

    internal class RescanFile
    {
        internal string Path;
        internal FileItem Item;

        internal RescanFile(string path, FileItem item)
        {
            Path = path;
            Item = item;
        }
    }

    internal class RescanFolder
    {
        internal string Path;
        internal FolderNode Node;
        internal bool Recurse;

        internal RescanFolder(string path, FolderNode node, bool recurse)
        {
            Path = path;
            Node = node;
            Recurse = recurse;
        }
    }
}
