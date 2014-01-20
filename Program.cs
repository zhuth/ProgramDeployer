using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProgramDeployerServer
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && System.IO.File.Exists(args[1]))
            {
                new Deployer(args[1]).Run();
                return;
            }
            else
            {
                var dp = new Deployer();
                do
                {
                    var cmd = Console.ReadLine();
                    if (cmd == "quit") return;
                    dp.Run(cmd);
                } while (true);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmServerMain());
        }
    }
}
