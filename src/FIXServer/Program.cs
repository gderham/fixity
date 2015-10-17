namespace Fixity.FixServer
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 9700;
            var server = new FixServer(port);
            server.Start();
        }
    }
}
