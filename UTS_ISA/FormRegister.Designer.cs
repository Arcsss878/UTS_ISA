namespace UTS_ISA
{
    partial class FormRegister
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
            this.lblUser     = new System.Windows.Forms.Label();
            this.lblPass     = new System.Windows.Forms.Label();
            this.lblConfirm  = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.txtConfirm  = new System.Windows.Forms.TextBox();
            this.btnRegister = new System.Windows.Forms.Button();
            this.btnBack     = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // lblUser
            this.lblUser.AutoSize = true;
            this.lblUser.Location = new System.Drawing.Point(30, 25);
            this.lblUser.Text = "Username :";

            // lblPass
            this.lblPass.AutoSize = true;
            this.lblPass.Location = new System.Drawing.Point(30, 65);
            this.lblPass.Text = "Password :";

            // lblConfirm
            this.lblConfirm.AutoSize = true;
            this.lblConfirm.Location = new System.Drawing.Point(30, 105);
            this.lblConfirm.Text = "Konfirmasi :";

            // txtUsername
            this.txtUsername.Location = new System.Drawing.Point(130, 22);
            this.txtUsername.Size = new System.Drawing.Size(200, 22);

            // txtPassword
            this.txtPassword.Location = new System.Drawing.Point(130, 62);
            this.txtPassword.Size = new System.Drawing.Size(200, 22);
            this.txtPassword.PasswordChar = '*';

            // txtConfirm
            this.txtConfirm.Location = new System.Drawing.Point(130, 102);
            this.txtConfirm.Size = new System.Drawing.Size(200, 22);
            this.txtConfirm.PasswordChar = '*';

            // btnRegister
            this.btnRegister.Location = new System.Drawing.Point(80, 150);
            this.btnRegister.Size = new System.Drawing.Size(100, 28);
            this.btnRegister.Text = "Register";
            this.btnRegister.UseVisualStyleBackColor = true;
            this.btnRegister.Click += new System.EventHandler(this.btnRegister_Click);

            // btnBack
            this.btnBack.Location = new System.Drawing.Point(195, 150);
            this.btnBack.Size = new System.Drawing.Size(80, 28);
            this.btnBack.Text = "Kembali";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);

            // FormRegister
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(370, 200);
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.btnRegister);
            this.Controls.Add(this.txtConfirm);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.lblConfirm);
            this.Controls.Add(this.lblPass);
            this.Controls.Add(this.lblUser);
            this.Name = "FormRegister";
            this.Text = "Secure Chat — Register";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblUser;
        private System.Windows.Forms.Label lblPass;
        private System.Windows.Forms.Label lblConfirm;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.TextBox txtConfirm;
        private System.Windows.Forms.Button btnRegister;
        private System.Windows.Forms.Button btnBack;
    }
}
