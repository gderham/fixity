namespace Fixity.FIXServer
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 9700;
            var server = new FixServerStub(port);
            server.Start();
        }
    }
}
