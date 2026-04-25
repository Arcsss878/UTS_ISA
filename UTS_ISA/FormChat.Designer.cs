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
            this.lstRiwayat      = new System.Windows.Forms.ListBox();
            this.txtChat         = new System.Windows.Forms.TextBox();
            this.lstUser         = new System.Windows.Forms.ListBox();
            this.btnKirim        = new System.Windows.Forms.Button();
            this.btnExport       = new System.Windows.Forms.Button();
            this.lblUsers        = new System.Windows.Forms.Label();
            this.lblRole         = new System.Windows.Forms.Label();
            this.lblChatWith     = new System.Windows.Forms.Label();
            this.lblTokenInfo    = new System.Windows.Forms.Label();
            this.btnRefreshToken = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // ── lblRole — nama user + role (pojok kiri atas) ───────────────────
            this.lblRole.AutoSize = true;
            this.lblRole.Location = new System.Drawing.Point(12, 10);
            this.lblRole.Name     = "lblRole";
            this.lblRole.Text     = "";
            this.lblRole.Font     = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold);

            // ── lblTokenInfo — countdown token session ─────────────────────────
            this.lblTokenInfo.AutoSize  = false;
            this.lblTokenInfo.Location  = new System.Drawing.Point(12, 30);
            this.lblTokenInfo.Size      = new System.Drawing.Size(555, 18); // diperlebar agar "Sisa: xx:xx" tidak terpotong
            this.lblTokenInfo.Name      = "lblTokenInfo";
            this.lblTokenInfo.Text      = "Session: -";
            this.lblTokenInfo.Font      = new System.Drawing.Font("Consolas", 8.5f);
            this.lblTokenInfo.ForeColor = System.Drawing.Color.DarkGreen;

            // ── btnRefreshToken — minta token baru ke server ───────────────────
            this.btnRefreshToken.Location = new System.Drawing.Point(572, 26);
            this.btnRefreshToken.Name     = "btnRefreshToken";
            this.btnRefreshToken.Size     = new System.Drawing.Size(88, 22);
            this.btnRefreshToken.Text     = "Refresh Token";
            this.btnRefreshToken.Font     = new System.Drawing.Font("Segoe UI", 7.5f);
            this.btnRefreshToken.UseVisualStyleBackColor = true;
            this.btnRefreshToken.Click   += new System.EventHandler(this.btnRefreshToken_Click);

            // ── lblChatWith — status chat dengan siapa ─────────────────────────
            this.lblChatWith.AutoSize  = false;
            this.lblChatWith.Location  = new System.Drawing.Point(12, 54);
            this.lblChatWith.Size      = new System.Drawing.Size(480, 20);
            this.lblChatWith.Name      = "lblChatWith";
            this.lblChatWith.Text      = "Pilih user untuk mulai chat";
            this.lblChatWith.Font      = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold);

            // ── lstRiwayat — riwayat chat ──────────────────────────────────────
            this.lstRiwayat.FormattingEnabled = true;
            this.lstRiwayat.ItemHeight        = 16;
            this.lstRiwayat.Location          = new System.Drawing.Point(12, 77);
            this.lstRiwayat.Name              = "lstRiwayat";
            this.lstRiwayat.Size              = new System.Drawing.Size(480, 278);
            this.lstRiwayat.TabIndex          = 0;

            // ── txtChat — input pesan ──────────────────────────────────────────
            this.txtChat.Location = new System.Drawing.Point(12, 368);
            this.txtChat.Name     = "txtChat";
            this.txtChat.Size     = new System.Drawing.Size(380, 22);
            this.txtChat.TabIndex = 1;

            // ── btnKirim ───────────────────────────────────────────────────────
            this.btnKirim.Location = new System.Drawing.Point(402, 366);
            this.btnKirim.Name     = "btnKirim";
            this.btnKirim.Size     = new System.Drawing.Size(90, 26);
            this.btnKirim.Text     = "Kirim";
            this.btnKirim.UseVisualStyleBackColor = true;
            this.btnKirim.Click   += new System.EventHandler(this.btnKirim_Click);

            // ── btnExport ──────────────────────────────────────────────────────
            this.btnExport.Location = new System.Drawing.Point(12, 403);
            this.btnExport.Name     = "btnExport";
            this.btnExport.Size     = new System.Drawing.Size(140, 26);
            this.btnExport.Text     = "Export Riwayat (.txt)";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click   += new System.EventHandler(this.btnExport_Click);

            // ── lstUser — daftar semua user + status online/offline ────────────
            this.lstUser.FormattingEnabled = true;
            this.lstUser.ItemHeight        = 16;
            this.lstUser.Location          = new System.Drawing.Point(505, 77);
            this.lstUser.Name              = "lstUser";
            this.lstUser.Size              = new System.Drawing.Size(140, 278);
            this.lstUser.TabIndex          = 2;
            this.lstUser.SelectedIndexChanged += new System.EventHandler(this.lstUser_SelectedIndexChanged);

            // ── lblUsers — judul panel user ────────────────────────────────────
            this.lblUsers.AutoSize = true;
            this.lblUsers.Location = new System.Drawing.Point(505, 54);
            this.lblUsers.Name     = "lblUsers";
            this.lblUsers.Text     = "User List:";
            this.lblUsers.Font     = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold);

            // ── FormChat ───────────────────────────────────────────────────────
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize          = new System.Drawing.Size(675, 445);
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
            this.Name         = "FormChat";
            this.Text         = "Secure Chat";
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
