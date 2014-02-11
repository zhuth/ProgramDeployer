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
        public string FileName { get; set; }
        
        private List<string> _clients = new List<string>();

        public Deployer(string filename)
        {
            FileName = filename;
        }

        public Deployer() { }

        // 好吧我们来说明一下这个部署脚本怎么写
        // 每一行的格式是 [command] [args]
        // 这些是合法的 command：
        //      client [url1] [url2] ...    给定客户端列表，这些客户端地址将用来和后面 map 指令的 [remote] 组合成客户端的完整 URL
        //      map [remote] [local]        将本地文件（夹） [local] 上传到远程路径 [remote] 上
        //      proc [action] [name]        kill 或 start 一个进程
        // # 开始的是注释
        public void Run()
        {
            foreach (string ln in File.ReadAllLines(FileName))
            {
                Run(ln);
            }
        }

        public void Run(string line)
        {
            ProgramDeployerClient.PropertiesParser props = new ProgramDeployerClient.PropertiesParser("");

            line = noannoations(line).Trim();
            string[] args = split(line);

            switch (args[0].ToLower())
            {
                case "client":
                    _clients.Clear();
                    for (int i = 1; i < args.Length; ++i)
                    {
                        string remoteUrl = args[i] + (args[i].EndsWith("/") ? "" : "/");
                        try
                        {
                            new WebClient().DownloadString(remoteUrl + "ping");
                            _clients.Add(remoteUrl);
                        }
                        catch {
                            Console.WriteLine("Unable to visit " + remoteUrl + ", skipped.");
                        }
                    }
                    break;
                case "wait":
                    int delay;
                    if (args.Length > 1 && int.TryParse(args[1], out delay))
                        System.Threading.Thread.Sleep(delay);
                    break;
                case "map":
                    if (args.Length < 3) return;
                    if (!File.Exists(args[2]) && !Directory.Exists(args[2])) return;
                    // 判断是文件还是目录
                    bool isFile = File.Exists(args[2]);
                    if (isFile) // 是文件，直接比较并上传
                    {
                        PushFile(args[1], args[2]);
                    }
                    else // 不是文件，要展开各个子目录，逐个比较
                    {
                        PushDirectory(args[1], args[2], args.Length > 3 ? args[3] : null);
                    }
                    break;
                case "proc":
                    if (args.Length < 3) return;
                    foreach (string client in _clients)
                    {
                        try
                        {
                            Console.WriteLine("- proc:" + args[1] + " " + args[2] + " @" + client);
                            Console.WriteLine(ProgramDeployerClient.Httpd.DownloadString(client + "/proc?action=" + args[1] + "&name=" + args[2], Properties.Settings.Default.HashKey));
                        }
                        catch { }
                    }
                    break;
                case "download":
                    if (args.Length < 2) return;
                    string path = ProgramDeployerClient.Httpd.GetSignedUrl(args[1], Properties.Settings.Default.HashKey);
                    System.Diagnostics.Process.Start(path);
                    break;
            }
        }

        public void PushFile(string remotePath, string localFilePath)
        {
            if (!File.Exists(localFilePath)) return;

            Console.WriteLine("- " + localFilePath);
            string localMD5 = ProgramDeployerClient.MD5Crypt.HashFile(localFilePath, "md5");
            foreach (string client in _clients)
            {
                int trials = 0;
                bool doPush = false;
                try
                {
                    string md5 = ProgramDeployerClient.Httpd.DownloadString(client + remotePath + "?md5", Properties.Settings.Default.HashKey).Trim();
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
                    ProgramDeployerClient.Httpd.PushFile(client + remotePath, localFilePath, Properties.Settings.Default.HashKey);
                }
                catch(Exception ex)
                {
                    if (trials++ < 10) goto Retry;
                    else Console.WriteLine("[Error] " + ex.Message);
                }
            }
        }

        public void PushDirectory(string remoteDirPath, string localDirPath, string pattern)
        {
            if (!remoteDirPath.EndsWith("/")) remoteDirPath += "/";
            Regex regPattern = new Regex((pattern ?? ".*").ToLower());
            HashSet<string> ignoredFiles = new HashSet<string>();
            if (File.Exists(localDirPath + @"\.deploy-ignore"))
            {
                ignoredFiles.Add(".deploy-ignore");
                foreach (string line in File.ReadAllLines(localDirPath + @"\.deploy-ignore"))
                    ignoredFiles.Add(line.ToLower());
            }

            foreach (string f in Directory.GetFileSystemEntries(localDirPath, "*", SearchOption.AllDirectories))
            {
                if (!regPattern.IsMatch(f.ToLower())) continue;
                string filename = f.Substring(f.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                if (ignoredFiles.Contains(filename.ToLower())) continue;
                string remotePath = f.Substring(localDirPath.Length + 1).Replace('\\', '/');
                PushFile(remoteDirPath + remotePath, f);
            }
        }

        private static string noannoations(string str)
        {
            int sharp = str.IndexOf('#');
            if (sharp < 0) return str;
            return str.Substring(0, sharp);
        }

        private static string[] split(string line)
        {
            line = line.Trim() + " ";
            List<string> result = new List<string>();
            bool qmark = false;
            string sb = "";
            for (int i = 0; i < line.Length;++i )
            {
                if (line[i] == '"')
                {
                    qmark = !qmark;
                    continue;
                }
                if (!qmark && char.IsWhiteSpace(line[i]))
                {
                    while (i<line.Length && char.IsWhiteSpace(line[i])) ++i;
                    i--;
                    result.Add(sb);
                    sb = "";
                    continue;
                }
                sb += line[i];
            }
            return result.ToArray();
        }
    }
}
