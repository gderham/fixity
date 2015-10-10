namespace Fixity.Actors
{
    using Akka.Actor;
    using log4net;

    using Core.Actors;
    using FixMessages;

    /// <summary>
    /// Bidirectional message type converter between client (ASCII text FIX
    /// messages) and server (typed subclasses of BaseMessage).
    /// </summary>
    public class FixInterpreterActor : ReceiveActor
    {
        #region Incoming messages

        public class AddClient
        {
            public AddClient(IActorRef actor)
            {
                Actor = actor;
            }
            public IActorRef Actor { get; private set; }
        }

        #endregion

        private static readonly ILog _log = LogManager.GetLogger(typeof(FixInterpreterActor));
        private IActorRef _client;
        private IActorRef _server;
        private FixParser _parser = new FixParser();

        public FixInterpreterActor(IActorRef server)
        {
            _server = server;
            Processing();
        }

        public void Processing()
        {
            Receive<AddClient>(message => //TODO: Do something better
            {
                _client = message.Actor;
            });
            
            // Messages from the client to the server
            Receive<TcpServerActor.ReceivedMessage>(message =>
            {
                _server.Tell(_parser.ParseMessage(message.Text));
            });

            // Messages from the server to the client
            Receive<LogonMessage>(message =>
            {
                string fixMessage = _parser.CreateMessage(message);
                _client.Tell(new TcpServerActor.SendMessage(fixMessage));
            });

            Receive<HeartbeatMessage>(message =>
            {
                string fixMessage = _parser.CreateMessage(message);
                _client.Tell(new TcpServerActor.SendMessage(fixMessage));
            });

        }
    }
}
