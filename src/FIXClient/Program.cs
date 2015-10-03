using Akka.Actor;

namespace Fixity.FIXClient
{
    /// <summary>
    /// Starts a FIXClient that immediately attempts to connect and logon to a
    /// a FIX server at the configured address.
    /// </summary>
    class Program
    {
        private static Properties.Settings GetConfig()
        {
            return Properties.Settings.Default;
        }

        static void Main(string[] args)
        {
            var config = GetConfig();

            var actorSystem = ActorSystem.Create("FixClientActors");

            var fixSessionProps = Props.Create(() => new Actors.FixSessionActor(config.FixServerAddress));
            var fixSession = actorSystem.ActorOf(fixSessionProps);

            actorSystem.AwaitTermination();
        }

        // 0. Create a simple UI to display received messages, and some buttons to request stuff?
        // 1. Create a FixConnection actor that connects to a TCP socket? What's the best way to do this because we don't want
        // an actor that's unresponsive to messages? Create an async closure that puts a message on the queue?
        // This actor simply forwards the messages to another actor for processing.

        // 2. FixSession actor that represents the session
        // 2a. Creates the FixConnection as a child?
        // 2b. Creates a heartbeat actor to handle heartbeating
        // Changes behaviour Unconnected -> Connecting -> Connected -> LoggingOn -> LoggedOn -> Listening -> 
        // FailedToLogon/Disconnected (waits before reconnecting)

        // Testing (can use Akka Testing?). Need unit tests for Actors?
        // 1. Attempt to connect but fails to connect
        // 2. Connects ok but fails logon
        // 3. Connects ok but no heartbeat
        // 4. Connects ok but immediately disconnects
        // 5. Connects and logs on ok but server immediately disconnects
        // 6. Connects and logs on ok but server immediately logs off
        // 7. Connects and heartbeats received at roughly correct time
        // 8. Connects and seq id is wrong?
        // 9. Connects and sends RFQ and receives reply
        // 10. Connects and sends RFQ but receives no reply
        // 11. Connects and sends RFQ but receives message to resend
        // 12. High frequency message test - what is possible throughput? Check memory.

    }
}
