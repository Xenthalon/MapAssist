
namespace MapAssist
{
    partial class AddAuthorizedWindowTitleForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddAuthorizedWindowTitleForm));
            this.textBoxAutorizedWindowTitle = new System.Windows.Forms.TextBox();
            this.btnAddAuthorizedWindowTitle = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textBoxAutorizedWindowTitle
            // 
            this.textBoxAutorizedWindowTitle.Location = new System.Drawing.Point(12, 14);
            this.textBoxAutorizedWindowTitle.Name = "lstAreas";
            this.textBoxAutorizedWindowTitle.Size = new System.Drawing.Size(287, 173);
            this.textBoxAutorizedWindowTitle.TabIndex = 0;
            // 
            // btnAddAuthorizedWindowTitle
            // 
            this.btnAddAuthorizedWindowTitle.Location = new System.Drawing.Point(12, 193);
            this.btnAddAuthorizedWindowTitle.Name = "btnAuthorizedWindowTitle";
            this.btnAddAuthorizedWindowTitle.Size = new System.Drawing.Size(287, 23);
            this.btnAddAuthorizedWindowTitle.TabIndex = 1;
            this.btnAddAuthorizedWindowTitle.Text = "Add Authorized Window Title";
            this.btnAddAuthorizedWindowTitle.UseVisualStyleBackColor = true;
            this.btnAddAuthorizedWindowTitle.Click += new System.EventHandler(this.btnAddAuthorizedWindowTitle_Click);
            // 
            // AddAuthorizedWindowTitleForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(150, 100);
            this.Controls.Add(this.btnAddAuthorizedWindowTitle);
            this.Controls.Add(this.textBoxAutorizedWindowTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AddAuthorizedWindowTitleForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Authorized Window Title - MapAssist";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox textBoxAutorizedWindowTitle;
        private System.Windows.Forms.Button btnAddAuthorizedWindowTitle;
    }
}
