using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace ProgramDeployerClient
{
    public class Httpd
    {
        private static readonly Encoding charEncoder = Encoding.UTF8;

        public int Port { get; private set; }
        public bool Running { get; set; }

        public delegate byte[] RequestHandler(string url, string method, PropertiesParser queryStrings, BinaryBuffer rawData, out string contentType);
        public event RequestHandler RequestArrival;

        private TcpListener _listener;
        private Thread _thrListener;

        public Httpd(int port)
        {
            Port = port;
        }

        private static Dictionary<string, string> extensionsToContentType = new Dictionary<string, string>()
        { 
            //{ "extension", "content type" }
            { "htm", "text/html" },
            { "html", "text/html" },
            { "xml", "text/xml" },
            { "txt", "text/plain" },
            { "css", "text/css" },
            { "png", "image/png" },
            { "gif", "image/gif" },
            { "jpg", "image/jpg" },
            { "jpeg", "image/jpeg" },
            { "zip", "application/zip"}
        };

        private void listen()
        {
            if (Running) Stop();
            Running = true;
            _listener = new TcpListener(new IPEndPoint(0, Port));
            _listener.Start();
            while (Running)
            {
                var tc = _listener.AcceptSocket();
                new Thread(() =>
                {
                    try
                    {
                        handleRequest(tc);
                    }
                    catch
                    {
                        try { tc.Close(); }
                        catch { }
                    }
                }).Start();
            }
        }

        public static string GetContentTypeByExtention(string extension)
        {
            if (extensionsToContentType.ContainsKey(extension)) return extensionsToContentType[extension];
            return "application/binary";
        }

        private void handleRequest(Socket clientSocket)
        {
            byte[] buffer = new byte[1024];
            int receivedBCount = clientSocket.Receive(buffer); // Receive the request
            string strReceived = charEncoder.GetString(buffer, 0, receivedBCount);

            // Parse method of the request
            string httpMethod = strReceived.Substring(0, strReceived.IndexOf(" "));

            int start = strReceived.IndexOf(httpMethod) + httpMethod.Length + 1;
            int length = strReceived.LastIndexOf("HTTP") - start - 1;
            string requestedUrl = strReceived.Substring(start, length);

            PropertiesParser queryStrings = new PropertiesParser("");
            requestedUrl = System.Web.HttpUtility.UrlDecode(requestedUrl);

            int qmark = requestedUrl.IndexOf('?');
            if (qmark > 0)
            {
                string fullQueryString = requestedUrl.Substring(qmark + 1);
                requestedUrl = requestedUrl.Substring(0, qmark);
                queryStrings = new PropertiesParser(fullQueryString.Split('&'));
            }

            byte[] toSend = new byte[0];
            string contentType = "text/html";
            byte[] nbuffer;
            int newline = 0;
            for (; newline < receivedBCount; ++newline)
                if (buffer[newline] == (byte)'\n') break;
            nbuffer = new byte[receivedBCount - newline - 1];
            Array.Copy(buffer, newline + 1, nbuffer, 0, receivedBCount - newline - 1);

            if (RequestArrival != null) toSend = RequestArrival(requestedUrl, httpMethod, queryStrings, new BinaryBuffer(0, nbuffer, new NetworkStream(clientSocket)), out contentType);

            if (toSend == null)
            {
                if (contentType == "404") returnError(clientSocket, contentType, "Not Found");
                else if (contentType == "500") returnError(clientSocket, contentType, "Internal Server Error");
                else if (contentType == "403") returnError(clientSocket, contentType, "Forbidden");
                else returnError(clientSocket, "501", "Not Implemented");
                return;
            }
            sendOkResponse(clientSocket, toSend, contentType ?? "text/html");

            /*
            requestedFile = requestedFile.Replace("/", @"\").Replace("\\..", "");
            start = requestedFile.LastIndexOf('.') + 1;
            if (start > 0)
            {
                length = requestedFile.Length - start;
                string extension = requestedFile.Substring(start, length);
                if (File.Exists(contentPath + requestedFile)) //If yes check existence of the file
                    // Everything is OK, send requested file with correct content type:
                    sendOkResponse(clientSocket,
                        File.ReadAllBytes(contentPath + requestedFile), getContentTypeByExtentions(extension));
                else
                    notFound(clientSocket); // We don't support this extension.
                // We are assuming that it doesn't exist.
            }
            else
            {
                // If file is not specified try to send index.htm or index.html
                // You can add more (default.htm, default.html)
                if (requestedFile.Substring(length - 1, 1) != @"\")
                    requestedFile += @"\";
                if (File.Exists(contentPath + requestedFile + "index.htm"))
                    sendOkResponse(clientSocket,
                      File.ReadAllBytes(contentPath + requestedFile + "\\index.htm"), "text/html");
                else if (File.Exists(contentPath + requestedFile + "index.html"))
                    sendOkResponse(clientSocket,
                      File.ReadAllBytes(contentPath + requestedFile + "\\index.html"), "text/html");
                else
                    notFound(clientSocket);
            }
            */
        }

        private void returnError(Socket clientSocket, string ErrorCode, string ErrorDescrption)
        {
            sendResponse(clientSocket, "<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head><body><h2>" + ErrorCode + " - " + ErrorDescrption + "</h2></body></html>",
                ErrorCode + " " + ErrorDescrption, "text/html");
        }

        private void sendOkResponse(Socket clientSocket, byte[] bContent, string contentType)
        {
            sendResponse(clientSocket, bContent, "200 OK", contentType);
        }

        // For strings
        private void sendResponse(Socket clientSocket, string strContent, string responseCode,
                                  string contentType)
        {
            byte[] bContent = charEncoder.GetBytes(strContent);
            sendResponse(clientSocket, bContent, responseCode, contentType);
        }

        // For byte arrays
        private void sendResponse(Socket clientSocket, byte[] bContent, string responseCode,
                                  string contentType)
        {
            try
            {
                byte[] bHeader = charEncoder.GetBytes(
                                    "HTTP/1.1 " + responseCode + "\r\n"
                                  + "Server: Program DeployerClient based on Atasoy Simple Web Server\r\n"
                                  + "Content-Length: " + bContent.Length.ToString() + "\r\n"
                                  + "Connection: close\r\n"
                                  + "Content-Type: " + contentType + "\r\n\r\n");
                clientSocket.Send(bHeader);
                clientSocket.Send(bContent);
                clientSocket.Close();
            }
            catch { }
        }

        public void Stop()
        {
            Running = false;
            try { _listener.Stop(); }
            catch { }
        }

        public void Start()
        {
            _thrListener = new Thread(new ThreadStart(listen));
            _thrListener.Start();
        }

        public static void PushFile(string url, string filename, string hashkey)
        {
            Uri uri = new Uri(url);
            TcpClient tc = new TcpClient();
            tc.Connect(uri.Host, uri.Port);

            FileInfo fi = new FileInfo(filename);
            if (!fi.Exists) return;

            string sign = Sign(uri.AbsolutePath, new PropertiesParser("length=" + fi.Length), hashkey);

            using (var ns = tc.GetStream())
            {
                byte[] buf = charEncoder.GetBytes("PUSH " + uri.AbsolutePath + "?length=" + fi.Length + "&sign=" + sign + " HTTP/1.1fake\r\n");
                ns.Write(buf, 0, buf.Length);
                //for (int i = buf.Length + 1; i <= 1024; ++i) ns.WriteByte(0); // 填充0

                long length = fi.Length;
                buf = new byte[102400];
                using (var sr = new FileStream(filename, FileMode.OpenOrCreate))
                {
                    while (length > 0)
                    {
                        int sent = sr.Read(buf, 0, buf.Length);
                        ns.Write(buf, 0, sent);
                        length -= sent;
                    }
                }
            }
        }

        public static string Sign(string url, PropertiesParser props, string hashkey)
        {
            return MD5Crypt.HashString(url + hashkey + props.ToString('&'));
        }

        public static string DownloadString(string urlAndQuery, string hashkey)
        {
            return new WebClient().DownloadString(GetSignedUrl(urlAndQuery, hashkey)).Trim();
        }

        public static string GetSignedUrl(string urlAndQuery, string hashkey)
        {
            Uri uri = new Uri(urlAndQuery);
            var props = new ProgramDeployerClient.PropertiesParser(uri.Query, '&');
            string sign = ProgramDeployerClient.Httpd.Sign(uri.AbsolutePath, props, Properties.Settings.Default.HashKey);
            if (urlAndQuery.Contains('?')) urlAndQuery += "&"; else urlAndQuery += "?";
            return urlAndQuery + "sign=" + sign;
        }
    }
}
