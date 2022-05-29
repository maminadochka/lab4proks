using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;

namespace Proxy2
{
    public class ProxyServer
    {
        private const int DefaultBufferSize = 1024;

        private const int DefaultPort = 80;

        private readonly char[] SplitChars = { '\n', '\r' };

        public int TCPPort { get; }

        public IPAddress IP { get; }

        private bool _isAlive;


        public ProxyServer(int port, IPAddress ip)
        {
            TCPPort = port;
            IP = ip;
            _isAlive = true;
        }


        public void Start()
        {
            IPEndPoint localPoint = new IPEndPoint(IP, TCPPort);
            Socket listening = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listening.Bind(localPoint);
                listening.Listen(20);

                while (_isAlive)
                {
                    Socket handler = listening.Accept();
                    Task.Run(() => ProcessSocket(handler));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Start: {0}", e.Message);
            }
        }

        private void ProcessSocket(Socket socket)
        {
            using (NetworkStream clientStream = new NetworkStream(socket))
            {
                StringBuilder totalBuf;
                int totalCount;
                ReadFromStream(clientStream, out totalBuf, out totalCount, Encoding.UTF8);
                string request = totalBuf.ToString();

                try
                {
                    string hostName;
                    int hostPort;
                    GetHostInfo(request, out hostName, out hostPort);

                    if (hostName != string.Empty)
                    {
                        Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        server.Connect(hostName, hostPort);

                        using (NetworkStream serverStream = new NetworkStream(server))
                        {
                            request = ProcessHTTPPath(request);
                            byte[] tmp = Encoding.UTF8.GetBytes(request);
                            serverStream.Write(tmp, 0, tmp.Length);

                            byte[] buf = new byte[4096];
                            int count = serverStream.Read(buf, 0, buf.Length);

                            string mes = Encoding.UTF8.GetString(buf, 0, count);

                            string[] parts = GetHostString(buf).Split(' ');
                            string res = hostName + hostPort;
                            for (int i = 0; i < parts.Length; ++i)
                            {
                                if (!parts[i].Contains("HTTP"))
                                    res = res + " " + parts[i];
                            }
                            Console.WriteLine(res);

                            clientStream.Write(buf, 0, count);

                            //while (serverStream.DataAvailable)
                            serverStream.CopyTo(clientStream);
                        }

                    }
                }
                catch (ArgumentOutOfRangeException e)
                {
                    return;
                }
                catch (Exception e)
                {
                   // Console.WriteLine("EXCEPTION: {0}", e.Message);
                    return;
                }
                finally
                {
                    socket.Disconnect(false);
                    socket.Close();
                }
            }
        }

        private void ReadFromStream(NetworkStream stream, out StringBuilder res, out int totalCount, Encoding encoding)
        {
            res = new StringBuilder(string.Empty);
            totalCount = 0;

            byte[] buf = new byte[DefaultBufferSize];
            do
            {
                int count = stream.Read(buf, totalCount, buf.Length);
                res.Append(encoding.GetString(buf, 0, count));
                totalCount += count;
            }
            while (stream.DataAvailable);
        }

        private string ProcessHTTPPath(string message)
        {
            Regex headerRegex = new Regex(@"http:\/\/[a-z0-9а-яё\:\.]*");
            MatchCollection headers = headerRegex.Matches(message);
            return message.Replace(headers[0].Value, "");
        }

        private string GetHostString(byte[] bytes)
        {
            int pos = 0;
            byte rByte = Encoding.UTF8.GetBytes(new char[] { '\r' })[0];
            byte nByte = Encoding.UTF8.GetBytes(new char[] { '\n' })[0];


            while ((bytes[pos] != rByte) && (bytes[pos] != nByte))
                ++pos;

            return Encoding.UTF8.GetString(bytes, 0, pos);
        }

        private void GetHostInfo(string message, out string hostName, out int hostPort)
        {
            int pos = message.IndexOf("Host");
            hostName = string.Empty;
            hostPort = DefaultPort;

            if (pos != -1)
            {
                pos += 4;
                while ((message[pos] == ':') || (message[pos] == ' '))
                    ++pos;

                int startPos = pos;
                while ((message[pos] != ':') && (message[pos] != '\r') && (message[pos] != '\n'))
                    ++pos;

                hostName = message.Substring(startPos, pos - startPos);
                ++pos;
                startPos = pos;

                while ((message[pos] != '\r') && (message[pos] != '\n'))
                    ++pos;

                if (!int.TryParse(message.Substring(startPos, pos - startPos), out hostPort))
                    hostPort = DefaultPort;
            }
            else
            {
                throw new ArgumentOutOfRangeException("GetHostInfo failure");
            }
        }
    }
}
