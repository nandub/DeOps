/********************************************************************************

	De-Ops: Decentralized Operations
	Copyright (C) 2006 John Marshall Group, Inc.

	By contributing code you grant John Marshall Group an unlimited, non-exclusive
	license to your contribution.

	For support, questions, commercial use, etc...
	E-Mail: swabby@c0re.net

********************************************************************************/

using System;
using System.Security.Cryptography;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Net.Sockets;
using System.Reflection;
using System.Windows.Forms;

using DeOps.Components;
using DeOps.Implementation;
using DeOps.Implementation.Dht;
using DeOps.Implementation.Protocol;
using DeOps.Implementation.Protocol.Comm;
using DeOps.Implementation.Protocol.Net;


namespace DeOps.Interface.Tools
{
	/// <summary>
	/// Summary description for PacketsForm.
	/// </summary>
	internal class PacketsForm : System.Windows.Forms.Form
	{
        DhtNetwork Network;
		G2Protocol Protocol;
		
		SHA1CryptoServiceProvider sha = new SHA1CryptoServiceProvider();

		internal delegate void UpdateLogHandler(PacketLogEntry logEntry);
		internal UpdateLogHandler UpdateLog;

		private System.Windows.Forms.Splitter splitter1;
		private System.Windows.Forms.ListView ListViewPackets;
        private System.Windows.Forms.ColumnHeader columnHeaderProtocol;
		private System.Windows.Forms.ColumnHeader columnHeaderType;
		private System.Windows.Forms.ColumnHeader columnHeaderAddress;
		private System.Windows.Forms.ColumnHeader columnHeaderSize;
		private System.Windows.Forms.TreeView TreeViewPacket;
		private System.Windows.Forms.ColumnHeader columnHeaderHash;
		private System.Windows.Forms.MainMenu mainMenu1;
		private System.Windows.Forms.MenuItem menuItemCommands;
        private System.Windows.Forms.MenuItem menuItemPause;
        private MenuItem MenuItemTcp;
        private MenuItem MenuItemUdp;
        private MenuItem MenuItemRudp;
        private ColumnHeader columnHeaderTime;
        private IContainer components;

        internal PacketsForm(string name, DhtNetwork network)
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			Network = network;

			Protocol = new G2Protocol();

			UpdateLog = new UpdateLogHandler(AsyncUpdateLog);

            Text = name + " Packets (" + Network.Core.User.Settings.ScreenName + ")";

            RefreshView();
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PacketsForm));
            this.TreeViewPacket = new System.Windows.Forms.TreeView();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.ListViewPackets = new System.Windows.Forms.ListView();
            this.columnHeaderTime = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderProtocol = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderAddress = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderType = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderSize = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderHash = new System.Windows.Forms.ColumnHeader();
            this.mainMenu1 = new System.Windows.Forms.MainMenu(this.components);
            this.menuItemCommands = new System.Windows.Forms.MenuItem();
            this.menuItemPause = new System.Windows.Forms.MenuItem();
            this.MenuItemTcp = new System.Windows.Forms.MenuItem();
            this.MenuItemUdp = new System.Windows.Forms.MenuItem();
            this.MenuItemRudp = new System.Windows.Forms.MenuItem();
            this.SuspendLayout();
            // 
            // TreeViewPacket
            // 
            this.TreeViewPacket.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.TreeViewPacket.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.TreeViewPacket.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TreeViewPacket.Location = new System.Drawing.Point(0, 214);
            this.TreeViewPacket.Name = "TreeViewPacket";
            this.TreeViewPacket.Size = new System.Drawing.Size(568, 112);
            this.TreeViewPacket.TabIndex = 0;
            this.TreeViewPacket.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TreeViewPacket_KeyDown);
            // 
            // splitter1
            // 
            this.splitter1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.splitter1.Location = new System.Drawing.Point(0, 208);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(568, 6);
            this.splitter1.TabIndex = 1;
            this.splitter1.TabStop = false;
            // 
            // ListViewPackets
            // 
            this.ListViewPackets.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.ListViewPackets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderTime,
            this.columnHeaderProtocol,
            this.columnHeaderAddress,
            this.columnHeaderType,
            this.columnHeaderSize,
            this.columnHeaderHash});
            this.ListViewPackets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ListViewPackets.FullRowSelect = true;
            this.ListViewPackets.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.ListViewPackets.Location = new System.Drawing.Point(0, 0);
            this.ListViewPackets.MultiSelect = false;
            this.ListViewPackets.Name = "ListViewPackets";
            this.ListViewPackets.Size = new System.Drawing.Size(568, 208);
            this.ListViewPackets.TabIndex = 2;
            this.ListViewPackets.UseCompatibleStateImageBehavior = false;
            this.ListViewPackets.View = System.Windows.Forms.View.Details;
            this.ListViewPackets.Click += new System.EventHandler(this.ListViewPackets_Click);
            // 
            // columnHeaderTime
            // 
            this.columnHeaderTime.Text = "Time";
            // 
            // columnHeaderProtocol
            // 
            this.columnHeaderProtocol.Text = "Protocol";
            this.columnHeaderProtocol.Width = 92;
            // 
            // columnHeaderAddress
            // 
            this.columnHeaderAddress.Text = "Address";
            this.columnHeaderAddress.Width = 99;
            // 
            // columnHeaderType
            // 
            this.columnHeaderType.Text = "Type";
            this.columnHeaderType.Width = 78;
            // 
            // columnHeaderSize
            // 
            this.columnHeaderSize.Text = "Size";
            this.columnHeaderSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnHeaderSize.Width = 90;
            // 
            // columnHeaderHash
            // 
            this.columnHeaderHash.Text = "Hash";
            // 
            // mainMenu1
            // 
            this.mainMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItemCommands});
            // 
            // menuItemCommands
            // 
            this.menuItemCommands.Index = 0;
            this.menuItemCommands.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItemPause,
            this.MenuItemTcp,
            this.MenuItemUdp,
            this.MenuItemRudp});
            this.menuItemCommands.Text = "Commands";
            // 
            // menuItemPause
            // 
            this.menuItemPause.Index = 0;
            this.menuItemPause.Text = "Pause";
            this.menuItemPause.Click += new System.EventHandler(this.menuItemPause_Click);
            // 
            // MenuItemTcp
            // 
            this.MenuItemTcp.Checked = true;
            this.MenuItemTcp.Index = 1;
            this.MenuItemTcp.Text = "Tcp";
            this.MenuItemTcp.Click += new System.EventHandler(this.MenuItemTcp_Click);
            // 
            // MenuItemUdp
            // 
            this.MenuItemUdp.Checked = true;
            this.MenuItemUdp.Index = 2;
            this.MenuItemUdp.Text = "Udp";
            this.MenuItemUdp.Click += new System.EventHandler(this.MenuItemUdp_Click);
            // 
            // MenuItemRudp
            // 
            this.MenuItemRudp.Checked = true;
            this.MenuItemRudp.Index = 3;
            this.MenuItemRudp.Text = "Rudp";
            this.MenuItemRudp.Click += new System.EventHandler(this.MenuItemRudp_Click);
            // 
            // PacketsForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(568, 326);
            this.Controls.Add(this.ListViewPackets);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.TreeViewPacket);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Menu = this.mainMenu1;
            this.Name = "PacketsForm";
            this.Text = "Packets";
            this.Closing += new System.ComponentModel.CancelEventHandler(this.PacketsForm_Closing);
            this.Load += new System.EventHandler(this.PacketsForm_Load);
            this.ResumeLayout(false);

		}
		#endregion

		private void PacketsForm_Load(object sender, System.EventArgs e)
		{
			
		}

		private void PacketsForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
            Network.GuiPackets = null;
		}

		internal void AsyncUpdateLog(PacketLogEntry logEntry)
		{
			if(menuItemPause.Checked)
				return;

            AddListItem(logEntry);

            if (!ListViewPackets.Focused && ListViewPackets.Items.Count > 0)
				ListViewPackets.EnsureVisible(ListViewPackets.Items.Count - 1);
		}

        private void AddListItem(PacketLogEntry logEntry)
        {
            if ((logEntry.Protocol == TransportProtocol.Tcp && MenuItemTcp.Checked) ||
                (logEntry.Protocol == TransportProtocol.Udp && MenuItemUdp.Checked) ||
                (logEntry.Protocol == TransportProtocol.Rudp && MenuItemRudp.Checked))
                ListViewPackets.Items.Add(PackettoItem(logEntry));
            
        }

		internal ListViewItem PackettoItem(PacketLogEntry logEntry)
		{
			// hash, protocol, direction, address, type, size
			string hash      = Utilities.BytestoHex(sha.ComputeHash( logEntry.Data), 0, 2, false);
            string protocol = "?";

            switch (logEntry.Protocol)
            {
                case TransportProtocol.Tcp:
                    protocol = "tcp";
                    break;
                case TransportProtocol.Udp:
                    protocol = "udp";
                    break;
                case TransportProtocol.Rudp:
                    protocol = "rudp";
                    break;
            }

            // Network - Search / Search Req / Store ... - Component

            // Comm - Data / Ack / Syn

            // Rudp - Type - Component

            string name      = "?";

			G2Header root = new G2Header(logEntry.Data);
			
            if (Protocol.ReadPacket(root))
            {
                if(logEntry.Protocol == TransportProtocol.Rudp)
                {
                    name = TransportProtocol.Rudp.ToString() + " - ";

                    name += GetVariableName(typeof(CommPacket), root.Name);

                    if (root.Name == CommPacket.Data)
                    {
                        CommData data = CommData.Decode(Protocol, root);

                        name += " - " + GetVariableName(typeof(ComponentID), data.Component);
                    }

                }
                else
                {
                    name = GetVariableName(typeof(RootPacket), root.Name) + " - ";

                    if (root.Name == RootPacket.Comm)
                    {
                        RudpPacket commPacket = RudpPacket.Decode(Protocol, root);

                        name += GetVariableName(typeof(RudpPacketType), commPacket.PacketType);
                    }

                    if (root.Name == RootPacket.Network)
                    {
                        NetworkPacket netPacket = NetworkPacket.Decode(Protocol, root);

                        G2Header internalRoot = new G2Header(netPacket.InternalData);
                        if (Protocol.ReadPacket(internalRoot))
                        {
                            name += GetVariableName(typeof(NetworkPacket), internalRoot.Name);

                            ushort id = 0;
                            G2ReceivedPacket wrap = new G2ReceivedPacket();
                            wrap.Root = internalRoot;

                            // search request / search acks / stores have component types
                            if (internalRoot.Name == NetworkPacket.SearchRequest)
                            {
                                SearchReq req = SearchReq.Decode(Protocol, wrap);
                                id = req.Component;
                            }

                            if (internalRoot.Name == NetworkPacket.SearchAck)
                            {
                                SearchAck ack = SearchAck.Decode(Protocol, wrap);
                                id = ack.Component;
                            }

                            if (internalRoot.Name == NetworkPacket.StoreRequest)
                            {
                                StoreReq store = StoreReq.Decode(Protocol, wrap);
                                id = store.Component;
                            }

                            if(id != 0)
                                name += " - " + GetVariableName(typeof(ComponentID), id);
                        }
                    }
                }
            }

            string time = Network.Core.TimeNow.ToString("HH:mm:ss:ff");

            return new PacketListViewItem(logEntry, new string[] { time, protocol, logEntry.Address.ToString(), name, logEntry.Data.Length.ToString(), hash }, logEntry.Direction == DirectionType.In );
		}

        private string GetVariableName(Type packet, byte val)
        {

            FieldInfo[] fields = packet.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (FieldInfo info in fields)
                if(info.IsLiteral && val == (byte) info.GetValue(null))
                    return info.Name;

            return "unknown";
        }


        private string GetVariableName(Type packet, ushort val)
        {

            FieldInfo[] fields = packet.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (FieldInfo info in fields)
                if (info.IsLiteral && val == (ushort)info.GetValue(null))
                    return info.Name;

            return "unknown";
        }
        

		private void ListViewPackets_Click(object sender, System.EventArgs e)
		{
			if(ListViewPackets.SelectedItems.Count == 0)
				return;

			PacketListViewItem selected = (PacketListViewItem) ListViewPackets.SelectedItems[0];

			// build tree
			TreeViewPacket.Nodes.Clear();

            G2Header root = new G2Header(selected.LogEntry.Data);

            if (Protocol.ReadPacket(root))
            {
                if (selected.LogEntry.Protocol == TransportProtocol.Rudp)
                {
                    string name = root.Name.ToString() + " : " + GetVariableName(typeof(CommPacket), root.Name);
                    TreeNode rootNode = TreeViewPacket.Nodes.Add(name);

                    if (Protocol.ReadPayload(root))
                        rootNode.Nodes.Add(Utilities.BytestoHex(root.Data, root.PayloadPos, root.PayloadSize, true));

                    Protocol.ResetPacket(root);

                    // get type
                    Type packetType = null;
                    switch (root.Name)
                    {
                        case CommPacket.SessionRequest:
                            packetType = typeof(SessionRequest);
                            break;
                        case CommPacket.SessionAck:
                            packetType = typeof(SessionAck);
                            break;
                        case CommPacket.KeyRequest:
                            packetType = typeof(KeyRequest);
                            break;
                        case CommPacket.KeyAck:
                            packetType = typeof(KeyAck);
                            break;
                        case CommPacket.Data:
                            packetType = typeof(CommData);
                            break;
                        case CommPacket.Close:
                            packetType = typeof(CommClose);
                            break;
                    }

                    ReadChildren(root, rootNode, packetType);
                }
                else
                {
                    string name = root.Name.ToString() + " : " + GetVariableName(typeof(RootPacket), root.Name);
                    TreeNode rootNode = TreeViewPacket.Nodes.Add(name);

                    byte[] payload = null;
                    TreeNode payloadNode = null;
                    if (Protocol.ReadPayload(root))
                    {
                        payloadNode = new TreeNode(Utilities.BytestoHex(root.Data, root.PayloadPos, root.PayloadSize, true));
                        rootNode.Nodes.Add(payloadNode);
                        payload = Utilities.ExtractBytes(root.Data, root.PayloadPos, root.PayloadSize);
                    }
                    Protocol.ResetPacket(root);

                    if (root.Name == RootPacket.Comm)
                    {
                        ReadChildren(root, rootNode, typeof(RudpPacket));
                    }

                    if (root.Name == RootPacket.Network)
                    {
                        ReadChildren(root, rootNode, typeof(NetworkPacket));

                        G2Header payloadRoot = new G2Header(payload);
                        if (payload != null && Protocol.ReadPacket(payloadRoot))
                        {
                            name = payloadRoot.Name.ToString() + " : " + GetVariableName(typeof(NetworkPacket), payloadRoot.Name);
                            TreeNode netNode = payloadNode.Nodes.Add(name);

                            if (Protocol.ReadPayload(payloadRoot))
                                netNode.Nodes.Add(Utilities.BytestoHex(payloadRoot.Data, payloadRoot.PayloadPos, payloadRoot.PayloadSize, true));
                            Protocol.ResetPacket(payloadRoot);

                            Type packetType = null;
                            switch (payloadRoot.Name)
                            {
                                case NetworkPacket.SearchRequest:
                                    packetType = typeof(SearchReq);
                                    break;
                                case NetworkPacket.SearchAck:
                                    packetType = typeof(SearchAck);
                                    break;
                                case NetworkPacket.StoreRequest:
                                    packetType = typeof(StoreReq);
                                    break;
                                case NetworkPacket.Ping:
                                    packetType = typeof(Ping);
                                    break;
                                case NetworkPacket.Pong:
                                    packetType = typeof(Pong);
                                    break;
                                case NetworkPacket.Bye:
                                    packetType = typeof(Bye);
                                    break;
                                case NetworkPacket.ProxyRequest:
                                    packetType = typeof(ProxyReq);
                                    break;
                                case NetworkPacket.ProxyAck:
                                    packetType = typeof(ProxyAck);
                                    break;
                                case NetworkPacket.CrawlRequest:
                                    packetType = typeof(CrawlReq);
                                    break;
                                case NetworkPacket.CrawlAck:
                                    packetType = typeof(CrawlAck);
                                    break;

                            }


                            ReadChildren(payloadRoot, netNode, packetType);
                        }
                    }
                }
            }



			/*G2Header rootPacket = new G2Header(selected.LogEntry.Data);
			
			int start = 0;
			int size  = selected.LogEntry.Data.Length;
			if(G2ReadResult.PACKET_GOOD == Protocol.ReadNextPacket(rootPacket, ref start, ref size) )
			{
				TreeNode rootNode = TreeViewPacket.Nodes.Add( TrimName(rootPacket.Name.ToString()) );

				if(Protocol.ReadPayload(rootPacket))
				{
					//rootNode.Nodes.Add( Utilities.BytestoAscii(rootPacket.Data, rootPacket.PayloadPos, rootPacket.PayloadSize));
					rootNode.Nodes.Add( Utilities.BytestoHex(rootPacket.Data, rootPacket.PayloadPos, rootPacket.PayloadSize, true));
				}

				Protocol.ResetPacket(rootPacket);

				ReadChildren(rootPacket, rootNode, null);
			}*/

			TreeViewPacket.ExpandAll();
		}

		internal void ReadChildren(G2Header rootPacket, TreeNode rootNode, Type packetType)
		{
			G2Header child = new G2Header(rootPacket.Data);
	
			while( Protocol.ReadNextChild(rootPacket, child) == G2ReadResult.PACKET_GOOD )
			{
                string name = child.Name.ToString();

                if (packetType != null)
                    name += " : " + GetVariableName(packetType, child.Name);

				TreeNode childNode = rootNode.Nodes.Add(name);

				if(Protocol.ReadPayload(child))
				{
					//childNode.Nodes.Add( "Payload Ascii: " + Utilities.BytestoAscii(childPacket.Data, childPacket.PayloadPos, childPacket.PayloadSize));
					childNode.Nodes.Add(Utilities.BytestoHex(child.Data, child.PayloadPos, child.PayloadSize, true));
				}

				Protocol.ResetPacket(child);

				ReadChildren(child, childNode, null);
			}
		}

		string TrimName(string name)
		{
			int pos = name.LastIndexOf("/");

			return name.Substring(pos + 1);
		}

		private void TreeViewPacket_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
		{
			if(e.Control && e.KeyCode == Keys.C)
				Clipboard.SetDataObject(TreeViewPacket.SelectedNode.Text, true);
		}

		private void menuItemPause_Click(object sender, System.EventArgs e)
		{
			if( menuItemPause.Checked )
			{
                RefreshView();

				menuItemPause.Checked = false;
			}

			else
				menuItemPause.Checked = true;
	}

        private void MenuItemUdp_Click(object sender, EventArgs e)
        {
            if( MenuItemUdp.Checked )
                MenuItemUdp.Checked = false;
            else
                MenuItemUdp.Checked = true;

            RefreshView();
        }

        private void MenuItemTcp_Click(object sender, EventArgs e)
        {
            if (MenuItemTcp.Checked)
                MenuItemTcp.Checked = false;

            else
                MenuItemTcp.Checked = true;

            RefreshView();
        }

        private void MenuItemRudp_Click(object sender, EventArgs e)
        {
            if (MenuItemRudp.Checked)
                MenuItemRudp.Checked = false;

            else
                MenuItemRudp.Checked = true;

            RefreshView();
        }

        void RefreshView()
        {
            // un-pause
            ListViewPackets.Items.Clear();

            lock (Network.LoggedPackets)
                foreach (PacketLogEntry logEntry in Network.LoggedPackets)
                    AddListItem(logEntry);
        }

	}

	
	internal class PacketListViewItem : ListViewItem
	{
		internal PacketLogEntry LogEntry;

		internal PacketListViewItem(PacketLogEntry logEntry, string[] subItems, bool incoming) : base(subItems)
		{
			LogEntry = logEntry;

            ForeColor = incoming ? Color.DarkBlue : Color.DarkRed;
		}
	}
}