using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.IO;
using System.Text;
using System.Windows.Forms;

using RiseOp.Implementation;
using RiseOp.Interface;
using RiseOp.Services.Trust;


namespace RiseOp.Services.Mail
{
    internal partial class ComposeMail : ViewShell
    {
        MailService Mail;
        OpCore Core;

        ulong DefaultID;
        bool MessageSent;
        
        internal string CustomTitle;
        internal int ThreadID;

        List<ulong> ToIDs = new List<ulong>();


        internal ComposeMail(MailService mail, ulong id)
        {
            InitializeComponent();

            Mail = mail;
            Core = mail.Core;
            DefaultID = id;

            if (id != 0)
            {
                ToTextBox.Text = Core.GetName(id);
                ToIDs.Add(id);
            }
        }

        private void ComposeMail_Load(object sender, EventArgs e)
        {
            MessageBody.InputBox.Select();
        }

        internal override string GetTitle(bool small)
        {
            if (small)
                return "Compose";

            if(CustomTitle != null)
                return CustomTitle + Core.GetName(DefaultID);

            if (DefaultID != 0)
                return "Mail " + Core.GetName(DefaultID);

            return "Compose Mail";
        }

        internal override Size GetDefaultSize()
        {
            return new Size(450, 525);
        }

        internal override Icon GetIcon()
        {
            return MailRes.Compose;
        }

        internal override bool Fin()
        {
            if (!MessageSent && MessageBody.InputBox.Text.Length > 0)
                if (MessageBox.Show(this, "Discard Message?", "New Mail", MessageBoxButtons.YesNo) == DialogResult.No)
                    return false;

            return true;
        }

        private void LinkAdd_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Multiselect = true;
            open.Title = "Add Files to Mail";
            open.Filter = "All files (*.*)|*.*";

            if (open.ShowDialog() == DialogResult.OK)
                foreach (string path in open.FileNames)
                {
                    bool added = false;
                    foreach (AttachedFile attached in ListFiles.Items)
                        if (attached.FilePath == path)
                            added = true;

                    if (!added)
                        ListFiles.Items.Add(new AttachedFile(path));
                }

            if (ListFiles.SelectedItem == null && ListFiles.Items.Count > 0)
                ListFiles.SelectedItem = ListFiles.Items[0];
        }

        private void LinkRemove_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {

            if (ListFiles.SelectedItem != null)
            {
                int index = ListFiles.Items.IndexOf(ListFiles.SelectedItem);

                ListFiles.Items.Remove(ListFiles.SelectedItem);

                if (ListFiles.Items.Count > 0)
                {
                    if (index < ListFiles.Items.Count)
                        ListFiles.SelectedItem = ListFiles.Items[index];
                    else if (index - 1 < ListFiles.Items.Count)
                        ListFiles.SelectedItem = ListFiles.Items[index - 1];
                }
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            try
            {
                if(ToIDs.Count == 0)
                    throw new Exception("Letter not addressed to anyone");

                // files
                List<AttachedFile> files = new List<AttachedFile>();
                foreach (AttachedFile file in ListFiles.Items)
                    files.Add(file);

                // subject
                if (SubjectTextBox.Text.Length == 0)
                    throw new Exception("Subject is blank");

                // body
                if (MessageBody.InputBox.Text.Length == 0)
                    throw new Exception("Message body is blank");

                Mail.SendMail(ToIDs, files, SubjectTextBox.Text, MessageBody.InputBox.Rtf, ThreadID);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            MessageSent = true;

            if (External != null)
                External.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (External != null)
                External.Close();
        }


        private void BrowseTo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            AddLinks add = new AddLinks(Core.Trust, 0);

            string prefix = ToTextBox.Text.Length > 0 ? ", " : "";

            if (add.ShowDialog(this) == DialogResult.OK)
            {
                foreach (ulong id in add.People)
                    if (!ToIDs.Contains(id))
                        ToIDs.Add(id);

                UpdateToText();
            }
        }

        private void RemovePersonLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            RemoveLinks form = new RemoveLinks(Core, ToIDs);

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                foreach (ulong id in form.RemoveIDs)
                    if (ToIDs.Contains(id))
                        ToIDs.Remove(id) ;

                UpdateToText();
            }
        }

        void UpdateToText()
        {
            string text = "";

            foreach (ulong id in ToIDs)
                text += Core.GetName(id) + ", ";

            ToTextBox.Text = text.TrimEnd(',', ' ');
        }

        private void BrowseCC_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {

        }
    }
}
