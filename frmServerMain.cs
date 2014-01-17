using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProgramDeployerServer
{
    public partial class frmServerMain : Form
    {
        public frmServerMain()
        {
            InitializeComponent();
        }

        private void frmServerMain_Load(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void btnRunScript_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            new Deployer(ofd.FileName).Run();
        }

    }
}
