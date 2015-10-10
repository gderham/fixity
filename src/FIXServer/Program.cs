using System.Threading;

namespace Fixity.FIXServer
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 9700;
            var server = new FixServerStub(port);
            server.Start();

            //Thread.Sleep(10000);

           // server.Stop();
        }
    }
}
