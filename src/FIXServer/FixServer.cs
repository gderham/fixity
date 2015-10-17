namespace Fixity.FixServer
{
    using System;
    using System.Collections.Generic;

    using Akka.Actor;
    using Core.Actors;
    using Core;

    /// <summary>
    /// A FIX server.
    /// Handles connection, logon, heartbeating and canned responses
    /// to requests from a FIX client.
    /// </summary>
    public class FixServer
    {
        private ActorSystem _actorSystem;
        private IActorRef _fixServerActor;

        public FixServer(int port)
        {
            _actorSystem = ActorSystem.Create("FIXServer");

            // Some invented FX spot rates
            var prices = new Dictionary<string, double>()
            {
                { "USDGBP", 0.65575 },
                { "USDJPY", 119.75 }
            };

            var fixParser = new FixParser();

            var tcpServerProps = Props.Create(() => new TcpServerActor(port,
                FixParser.ExtractFixMessages));
            Func<IActorRefFactory, IActorRef> tcpServerCreator =
                (context) => context.ActorOf(tcpServerProps, "TcpServer");

            var fixInterpreterProps = Props.Create(() => new FixInterpreterActor(fixParser));
            Func<IActorRefFactory, IActorRef> fixInterpreterCreator =
                (context) => context.ActorOf(fixInterpreterProps, "FixInterpreter");

            var fixServerProps = Props.Create(() => new Actors.FixServerActor(tcpServerCreator,
                fixInterpreterCreator, prices));
            _fixServerActor = _actorSystem.ActorOf(fixServerProps, "FixServer");
            
        }

        /// <summary>
        /// Start listening for a client connection.
        /// </summary>
        public void Start()
        {
            _fixServerActor.Tell(new Actors.FixServerActor.StartListening());
            _actorSystem.AwaitTermination();
        }

        /// <summary>
        /// Stop the server running and disconnect the socket.
        /// </summary>
        public void Stop()
        {
            _fixServerActor.Tell(new Actors.FixServerActor.Shutdown());
            _actorSystem.AwaitTermination();
        }
        
    }
}
