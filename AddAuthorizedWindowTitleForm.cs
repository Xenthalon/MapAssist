using MapAssist.Settings;
using System;
using System.Linq;
using System.Windows.Forms;

namespace MapAssist
{
    public partial class AddAuthorizedWindowTitleForm : Form
    {
        public string listToAddTo;
        public AddAuthorizedWindowTitleForm()
        {
            InitializeComponent();
        }
        
        private void AddAuthorizedWindowTitle(string authorizedWindowTitle)
        {

            var formParent = (ConfigEditor)Owner;
            var list = formParent.Controls.Find(listToAddTo, true).FirstOrDefault() as ListBox;
            if (!list.Items.Contains(authorizedWindowTitle))
            {
                list.Items.Add(authorizedWindowTitle);
                switch (listToAddTo)
                {
                    case "lstAuthorizedWindowTitle":
                        MapAssistConfiguration.Loaded.AuthorizedWindowTitles = MapAssistConfiguration.Loaded.AuthorizedWindowTitles.Append(authorizedWindowTitle).ToArray();
                        break;
                }
                Close();
            }
        }

        private void btnAddAuthorizedWindowTitle_Click(object sender, EventArgs e)
        {
            var authorizedWindowTitleToAdd = textBoxAutorizedWindowTitle.Text;
            AddAuthorizedWindowTitle(authorizedWindowTitleToAdd);
        }
    }
}
