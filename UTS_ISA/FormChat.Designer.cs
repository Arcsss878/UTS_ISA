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
            this.lstRiwayat = new System.Windows.Forms.ListBox();
            this.txtChat = new System.Windows.Forms.TextBox();
            this.lstUser = new System.Windows.Forms.ListBox();
            this.btnKirim = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.lblUsers = new System.Windows.Forms.Label();
            this.lblRole = new System.Windows.Forms.Label();
            this.lblChatWith = new System.Windows.Forms.Label();
            this.lblTokenInfo = new System.Windows.Forms.Label();
            this.btnRefreshToken = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lstRiwayat
            // 
            this.lstRiwayat.BackColor = System.Drawing.Color.Black;
            this.lstRiwayat.Font = new System.Drawing.Font("Cascadia Code", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstRiwayat.ForeColor = System.Drawing.SystemColors.Info;
            this.lstRiwayat.FormattingEnabled = true;
            this.lstRiwayat.ItemHeight = 21;
            this.lstRiwayat.Location = new System.Drawing.Point(14, 143);
            this.lstRiwayat.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.lstRiwayat.Name = "lstRiwayat";
            this.lstRiwayat.Size = new System.Drawing.Size(624, 424);
            this.lstRiwayat.TabIndex = 0;
            // 
            // txtChat
            // 
            this.txtChat.BackColor = System.Drawing.Color.Silver;
            this.txtChat.Font = new System.Drawing.Font("Cascadia Code", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtChat.Location = new System.Drawing.Point(14, 592);
            this.txtChat.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtChat.Name = "txtChat";
            this.txtChat.Size = new System.Drawing.Size(540, 26);
            this.txtChat.TabIndex = 1;
            // 
            // lstUser
            // 
            this.lstUser.BackColor = System.Drawing.Color.Black;
            this.lstUser.Font = new System.Drawing.Font("Cascadia Code", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstUser.ForeColor = System.Drawing.SystemColors.Info;
            this.lstUser.FormattingEnabled = true;
            this.lstUser.ItemHeight = 21;
            this.lstUser.Location = new System.Drawing.Point(664, 143);
            this.lstUser.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.lstUser.Name = "lstUser";
            this.lstUser.Size = new System.Drawing.Size(157, 424);
            this.lstUser.TabIndex = 2;
            this.lstUser.SelectedIndexChanged += new System.EventHandler(this.lstUser_SelectedIndexChanged);
            // 
            // btnKirim
            // 
            this.btnKirim.BackColor = System.Drawing.Color.Transparent;
            this.btnKirim.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnKirim.Font = new System.Drawing.Font("Cascadia Code", 12F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnKirim.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.btnKirim.Location = new System.Drawing.Point(573, 575);
            this.btnKirim.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnKirim.Name = "btnKirim";
            this.btnKirim.Size = new System.Drawing.Size(102, 54);
            this.btnKirim.TabIndex = 4;
            this.btnKirim.Text = "Kirim";
            this.btnKirim.UseVisualStyleBackColor = false;
            this.btnKirim.Click += new System.EventHandler(this.btnKirim_Click);
            // 
            // btnExport
            // 
            this.btnExport.BackColor = System.Drawing.Color.Transparent;
            this.btnExport.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.btnExport.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnExport.Font = new System.Drawing.Font("Cascadia Code", 12F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnExport.ForeColor = System.Drawing.Color.Yellow;
            this.btnExport.Location = new System.Drawing.Point(19, 641);
            this.btnExport.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(317, 44);
            this.btnExport.TabIndex = 5;
            this.btnExport.Text = "Export Riwayat (.txt)";
            this.btnExport.UseVisualStyleBackColor = false;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // lblUsers
            // 
            this.lblUsers.AutoSize = true;
            this.lblUsers.BackColor = System.Drawing.Color.Transparent;
            this.lblUsers.Font = new System.Drawing.Font("Cascadia Code", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblUsers.ForeColor = System.Drawing.Color.Lime;
            this.lblUsers.Location = new System.Drawing.Point(658, 107);
            this.lblUsers.Name = "lblUsers";
            this.lblUsers.Size = new System.Drawing.Size(154, 32);
            this.lblUsers.TabIndex = 6;
            this.lblUsers.Text = "User List:";
            // 
            // lblRole
            // 
            this.lblRole.AutoSize = true;
            this.lblRole.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblRole.Location = new System.Drawing.Point(14, 13);
            this.lblRole.Name = "lblRole";
            this.lblRole.Size = new System.Drawing.Size(0, 25);
            this.lblRole.TabIndex = 0;
            // 
            // lblChatWith
            // 
            this.lblChatWith.BackColor = System.Drawing.Color.Transparent;
            this.lblChatWith.Font = new System.Drawing.Font("Cascadia Code", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblChatWith.ForeColor = System.Drawing.Color.Lime;
            this.lblChatWith.Location = new System.Drawing.Point(14, 91);
            this.lblChatWith.Name = "lblChatWith";
            this.lblChatWith.Size = new System.Drawing.Size(540, 48);
            this.lblChatWith.TabIndex = 3;
            this.lblChatWith.Text = "Pilih user untuk mulai chat";
            // 
            // lblTokenInfo
            // 
            this.lblTokenInfo.BackColor = System.Drawing.Color.Silver;
            this.lblTokenInfo.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTokenInfo.ForeColor = System.Drawing.Color.Black;
            this.lblTokenInfo.Location = new System.Drawing.Point(14, 40);
            this.lblTokenInfo.Name = "lblTokenInfo";
            this.lblTokenInfo.Size = new System.Drawing.Size(624, 23);
            this.lblTokenInfo.TabIndex = 1;
            this.lblTokenInfo.Text = "Session: -";
            // 
            // btnRefreshToken
            // 
            this.btnRefreshToken.BackColor = System.Drawing.Color.Transparent;
            this.btnRefreshToken.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnRefreshToken.Font = new System.Drawing.Font("Cascadia Code", 12F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRefreshToken.ForeColor = System.Drawing.Color.Yellow;
            this.btnRefreshToken.Location = new System.Drawing.Point(664, 26);
            this.btnRefreshToken.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnRefreshToken.Name = "btnRefreshToken";
            this.btnRefreshToken.Size = new System.Drawing.Size(139, 42);
            this.btnRefreshToken.TabIndex = 2;
            this.btnRefreshToken.Text = "Refresh Token";
            this.btnRefreshToken.UseVisualStyleBackColor = false;
            this.btnRefreshToken.Click += new System.EventHandler(this.btnRefreshToken_Click);
            // 
            // FormChat
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.BackgroundImage = global::UTS_ISA.Properties.Resources.background;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.ClientSize = new System.Drawing.Size(850, 698);
            this.Controls.Add(this.lblRole);
            this.Controls.Add(this.lblTokenInfo);
            this.Controls.Add(this.btnRefreshToken);
            this.Controls.Add(this.lblChatWith);
            this.Controls.Add(this.lstRiwayat);
            this.Controls.Add(this.txtChat);
            this.Controls.Add(this.btnKirim);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.lblUsers);
            this.Controls.Add(this.lstUser);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Cascadia Code", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "FormChat";
            this.Text = "Secure Chat";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormChat_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox  lstRiwayat;
        private System.Windows.Forms.TextBox  txtChat;
        private System.Windows.Forms.ListBox  lstUser;
        private System.Windows.Forms.Button   btnKirim;
        private System.Windows.Forms.Button   btnExport;
        private System.Windows.Forms.Label    lblUsers;
        private System.Windows.Forms.Label    lblRole;
        private System.Windows.Forms.Label    lblChatWith;
        private System.Windows.Forms.Label    lblTokenInfo;
        private System.Windows.Forms.Button   btnRefreshToken;
    }
}
