namespace Fixity.FIXServer.Actors
{
    using System;

    using Akka.Actor;
    using log4net;

    using Core.Actors;
    using Fixity.Actors;
    using FixMessages;
    using System.Collections.Generic;

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

        private class ClientLogoutTimedOut { }

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

        private readonly TimeSpan LogoutTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The interval for admin functions e.g. checking a heartbeat message 
        /// has been received from the client in the last interval.
        /// </summary>
        private TimeSpan _adminInterval = TimeSpan.FromSeconds(1); // Must be < _heartbeatInterval

        private ICancelable _adminCanceller;

        /// <summary>
        /// Cancels the waiting for client logout message.
        /// </summary>
        private ICancelable _clientLogoutWaitCanceller;

        /// <summary>
        /// The sequence number of the last message received from the client.
        /// </summary>
        private int _inboundSequenceNumber;
        //TODO: For each message received from the client, check the
        // sequence number is one more than than the last.
        // If this is not the case remove Fix client messages
        // from the mailbox and send a Resend Request (2) to the client
        // for all missed messages.

        /// <summary>
        /// The sequence number of the last message sent to the client.
        /// </summary>
        private int _outboundSequenceNumber;

        /// <summary>
        /// A set of FX Spot rates to be used for quotes.
        /// </summary>
        private Dictionary<string, double> _fxSpotOfferRates;

        private IActorRef _tcpServerActor;

        /// <summary>
        /// We can communicate with the FIX client using typed FIX messages
        /// via this actor.
        /// </summary>
        private IActorRef _fixInterpreterActor;

        public FixServerActor(Func<IActorRefFactory, IActorRef> tcpServerCreator,
            Func<IActorRefFactory, IActorRef> fixInterpreterCreator,
            Dictionary<string,double> prices)
        {
            _fxSpotOfferRates = prices; //TODO: Update from a message rather than passing in.

            _tcpServerActor = tcpServerCreator(Context);
            _fixInterpreterActor = fixInterpreterCreator(Context);

            _fixInterpreterActor.Tell(new FixInterpreterActor.SetServer(Self));
            _fixInterpreterActor.Tell(new FixInterpreterActor.SetClient(_tcpServerActor));
            
            _tcpServerActor.Tell(new TcpServerActor.Subscribe(_fixInterpreterActor));

            BecomeReady();
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

            // Exogenous system messages
            Receive<Shutdown>(message =>
            {
                _log.Debug("Shutting down.");
                BecomeShutDown();
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

            Receive<LogoutMessage>(message =>
            {
                _log.Debug("Received Logout message from client.");
                _inboundSequenceNumber = message.MessageSequenceNumber;
                _fixInterpreterActor.Tell(new LogoutMessage(_serverCompID, _clientCompID, _outboundSequenceNumber++));
                BecomeShutDown();
            });

            Receive<QuoteRequest>(message =>
            {
                if (_fxSpotOfferRates.ContainsKey(message.Symbol))
                {
                    _log.Debug("Responding to RFQ for " + message.Symbol);

                    string quoteID = "Quote" + _outboundSequenceNumber;

                    var quote = new Quote(_serverCompID, _clientCompID,
                        _outboundSequenceNumber++, message.QuoteReqID, quoteID,
                        message.Symbol, _fxSpotOfferRates[message.Symbol]);

                    _fixInterpreterActor.Tell(quote);
                }
                else
                {
                    // Reply - unable to quote
                    //TODO: Implement
                }
            });

            // Exogenous system messages
            Receive<Shutdown>(message =>
            {
                _log.Debug("Shutting down.");
                _fixInterpreterActor.Tell(new LogoutMessage(_serverCompID, _clientCompID, _outboundSequenceNumber++));
                BecomeWaitingForClientLogout();
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
                    // TODO: A TestRequest (1) message should be sent to the
                    // client to force a heartbeat before giving up.
                    BecomeShutDown();
                }
            });
        }

        /// <summary>
        /// A Logout message has been sent to the client and we're waiting for
        /// a Logout message to be reciprocated before shutting down the server.
        /// </summary>
        public void WaitingForClientLogout()
        {
            Receive<LogoutMessage>(message =>
            {
                _log.Debug("Received Logout message from client.");
                _clientLogoutWaitCanceller.Cancel();
                BecomeShutDown();
            });

            Receive<ClientLogoutTimedOut>(message =>
            {
                _log.Debug("Timed out waiting for client Logout message");
                BecomeShutDown();
            });
        }

        public void ShuttingDown()
        {
            //TODO: Wait for shutdown confirmation from TcpServer then go to Ready state.
        }

        #endregion

        #region Transitions

        public void BecomeReady()
        {
            _log.Debug("Ready");
            Become(Ready);
        }

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

        public void BecomeWaitingForClientLogout()
        {
            _log.Debug("Waiting for client logout confirmation.");

            // Schedule waiting for client logout
            _clientLogoutWaitCanceller = new Cancelable(Context.System.Scheduler);
            Context.System.Scheduler.ScheduleTellOnce(LogoutTimeout,
                Self, new ClientLogoutTimedOut(), ActorRefs.Nobody,
                _clientLogoutWaitCanceller);
            
            Become(WaitingForClientLogout);
        }

        public void BecomeShutDown()
        {
            _log.Debug("Shutting down server.");

            _tcpServerActor.Tell(new TcpServerActor.Shutdown());

            //TODO: Wait for shutdown confirmation from TcpServer.

            _heartbeatCanceller.Cancel();
            _adminCanceller.Cancel();

            BecomeReady();
        }

        #endregion
    }
}
