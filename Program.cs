using System.Net;
using System.Threading.Tasks;

namespace Proxy2
{
    class Program
    {
        static void Main(string[] args)
        {
            ProxyServer proxy = new ProxyServer(8005, IPAddress.Parse("127.0.0.1"));
            Task serverTask = new Task(() => proxy.Start());
            serverTask.Start();

            Task.WaitAll(serverTask);
        }
    }
}
