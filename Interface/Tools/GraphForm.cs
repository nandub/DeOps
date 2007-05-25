/********************************************************************************

	De-Ops: Decentralized Operations
	Copyright (C) 2006 John Marshall Group, Inc.

	By contributing code you grant John Marshall Group an unlimited, non-exclusive
	license to your contribution.

	For support, questions, commercial use, etc...
	E-Mail: swabby@c0re.net

********************************************************************************/

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

using DeOps.Implementation;
using DeOps.Implementation.Dht;
using DeOps.Implementation.Transport;

namespace DeOps.Interface.Tools
{
	/// <summary>
	/// Summary description for GraphForm.
	/// </summary>
	internal class GraphForm : System.Windows.Forms.Form
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

        OpCore Core;
        DhtNetwork Network;
        DhtRouting Routing;

		Bitmap DisplayBuffer;

		bool Redraw;

		internal delegate void UpdateGraphHandler();
		internal UpdateGraphHandler UpdateGraph;


        internal GraphForm(string name, DhtNetwork network)
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

            Network = network;
            Core = Network.Core;
            Routing = Network.Routing;

			UpdateGraph = new UpdateGraphHandler(AsyncUpdateGraph);
			
			Text = name + " Graph (" + Core.User.Settings.ScreenName + ")";

			Redraw = true;
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GraphForm));
            this.SuspendLayout();
            // 
            // GraphForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(292, 266);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "GraphForm";
            this.Text = "Graph";
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.GraphForm_Paint);
            this.Resize += new System.EventHandler(this.GraphForm_Resize);
            this.Closing += new System.ComponentModel.CancelEventHandler(this.GraphForm_Closing);
            this.ResumeLayout(false);

		}
		#endregion

		private void GraphForm_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
		{
			if(ClientSize.Width == 0 || ClientSize.Height == 0)
				return;
			
			if(DisplayBuffer == null )
				DisplayBuffer = new Bitmap(ClientSize.Width, ClientSize.Height);

			if( !Redraw )
			{
				e.Graphics.DrawImage(DisplayBuffer, 0, 0);
				return;
			}
			Redraw = false;

			// background
			Graphics buffer = Graphics.FromImage(DisplayBuffer);
			
			buffer.Clear(Color.DarkBlue);
			buffer.SmoothingMode = SmoothingMode.AntiAlias;

			// back black ellipse
			Point centerPoint = new Point(ClientSize.Width / 2, ClientSize.Height / 2);

			int maxRadius = (ClientSize.Height > ClientSize.Width) ? ClientSize.Width / 2 : ClientSize.Height / 2;
			maxRadius -= 5;

			buffer.FillEllipse( new SolidBrush(Color.Black), GetBoundingBox(centerPoint, maxRadius));

			uint localID = IDto32(Core.LocalDhtID);
			
			ArrayList contactPoints = new ArrayList();

			// draw circles
			int i = 0;
			float sweepAngle = 360;
			Pen orangePen = new Pen(Color.Orange, 2);
			
			lock(Routing.BucketList)
				foreach(DhtBucket bucket in Routing.BucketList)
				{
					// draw lines
                    if (!bucket.Last)
                    {
                        int rad = maxRadius * i / Routing.BucketList.Count;

                        uint lowpos = localID >> (32 - i);
                        lowpos = lowpos << (32 - i);
                        uint highpos = lowpos | ((uint)1 << 31 - i);

                        float startAngle = 360 * ((float)lowpos / (float)uint.MaxValue);

                        if (rad > 0)
                        {
                            buffer.DrawArc(orangePen, GetBoundingBox(centerPoint, rad), startAngle, sweepAngle);

                            buffer.DrawLine(orangePen, GetCircumPoint(centerPoint, rad, lowpos), GetCircumPoint(centerPoint, maxRadius, lowpos));
                            buffer.DrawLine(orangePen, GetCircumPoint(centerPoint, rad, highpos), GetCircumPoint(centerPoint, maxRadius, highpos));
                        }
                        else
                            buffer.DrawLine(orangePen, GetCircumPoint(centerPoint, maxRadius, 0), GetCircumPoint(centerPoint, maxRadius, uint.MaxValue / 2));

                        // draw text
                        lowpos = localID >> (31 - i);
                        highpos = (lowpos + 1) << (31 - i);
                        lowpos = lowpos << (31 - i);


                        Point textPoint = GetCircumPoint(centerPoint, rad + 10, lowpos + (highpos - lowpos) / 2);

                        if ((localID & ((uint)1 << (31 - i))) > 0)
                            buffer.DrawString("1", new Font(FontFamily.GenericSansSerif, 8, FontStyle.Bold), new SolidBrush(Color.White), textPoint);
                        else
                            buffer.DrawString("0", new Font(FontFamily.GenericSansSerif, 8, FontStyle.Bold), new SolidBrush(Color.White), textPoint);
                    }

					foreach(DhtContact contact in bucket.ContactList)
						contactPoints.Add(GetBoundingBox(GetCircumPoint(centerPoint, maxRadius, IDto32(contact.DhtID)), 4));

					sweepAngle /= 2;
					i++;
				}

			// draw contacts
			foreach(Rectangle contactRect in contactPoints)
				buffer.FillEllipse(new SolidBrush(Color.White), contactRect);

			// draw proxies
			lock(Network.TcpControl.Connections)
                foreach (TcpConnect connection in Network.TcpControl.Connections)
				{
					if(connection.Proxy == ProxyType.Server)
						buffer.FillEllipse(new SolidBrush(Color.Green), GetBoundingBox(GetCircumPoint(centerPoint, maxRadius, IDto32(connection.DhtID)), 4));
					
					if(connection.Proxy == ProxyType.ClientNAT || connection.Proxy == ProxyType.ClientBlocked)
						buffer.FillEllipse(new SolidBrush(Color.Red), GetBoundingBox(GetCircumPoint(centerPoint, maxRadius, IDto32(connection.DhtID)), 4));
				}

			// draw self
			buffer.FillEllipse(new SolidBrush(Color.Yellow), GetBoundingBox(GetCircumPoint(centerPoint, maxRadius, localID), 4));

		
			// Copy buffer to display
			e.Graphics.DrawImage(DisplayBuffer, 0, 0);
		}

		private void GraphForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Network.GuiGraph = null;
		}

		private void GraphForm_Resize(object sender, System.EventArgs e)
		{
			if(ClientSize.Width > 0 && ClientSize.Height > 0)
			{
				DisplayBuffer = new Bitmap(ClientSize.Width, ClientSize.Height);
				Redraw = true;
				Invalidate();
			}
		}

		internal void AsyncUpdateGraph()
		{
			Redraw = true;
			Invalidate();
		}

		Rectangle GetBoundingBox(Point center, int rad)
		{
			return new Rectangle(center.X - rad, center.Y - rad, rad * 2, rad * 2);
		}

		Point GetCircumPoint(Point center, int rad, uint position)
		{
			double fraction = (double) position / (double) uint.MaxValue;

			int xPos = (int) ((double) rad  * Math.Cos( fraction * 2*Math.PI)) + center.X; 
			int yPos = (int) ((double) rad  * Math.Sin( fraction * 2*Math.PI)) + center.Y; 

			return new Point(xPos, yPos);
		}

		uint IDto32(UInt64 id)
		{
			uint retVal = 0;

			for(int i = 0; i < 32; i++)
				if( Utilities.GetBit(id, i) == 1)
					retVal |= ((uint) 1) << (31 - i);

			return retVal;
		}
	}
}