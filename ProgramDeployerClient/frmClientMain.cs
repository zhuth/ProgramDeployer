using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace ProgramDeployerClient
{
    public partial class frmClientMain : Form
    {
        const string PROC_NAME = "ProgramDeployerClient";

        private Httpd _httpd = new Httpd(Properties.Settings.Default.Port);
        private static Dictionary<string, string> _aliases = null;
        
        public frmClientMain()
        {
            InitializeComponent();

            if (!Application.ExecutablePath.ToLower().EndsWith(@"\" + PROC_NAME.ToLower() + ".exe"))
            {
                Process[] otherInstances = Process.GetProcessesByName(PROC_NAME);
                foreach (var proc in otherInstances)
                    proc.Kill();
                string dst = Application.ExecutablePath.Substring(0, Application.ExecutablePath.LastIndexOf('\\') + 1) + PROC_NAME + ".exe";
                File.Copy(Application.ExecutablePath, dst, true);
                Process.Start(dst);
                Environment.Exit(0);
            }

            _httpd.Start();
            _httpd.RequestArrival += _httpd_RequestArrival;

            if (_aliases == null)
            {
                _aliases = new Dictionary<string, string>();
                foreach (string alias in Properties.Settings.Default.MonitoringDirectories.Split(';'))
                {
                    if (string.IsNullOrEmpty(alias)) continue;
                    int eqm = alias.IndexOf('=');
                    if (eqm < 0) continue;
                    string value = alias.Substring(eqm + 1);
                    if (string.IsNullOrEmpty(value)) continue;
                    if (value[value.Length - 1] != Path.DirectorySeparatorChar) value += Path.DirectorySeparatorChar;
                    _aliases.Add(alias.Substring(0, eqm), value);
                }
            }
        }

        private static string convertAliasUrlToFilePath(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (url[0] == '/') url = url.Substring(1);
            int slash = url.IndexOf('/');
            if (slash < 0) return null;

            string alias = url.Substring(0, slash);
            if (_aliases.ContainsKey(alias)) return _aliases[alias] + url.Substring(slash+1).Replace('/', Path.DirectorySeparatorChar);

            return null;
        }

        byte[] _httpd_RequestArrival(string Url, string Method, PropertiesParser QueryStrings, BinaryBuffer rawData, out string ContentType)
        {
#if !DEBUG
            try
            {
#endif
            string filename = convertAliasUrlToFilePath(Url);

            // 检查签名
            string sign = QueryStrings["sign"];
            if (sign == null || sign != Httpd.Sign(Url, QueryStrings, Properties.Settings.Default.HashKey))
            {
                ContentType = "403";
                return null;
            }

            while (Url.StartsWith("//")) Url = Url.Substring(1);

            switch (Method)
            {
                case "GET":
                case "POST":
                    ContentType = "text/plain";
                    StringBuilder sbReturn = new StringBuilder();

                    if (filename == null)
                    {
                        if (Url.StartsWith("/proc"))
                        {
                            Process[] processes;
                            if (QueryStrings["name"] == null || QueryStrings["name"] == "*")
                                processes = Process.GetProcesses();
                            else
                                processes = Process.GetProcessesByName(QueryStrings["name"]);
                            foreach (var proc in processes)
                            {
                                sbReturn.AppendLine(proc.ProcessName + "\t" + proc.PagedMemorySize64 + "\t" + proc.WorkingSet64);
                            }
                            switch (QueryStrings["action"])
                            {
                                case null:
                                    break;
                                case "kill":
                                    if (QueryStrings["name"] != null)
                                        foreach (var proc in processes) proc.Kill();
                                    break;
                                case "start":
                                    if (QueryStrings["name"] != null) 
                                        try
                                        {
                                            Process.Start(convertAliasUrlToFilePath(QueryStrings["name"]) ?? QueryStrings["name"]);
                                        }
                                        catch { }
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (!File.Exists(filename))
                        {
                            ContentType = "404";
                            return null;
                        }
                        // 对该文件作什么操作？
                        if (QueryStrings["md5"] != null)
                        {
                            // 计算指定文件的 MD5 值
                            sbReturn.AppendLine(MD5Crypt.HashFile(filename, "md5"));
                        }
                        else if (QueryStrings["sha1"] != null)
                        {
                            // 计算指定文件的 MD5 值
                            sbReturn.AppendLine(MD5Crypt.HashFile(filename, "sha1"));
                        }
                        else if (QueryStrings["info"] != null)
                        {
                            FileInfo fi = new FileInfo(filename);
                            sbReturn.AppendLine("Length = " + fi.Length);
                            sbReturn.AppendLine("CreationTimeUtc = " + fi.CreationTimeUtc);
                            sbReturn.AppendLine("LastAccessTimeUtc = " + fi.LastAccessTimeUtc);
                            sbReturn.AppendLine("LastWriteTimeUtc = " + fi.LastWriteTimeUtc);
                        }
                        else
                        {
                            ContentType = "application/oct-stream";
                            return File.ReadAllBytes(filename);
                        }
                    }
                    return Encoding.UTF8.GetBytes(sbReturn.ToString());
                case "PUSH":
                    int length;
                    if (!int.TryParse(QueryStrings["length"] ?? "_", out length))
                    {
                        ContentType = "500";
                        return null;
                    }

                    if (!File.Exists(filename))
                    {
                        FileInfo fi = new FileInfo(filename);
                        if (!Directory.Exists(fi.DirectoryName))
                            Directory.CreateDirectory(fi.DirectoryName);
                    }

                    using (var sw = new FileStream(filename, FileMode.Create))
                    {
                        try
                        {
                            while (length > 0)
                            {
                                byte[] buf = new byte[102400];
                                int recv = rawData.Read(ref buf);
                                length -= recv;
                                sw.Write(buf, 0, recv);
                            }
                        }
                        catch { }
                    }

                    ContentType = "text/plain";
                    return Encoding.UTF8.GetBytes("PUSH OK");
                default:
                    ContentType = "501";
                    return null;
            }
#if !DEBUG
            }
            catch
            {
                ContentType = "500";
                return null;
            }
#endif
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }
    }
}
