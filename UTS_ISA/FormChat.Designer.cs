namespace UTS_ISA
{
    partial class FormChat
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lstRiwayat = new System.Windows.Forms.ListBox();
            this.btnKirim = new System.Windows.Forms.Button();
            this.txtChat = new System.Windows.Forms.TextBox();
            this.lstUser = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lstRiwayat
            // 
            this.lstRiwayat.FormattingEnabled = true;
            this.lstRiwayat.ItemHeight = 16;
            this.lstRiwayat.Location = new System.Drawing.Point(23, 12);
            this.lstRiwayat.Name = "lstRiwayat";
            this.lstRiwayat.Size = new System.Drawing.Size(398, 340);
            this.lstRiwayat.TabIndex = 8;
            // 
            // btnKirim
            // 
            this.btnKirim.Location = new System.Drawing.Point(451, 377);
            this.btnKirim.Name = "btnKirim";
            this.btnKirim.Size = new System.Drawing.Size(106, 23);
            this.btnKirim.TabIndex = 7;
            this.btnKirim.Text = "Kirim";
            this.btnKirim.UseVisualStyleBackColor = true;
            this.btnKirim.Click += new System.EventHandler(this.btnKirim_Click);
            // 
            // txtChat
            // 
            this.txtChat.Location = new System.Drawing.Point(23, 377);
            this.txtChat.Name = "txtChat";
            this.txtChat.Size = new System.Drawing.Size(398, 22);
            this.txtChat.TabIndex = 6;
            // 
            // lstUser
            // 
            this.lstUser.FormattingEnabled = true;
            this.lstUser.ItemHeight = 16;
            this.lstUser.Location = new System.Drawing.Point(451, 40);
            this.lstUser.Name = "lstUser";
            this.lstUser.Size = new System.Drawing.Size(120, 308);
            this.lstUser.TabIndex = 9;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(451, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 16);
            this.label1.TabIndex = 10;
            this.label1.Text = "list user :";
            // 
            // FormChat
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(590, 416);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lstUser);
            this.Controls.Add(this.lstRiwayat);
            this.Controls.Add(this.btnKirim);
            this.Controls.Add(this.txtChat);
            this.Name = "FormChat";
            this.Text = "FormChat";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox lstRiwayat;
        private System.Windows.Forms.Button btnKirim;
        private System.Windows.Forms.TextBox txtChat;
        private System.Windows.Forms.ListBox lstUser;
        private System.Windows.Forms.Label label1;
    }
}