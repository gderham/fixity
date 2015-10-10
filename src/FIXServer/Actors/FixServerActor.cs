namespace Fixity.FIXServer.Actors
{
    using System;

    using Akka.Actor;
    using log4net;

    using Core;
    using Core.Actors;
    using Fixity.Actors;
    using FixMessages;

    /// <summary>
    /// A FIX Server.
    /// Uses a TcpServer actor to communicate with FIX clients, with
    /// FIX message parsing performed by a FixInterpreter actor.
    /// </summary>
    public class FixServerActor : ReceiveActor
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(FixServerActor));
        
        #region Incoming messages

        public class StartListening { }

        public class Shutdown { }

        #endregion

        #region Internal messages

        private class SendHeartbeat { }

        /// <summary>
        /// An instruction to perform regular admin functions
        /// e.g. checking the connection is still alive.
        /// </summary>
        private class PerformAdmin { }

        #endregion

        private readonly string _serverCompID = "FIXTEST";
        private string _clientCompID;

        /// <summary>
        /// The interval between heartbeat messages sent to the client.
        /// This is negotiated in the received Logon message.
        /// </summary>
        private TimeSpan _heartbeatInterval;

        private ICancelable _heartbeatCanceller;

        private DateTime _lastHeartbeatArrivalTime;

        /// <summary>
        /// The interval for admin functions e.g. checking a heartbeat message 
        /// has been received from the client in the last interval.
        /// </summary>
        private TimeSpan _adminInterval = TimeSpan.FromSeconds(1); // Must be < _heartbeatInterval

        private ICancelable _adminCanceller;

        /// <summary>
        /// The sequence number of the last message received from the client.
        /// </summary>
        private int _inboundSequenceNumber;

        /// <summary>
        /// The sequence number of the last message sent to the client.
        /// </summary>
        private int _outboundSequenceNumber;

        private IActorRef _tcpServerActor;

        /// <summary>
        /// We can communicate with the FIX client using typed FIX messages
        /// via this actor.
        /// </summary>
        private IActorRef _fixInterpreterActor;

        /// <param name="serverAddress">The port this server should listen
        /// to for clients.</param>
        public FixServerActor(int port)
        {
            // Self    <->    TcpServer
            //   \             /
            //    FixInterpreter

            var fixInterpreterProps = Props.Create(() => new FixInterpreterActor(Self));
            _fixInterpreterActor = Context.ActorOf(fixInterpreterProps);

            var tcpServerProps = Props.Create(() => new TcpServerActor(port,
                _fixInterpreterActor, FIXUtilities.ParseFixMessagesFromText));
            _tcpServerActor = Context.ActorOf(tcpServerProps);

            //TODO: Is this sensible? Perhaps the TCPServer should add itself to the interpreter.
            _fixInterpreterActor.Tell(new FixInterpreterActor.AddClient(_tcpServerActor));
                        
            Ready();
        }

        /// <summary>
        /// Set up a scheduled repeated message sent to Self.
        /// </summary>
        private Cancelable ScheduleRepeatedMessage(TimeSpan interval, object message)
        {
            var canceller = new Cancelable(Context.System.Scheduler);
            Context.System.Scheduler.ScheduleTellRepeatedly(interval, interval,
                Self, message, ActorRefs.Nobody, canceller);
            return canceller;
        }

        #region States

        /// <summary>
        /// Waiting for a StartListening instruction to listen for clients.
        /// </summary>
        public void Ready()
        {
            Receive<StartListening>(message =>
            {
                BecomeWaitingForClient();
            });
        }

        public void WaitingForClient()
        {
            Receive<TcpServerActor.ClientConnected>(message =>
            {
                BecomeConnected();
            });
        }

        public void Connected()
        {
            // Messages from client
            Receive<LogonMessage>(message =>
            {
                _clientCompID = message.SenderCompID;
                _heartbeatInterval = message.HeartBeatInterval;
                _inboundSequenceNumber = message.MessageSequenceNumber;
                //TODO: Check sequence number on received messages is as expected.
                BecomeLoggedOn();
            });

            Receive<HeartbeatMessage>(message =>
            {
                _lastHeartbeatArrivalTime = DateTime.UtcNow; //TODO: The server could timestamp messages as they arrive to avoid checking the time here?
                _inboundSequenceNumber = message.MessageSequenceNumber;
            });

            // Exogenous system message
            Receive<Shutdown>(message =>
            {
                _log.Debug("Shutting down.");
                BecomeShutdown();
            });
        }

        public void LoggedOn()
        {
            // Messages from client
            Receive<HeartbeatMessage>(message =>
            {
                _lastHeartbeatArrivalTime = DateTime.UtcNow; //TODO: The server could timestamp messages as they arrive to avoid checking the time here?
                _inboundSequenceNumber = message.MessageSequenceNumber;
            });

            // Exogenous system message
            Receive<Shutdown>(message =>
            {
                _log.Debug("Shutting down.");
                BecomeShutdown();
            });

            // Internal messages
            Receive<SendHeartbeat>(message =>
            {
                _log.Debug("Sending heartbeat message.");
                var heartbeatMessage = new HeartbeatMessage(_serverCompID, _clientCompID,
                    _outboundSequenceNumber++);
                _fixInterpreterActor.Tell(heartbeatMessage);
            });
            
            Receive<PerformAdmin>(message =>
            {
                // Check a heartbeat was received in the last 2 * heartbeat interval,
                // otherwise assume connection is lost and shut down.
                // This is the only method employed to check the connection.
                if (DateTime.UtcNow - _lastHeartbeatArrivalTime > _heartbeatInterval + _heartbeatInterval)
                {
                    _log.Debug("Client connection lost.");
                    BecomeShutdown();
                }
            });
        }
     
        public void ShuttingDown()
        {
            //TODO: Wait for shutdown confirmation from TcpServer then go to Ready state.
        }

        #endregion

        #region Transitions

        public void BecomeWaitingForClient()
        {
            Become(WaitingForClient);
            _tcpServerActor.Tell(new TcpServerActor.StartListening());
        }

        public void BecomeConnected()
        {
            _log.Debug("Connected to client.");
            Become(Connected);
            _tcpServerActor.Tell(new TcpServerActor.AcceptMessages());
        }

        public void BecomeLoggedOn()
        {
            _log.Debug("Client is logged on.");

            _outboundSequenceNumber = 0;
            _lastHeartbeatArrivalTime = DateTime.UtcNow;

            Become(LoggedOn);

            // We confirm logon by replying to the client.
            var response = new LogonMessage(_serverCompID, _clientCompID, _outboundSequenceNumber++,
                _heartbeatInterval);
            _fixInterpreterActor.Tell(response);

            // Start sending heartbeat messages to the client
            _heartbeatCanceller = ScheduleRepeatedMessage(_heartbeatInterval, new SendHeartbeat());

            // And check for returned heartbeats in an admin function
            _adminCanceller = ScheduleRepeatedMessage(_adminInterval, new PerformAdmin());
        }

        public void BecomeShutdown()
        {
            _log.Debug("Shutting down.");

            //TODO: Send a logoff message to client
            Become(ShuttingDown);

            _heartbeatCanceller.Cancel();
            _adminCanceller.Cancel();

            // Tell tcp server to shut down.
            _tcpServerActor.Tell(new TcpServerActor.Shutdown());
        }

        #endregion
    }
}
