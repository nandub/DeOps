/********************************************************************************

	De-Ops: Decentralized Operations
	Copyright (C) 2006 John Marshall Group, Inc.

	By contributing code you grant John Marshall Group an unlimited, non-exclusive
	license to your contribution.

	For support, questions, commercial use, etc...
	E-Mail: swabby@c0re.net

********************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;

using DeOps.Implementation;
using DeOps.Implementation.Dht;
using DeOps.Implementation.Transport;
using DeOps.Implementation.Protocol.Net;

using DeOps.Interface;
using DeOps.Interface.Tools;
using DeOps.Implementation.Protocol;


namespace DeOps.Simulator
{
    internal enum InstanceChangeType { Add, Remove, Update, Refresh};

    internal partial class SimForm : Form
    {
        internal InternetSim Sim;

        internal delegate void InstanceChangeHandler(SimInstance instance, InstanceChangeType type);
        internal InstanceChangeHandler InstanceChange;

        Random Rnd = new Random((int)DateTime.Now.Ticks);

        string[] OpNames = new string[20];
        RijndaelManaged[] OpKeys = new RijndaelManaged[20];

        private ListViewColumnSorter lvwColumnSorter = new ListViewColumnSorter();

        G2Protocol Protocol = new G2Protocol();

        internal Dictionary<ulong, NetView> NetViews = new Dictionary<ulong, NetView>();


        internal SimForm()
        {
            InitializeComponent();

            listInstances.ListViewItemSorter = lvwColumnSorter;

            //LoadNames();

            Sim = new InternetSim(this);
        }

        void LoadNames()
        {
            // operations
            OpNames[0] = "Firesoft";
            OpNames[1] = "Shacknews";
            OpNames[2] = "Engadget";
            OpNames[3] = "Gizmodo";
            OpNames[4] = "SomethingAwful";
            OpNames[5] = "Transbuddah";
            OpNames[6] = "Metafilter";
            OpNames[7] = "BoingBoing";
            OpNames[8] = "Yahoo";
            OpNames[9] = "Google";
            OpNames[10] = "Wikipedia";
            OpNames[11] = "Souceforge";
            OpNames[12] = "c0re";
            OpNames[13] = "MSDN";
            OpNames[14] = "Ebay";
            OpNames[15] = "Flickr";
            OpNames[16] = "Fark";
            OpNames[17] = "ScienceDaily";
            OpNames[18] = "Unicef";
            OpNames[19] = "Peta";

            for (int i = 0; i < 20; i++)
            {
                OpKeys[i] = new RijndaelManaged();
                OpKeys[i].GenerateKey();
            }


            // load last / male / female names
            List<string> LastNames = new List<string>();
            List<string> MaleNames = new List<string>();
            List<string> FemaleNames = new List<string>();

            ReadNames("..\\..\\names_last.txt", LastNames);
            ReadNames("..\\..\\names_male.txt", MaleNames);
            ReadNames("..\\..\\names_female.txt", FemaleNames);

            string rootpath = Application.StartupPath + "\\Firesoft\\";
            Directory.CreateDirectory(rootpath);

            // choose random name combos to create profiles for
            string name = "";

            for(int i = 0; i < 30; i++)
            {
                if( Rnd.Next(2) == 1)
                    name = MaleNames[Rnd.Next(MaleNames.Count)];
                else
                    name = FemaleNames[Rnd.Next(FemaleNames.Count)];

                name += " " + LastNames[Rnd.Next(LastNames.Count)];
                
                // create profile
                int index = Rnd.Next(19);

                string filename = OpNames[0] + " - " + name;
                string path = rootpath + filename + "\\" + filename + ".dop";
                Directory.CreateDirectory(rootpath + filename);

                Identity ident = new Identity(path, name, Protocol);


                ident.Settings.Operation = OpNames[0];
                ident.Settings.ScreenName = name;
                ident.Settings.KeyPair = new RSACryptoServiceProvider(1024);
                ident.Settings.OpKey = OpKeys[0];
                ident.Settings.OpAccess = AccessType.Public;

                ident.Save();
            }
        }

        private void ReadNames(string path, List<string> list)
        {
            StreamReader file = new StreamReader(path);
            
            string name = file.ReadLine();

            while (name != null)
            {
                name = name.Trim();
                name = name.ToLower();
                char[] letters = name.ToCharArray();
                letters[0] = char.ToUpper(letters[0]);
                list.Add(new string(letters));
                
                name = file.ReadLine();
            }
        }

        private void ControlForm_Load(object sender, EventArgs e)
        {
            InstanceChange += new InstanceChangeHandler(OnInstanceChange);
        }

        private void LoadMenuItem_Click(object sender, EventArgs e)
        {
            // choose folder to load from
            try
            {
                FolderBrowserDialog browse = new FolderBrowserDialog();

                browse.SelectedPath = Application.StartupPath;

                if (browse.ShowDialog(this) == DialogResult.OK)
                {
                    string[] dirs = Directory.GetDirectories(browse.SelectedPath);
                    foreach (string dir in dirs)
                    {
                        string[] paths = Directory.GetFiles(dir, "*.dop");

                        foreach (string path in paths)
                            Sim.AddInstance(path);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message);
            }
        }

        private void SaveMenuItem_Click(object sender, EventArgs e)
        {
            Sim.Pause();

            foreach (SimInstance instance in Sim.Instances)
                instance.Core.User.Save();

            MessageBox.Show(this, "Nodes Saved");
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }    

        private void buttonStart_Click(object sender, EventArgs e)
        {
            Sim.Start();
        }

        private void ButtonStep_Click(object sender, EventArgs e)
        {
            Sim.DoStep();
        }  

        private void buttonPause_Click(object sender, EventArgs e)
        {
            Sim.Pause();
        }

        void OnInstanceChange(SimInstance instance, InstanceChangeType type)
        {
            // add
            if (type == InstanceChangeType.Add)
            {
                listInstances.Items.Add(new ListInstanceItem(instance));
            }

            // refresh
            else if (type == InstanceChangeType.Refresh)
            {
                listInstances.Items.Clear();
                foreach(SimInstance inst in Sim.Instances)
                    listInstances.Items.Add(new ListInstanceItem(inst));
            }

            // update
            else if (type == InstanceChangeType.Update)
            {
                foreach (ListInstanceItem item in listInstances.Items)
                    if (item.Instance == instance)
                    {
                        item.Refresh();
                        break;
                    }
            }

            // remove
            else if (type == InstanceChangeType.Remove)
            {
                foreach (ListInstanceItem item in listInstances.Items)
                    if (item.Instance == instance)
                    {
                        listInstances.Items.Remove(item);
                        break;
                    }
            }
        }

        DateTime LastSimTime = new DateTime(2006, 1, 1, 0, 0, 0);

        private void SecondTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan total = Sim.TimeNow - Sim.StartTime;
            TimeSpan real = Sim.TimeNow - LastSimTime;

            LastSimTime = Sim.TimeNow;

            string label = total.ToString();

            labelTime.Text = SpantoString(total) + " / " + SpantoString(real);
        }

        string SpantoString(TimeSpan span)
        {
            string postfix = "";

            if (span.Milliseconds == 0)
                postfix += ".00";
            else
                postfix += "." + span.Milliseconds.ToString().Substring(0, 2);

            span = span.Subtract(new TimeSpan(0, 0, 0, 0, span.Milliseconds));

            return span.ToString() + postfix;

        }

        private void ControlForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Sim.Shutdown = true;
        }

        private void listInstances_MouseClick(object sender, MouseEventArgs e)
        {
            // main
            // internal
            // global
                // crawler
                // graph
                // packets
                // search
            // operation
            // console
            // ---
            // Disconnect

            if (e.Button != MouseButtons.Right)
                return;

            ListInstanceItem item = listInstances.GetItemAt(e.X, e.Y) as ListInstanceItem;

            if (item == null)
                return;

            ContextMenu menu = new ContextMenu();

            if(item.Instance.Core == null)
                menu.MenuItems.Add(new MenuItem("Connect", new EventHandler(Click_Connect)));
            else
            {
                MenuItem global = new MenuItem("Global");
                global.MenuItems.Add(new MenuItem("Crawler", new EventHandler(Click_GlobalCrawler)));
                global.MenuItems.Add(new MenuItem("Graph", new EventHandler(Click_GlobalGraph)));
                global.MenuItems.Add(new MenuItem("Packets", new EventHandler(Click_GlobalPackets)));
                global.MenuItems.Add(new MenuItem("Search", new EventHandler(Click_GlobalSearch)));

                MenuItem operation = new MenuItem("Operation");
                operation.MenuItems.Add(new MenuItem("Crawler", new EventHandler(Click_OpCrawler)));
                operation.MenuItems.Add(new MenuItem("Graph", new EventHandler(Click_OpGraph)));
                operation.MenuItems.Add(new MenuItem("Packets", new EventHandler(Click_OpPackets)));
                operation.MenuItems.Add(new MenuItem("Search", new EventHandler(Click_OpSearch)));

                menu.MenuItems.Add(new MenuItem("Main", new EventHandler(Click_Main)));
                menu.MenuItems.Add(new MenuItem("Internal", new EventHandler(Click_Internal)));
                menu.MenuItems.Add(global);
                menu.MenuItems.Add(operation);
                menu.MenuItems.Add(new MenuItem("Console", new EventHandler(Click_Console)));
                menu.MenuItems.Add(new MenuItem("-"));
                menu.MenuItems.Add(new MenuItem("Disconnect", new EventHandler(Click_Disconnect)));
            }

            menu.Show(listInstances, e.Location);
        }

        private void Click_Main(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                OpCore core = item.Instance.Core;

                if (core.GuiMain == null)
                    core.GuiMain = new MainForm(core);

                core.GuiMain.Show();
                core.GuiMain.Activate();
            }
        }

        private void Click_Console(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                OpCore core = item.Instance.Core;

                if (core.GuiConsole == null)
                    core.GuiConsole = new ConsoleForm(core);

                core.GuiConsole.Show();
                core.GuiConsole.Activate();
            }
        }

        private void Click_Internal(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                OpCore core = item.Instance.Core;

                if (core.GuiInternal == null)
                    core.GuiInternal = new InternalsForm(core);

                core.GuiInternal.Show();
                core.GuiInternal.Activate();
            }
        }

        private void Click_GlobalCrawler(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                OpCore core = item.Instance.Core;

                DhtNetwork network = core.GlobalNet;

                if (network.GuiCrawler == null)
                    network.GuiCrawler = new CrawlerForm("Global", network);

                network.GuiCrawler.Show();
                network.GuiCrawler.Activate();
            }
        }

        private void Click_GlobalGraph(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                OpCore core = item.Instance.Core;

                DhtNetwork network = core.GlobalNet;

                if (network.GuiGraph == null)
                    network.GuiGraph = new GraphForm("Global", network);

                network.GuiGraph.Show();
                network.GuiGraph.Activate();
            }
        }

        private void Click_GlobalPackets(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                OpCore core = item.Instance.Core;

                DhtNetwork network = core.GlobalNet;

                if (network.GuiPackets == null)
                    network.GuiPackets = new PacketsForm("Global", network);

                network.GuiPackets.Show();
                network.GuiPackets.Activate();
            }
        }

        private void Click_GlobalSearch(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                SearchForm form = new SearchForm("Global", this, item.Instance.Core.GlobalNet);

                form.Show();
            }
        }

        private void Click_OpCrawler(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                OpCore core = item.Instance.Core;

                DhtNetwork network = core.OperationNet;

                if (network.GuiCrawler == null)
                    network.GuiCrawler = new CrawlerForm("Operation", network);

                network.GuiCrawler.Show();
                network.GuiCrawler.Activate();
            }
        }

        private void Click_OpGraph(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                OpCore core = item.Instance.Core;

                DhtNetwork network = core.OperationNet;

                if (network.GuiGraph == null)
                    network.GuiGraph = new GraphForm("Operation", network);

                network.GuiGraph.Show();
                network.GuiGraph.Activate();
            }
        }

        private void Click_OpPackets(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                OpCore core = item.Instance.Core;

                DhtNetwork network = core.OperationNet;

                if (network.GuiPackets == null)
                    network.GuiPackets = new PacketsForm("Operation", network);

                network.GuiPackets.Show();
                network.GuiPackets.Activate();
            }
        }

        private void Click_OpSearch(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
            {
                SearchForm form = new SearchForm("Operation", this, item.Instance.Core.OperationNet);
                form.Show();
            }
        }

        private void Click_Connect(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
                if(item.Instance.Core == null)
                    Sim.BringOnline(item.Instance);
        }

        private void Click_Disconnect(object sender, EventArgs e)
        {
            foreach (ListInstanceItem item in listInstances.SelectedItems)
                if(item.Instance.Core != null)
                    Sim.BringOffline(item.Instance);
        }

        private void listInstances_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvwColumnSorter.ColumnToSort)
            {
                // Reverse the current sort direction for this column.
                if (lvwColumnSorter.OrderOfSort == SortOrder.Ascending)
                    lvwColumnSorter.OrderOfSort = SortOrder.Descending;
                else
                    lvwColumnSorter.OrderOfSort = SortOrder.Ascending;
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvwColumnSorter.ColumnToSort = e.Column;
                lvwColumnSorter.OrderOfSort = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            listInstances.Sort();
        }

        private void LinkUpdate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            lock(Sim.Instances)
                foreach (SimInstance instance in Sim.Instances)
                    OnInstanceChange(instance, InstanceChangeType.Update);

            LabelInstances.Text = Sim.Instances.Count.ToString() + " Instances";
        }

        private void ViewMenu_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;

            if (item == null || Sim == null) 
                return;

            item.DropDownItems.Clear();
            item.DropDownItems.Add(new ViewMenuItem("Global", 0, new EventHandler(ViewMenu_OnClick)));

            foreach(ulong id in Sim.OpNames.Keys)
                item.DropDownItems.Add(new ViewMenuItem(Sim.OpNames[id], id, new EventHandler(ViewMenu_OnClick)));
        }

        private void ViewMenu_OnClick(object sender, EventArgs e)
        {
            ViewMenuItem item = sender as ViewMenuItem;

            if (item == null || Sim == null)
                return;

            NetView view = null;

            if (!NetViews.ContainsKey(item.OpID))
            {
                view = new NetView(this, item.OpID);
                NetViews[item.OpID] = view;
            }
            else
                view = NetViews[item.OpID];

            view.Show();
            view.BringToFront();
        }
    }

    internal class ViewMenuItem : ToolStripMenuItem
    {
        internal ulong OpID;

        internal ViewMenuItem(string name, ulong id, EventHandler onClick)
            : base(name, null, onClick)
        {
            OpID = id;
        }

    }

    internal class ListInstanceItem : ListViewItem
    {
        internal SimInstance Instance;

        internal ListInstanceItem(SimInstance instance)
        {
            Instance = instance;

            Refresh();
        }

        internal void Refresh()
        {
            int SUBITEM_COUNT = 9;

            while (SubItems.Count < SUBITEM_COUNT)
                SubItems.Add("");

            if (Instance.Core == null)
            {
                Text = Path.GetFileNameWithoutExtension(Instance.Path);
                ForeColor = Color.Gray;

                SubItems[1].Text = ""; SubItems[2].Text = ""; SubItems[3].Text = ""; SubItems[4].Text = "";
                SubItems[5].Text = ""; SubItems[6].Text = ""; SubItems[7].Text = ""; 

                return;
            }


            Text = Instance.Core.User.Settings.ScreenName;
            ForeColor = Color.Black;
            
            // 0 user
            // 1 op
            // 2 Dht id
            // 3 client id
            // 4 firewall
            // 5 alerts
            // 6 proxies
            // 7 Notes
            // 8 bandwidth

            // alerts...
            string alerts = "";
            
            // firewall incorrect
            if (Instance.RealFirewall != Instance.Core.Firewall)
                alerts += "Firewall, ";

            // ip incorrect
            if (!Instance.RealIP.Equals(Instance.Core.LocalIP))
                alerts += "IP, ";

            // routing unresponsive global/op
            if(Instance.Core.GlobalNet != null)
                if(!Instance.Core.GlobalNet.Routing.Responsive())
                    alerts += "Global Routing, ";

            if (!Instance.Core.OperationNet.Routing.Responsive())
                alerts += "Op Routing, ";

            // not proxied global/op
            if (Instance.RealFirewall != FirewallType.Open)
            {
                if (Instance.Core.GlobalNet != null)
                    if (Instance.Core.GlobalNet.TcpControl.ConnectionMap.Count == 0)
                        alerts += "Global Proxy, ";

                if (Instance.Core.OperationNet.TcpControl.ConnectionMap.Count == 0)
                    alerts += "Op Proxy, ";
            }

            // locations
            if (Instance.Core.Locations.LocationMap.Count <= 1)
                alerts += "Locs, ";

            string localip = (Instance.Core.LocalIP == null) ? "null" : Instance.Core.LocalIP.ToString();

            SubItems[1].Text = Instance.Core.User.Settings.Operation;
            SubItems[2].Text = Utilities.IDtoBin(Instance.Core.LocalDhtID);
            SubItems[3].Text = Instance.Core.ClientID.ToString(); 
            SubItems[4].Text = Instance.RealFirewall.ToString();
            SubItems[5].Text = alerts;

            if(Instance.Core.GlobalNet != null)
            {
                SubItems[6].Text = ProxySummary(Instance.Core.GlobalNet.TcpControl);
                SubItems[7].Text = NotesSummary(Instance.Core);
            }

            SubItems[8].Text = Instance.BytesRecvd.ToString() + " in / " + Instance.BytesSent.ToString() + " out";
        }

        private string ProxySummary(TcpHandler control)
        {
            StringBuilder summary = new StringBuilder();

            lock(control.Connections)
                foreach(TcpConnect connect in control.Connections)
                    if(connect.State == TcpState.Connected)
                        if (connect.Proxy == ProxyType.ClientBlocked || connect.Proxy == ProxyType.ClientNAT)
                            if(Instance.Internet.UserNames.ContainsKey(connect.DhtID))
                                summary.Append(Instance.Internet.UserNames[connect.DhtID] + ", ");

            return summary.ToString();
        }

        private string NotesSummary(OpCore core)
        {
            //crit change store to other column at end total searches and transfers to detect backlogs


            StringBuilder summary = new StringBuilder();

            summary.Append(core.Links.LinkMap.Count.ToString() + " links, ");

            summary.Append(core.Locations.LocationMap.Count.ToString() + " locs, ");

            summary.Append(core.OperationNet.Searches.Pending.Count.ToString() + " searches, ");

            summary.Append(core.Transfers.Pending.Count.ToString() + " transfers");

            //foreach (ulong key in store.Index.Keys)
            //    foreach (StoreData data in store.Index[key])
            //        if (data.Kind == DataKind.Profile)
             //           summary.Append(((ProfileData)data.Packet).Name + ", ");

            //foreach (ulong key in store.Index.Keys)
            //    if(Instance.Internet.OpNames.ContainsKey(key))
            //        summary.Append(Instance.Internet.OpNames[key] + " " + store.Index[key].Count + ", ");

            return summary.ToString();
        }
    }
}
