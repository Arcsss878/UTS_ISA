namespace UTS_ISA
{
    partial class FormChat
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lstRiwayat   = new System.Windows.Forms.ListBox();
            this.txtChat      = new System.Windows.Forms.TextBox();
            this.lstUser      = new System.Windows.Forms.ListBox();
            this.btnKirim     = new System.Windows.Forms.Button();
            this.btnExport    = new System.Windows.Forms.Button();
            this.lblUsers     = new System.Windows.Forms.Label();
            this.lblRole      = new System.Windows.Forms.Label();
            this.SuspendLayout();

            // lstRiwayat — riwayat chat
            this.lstRiwayat.FormattingEnabled = true;
            this.lstRiwayat.ItemHeight = 16;
            this.lstRiwayat.Location = new System.Drawing.Point(12, 35);
            this.lstRiwayat.Name = "lstRiwayat";
            this.lstRiwayat.Size = new System.Drawing.Size(430, 320);
            this.lstRiwayat.TabIndex = 0;

            // txtChat — input pesan
            this.txtChat.Location = new System.Drawing.Point(12, 368);
            this.txtChat.Name = "txtChat";
            this.txtChat.Size = new System.Drawing.Size(330, 22);
            this.txtChat.TabIndex = 1;

            // btnKirim
            this.btnKirim.Location = new System.Drawing.Point(352, 366);
            this.btnKirim.Name = "btnKirim";
            this.btnKirim.Size = new System.Drawing.Size(90, 26);
            this.btnKirim.Text = "Kirim";
            this.btnKirim.UseVisualStyleBackColor = true;
            this.btnKirim.Click += new System.EventHandler(this.btnKirim_Click);

            // btnExport — export history
            this.btnExport.Location = new System.Drawing.Point(12, 403);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(130, 26);
            this.btnExport.Text = "Export Riwayat (.txt)";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);

            // lstUser — daftar user online
            this.lstUser.FormattingEnabled = true;
            this.lstUser.ItemHeight = 16;
            this.lstUser.Location = new System.Drawing.Point(460, 35);
            this.lstUser.Name = "lstUser";
            this.lstUser.Size = new System.Drawing.Size(130, 320);
            this.lstUser.TabIndex = 2;

            // lblUsers
            this.lblUsers.AutoSize = true;
            this.lblUsers.Location = new System.Drawing.Point(460, 12);
            this.lblUsers.Text = "User Online :";

            // lblRole
            this.lblRole.AutoSize = true;
            this.lblRole.Location = new System.Drawing.Point(12, 12);
            this.lblRole.Name = "lblRole";
            this.lblRole.Text = "";

            // FormChat
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(605, 445);
            this.Controls.Add(this.lblRole);
            this.Controls.Add(this.lblUsers);
            this.Controls.Add(this.lstUser);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.btnKirim);
            this.Controls.Add(this.txtChat);
            this.Controls.Add(this.lstRiwayat);
            this.Name = "FormChat";
            this.Text = "Secure Chat";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormChat_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ListBox lstRiwayat;
        private System.Windows.Forms.TextBox txtChat;
        private System.Windows.Forms.ListBox lstUser;
        private System.Windows.Forms.Button btnKirim;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Label lblUsers;
        private System.Windows.Forms.Label lblRole;
    }
}
