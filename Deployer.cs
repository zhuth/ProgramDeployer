using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProgramDeployerServer
{
    public class Deployer
    {
        // 好吧我们来说明一下这个部署脚本怎么写
        // 每一行的格式是 [command] [args]
        // 这些是合法的 command：
        //      client [url1] [url2] ...    给定客户端列表，这些客户端地址将用来和后面 map 指令的 [remote] 组合成客户端的完整 URL
        //      map [remote] [local]        将本地文件（夹） [local] 上传到远程路径 [remote] 上
        //      proc [action] [name]        kill 或 start 一个进程
        // # 开始的是注释

        public static void Run(string scriptfile)
        {
            List<string> clients = new List<string>();

            foreach (string ln in File.ReadAllLines(scriptfile))
            {
                string line = noannoations(ln).Trim();
                string[] args = Regex.Split(line, @"\s+");

                switch (args[0].ToLower())
                {
                    case "client":
                        clients.Clear();
                        for (int i = 1; i < args.Length; ++i)
                            clients.Add(args[i] +(args[i].EndsWith("/")  ? "" :"/"));
                        break;
                    case "wait":
                        int delay;
                        if (args.Length > 1 && int.TryParse(args[1], out delay))
                            System.Threading.Thread.Sleep(delay);
                        break;
                    case "map":
                        if (args.Length < 3) continue;
                        if (!File.Exists(args[2]) && !Directory.Exists(args[2])) continue;
                        // 判断是文件还是目录
                        bool isFile = File.Exists(args[2]);
                        if (isFile) // 是文件，直接比较并上传
                        {
                            PushFile(clients, args[1], args[2]);
                        }
                        else // 不是文件，要展开各个子目录，逐个比较
                        {
                            PushDirectory(clients, args[1], args[2]);
                        }
                        break;
                    case "proc":
                        if (args.Length < 3) continue;
                        foreach (string client in clients)
                        {
                            try
                            {
                                Console.WriteLine("- proc:" + args[1] + " " + args[2] + " @" + client);
                                new WebClient().DownloadString(client + "proc?action=" + args[1] + "&name=" + args[2]);
                            }
                            catch { }
                        }
                        break;
                }
            }
        }

        public static void PushFile(IEnumerable<string> clients, string remotePath, string localFilePath)
        {
            Console.WriteLine("- " + localFilePath);
            string localMD5 = ProgramDeployerClient.FileMD5.HashFile(localFilePath, "md5");
            foreach (string client in clients)
            {
                int trials = 0;
                bool doPush = false;
                try
                {
                    string md5 = new WebClient().DownloadString(client + remotePath + "?md5").Trim();
                    if (localMD5 != md5) doPush = true;
                }
                catch (WebException)
                {
                    doPush = true;
                }
                if (!doPush) return;
                Console.WriteLine("    >> " + client + remotePath);
            Retry:
                try
                {
                    ProgramDeployerClient.Httpd.PushFile(client + remotePath, localFilePath);
                }
                catch(Exception ex)
                {
                    if (trials++ < 10) goto Retry;
                    else Console.WriteLine("[Error] " + ex.Message);
                }
            }
        }

        public static void PushDirectory(IEnumerable<string> clients, string remoteDirPath, string localDirPath)
        {
            if (!remoteDirPath.EndsWith("/")) remoteDirPath += "/";
            foreach (string f in Directory.GetFileSystemEntries(localDirPath, "*", SearchOption.AllDirectories))
            {
                string remotePath = f.Substring(localDirPath.Length + 1).Replace('\\', '/');
                PushFile(clients, remoteDirPath + remotePath, f);
            }
        }

        private static string noannoations(string str)
        {
            int sharp = str.IndexOf('#');
            if (sharp < 0) return str;
            return str.Substring(0, sharp);
        }
    }
}
