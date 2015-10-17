namespace Fixity.FixServer.Actors
{
    using System;
    using System.Collections.Generic;

    using Akka.Actor;
    using log4net;

    using Core.Actors;
    using Core.FixMessages;

    /// <summary>
    /// A FIX Server implemented using Akka.NET.
    /// 
    /// Uses a TcpServerActor to communicate with FIX clients, and
    /// FIX message parsing (between the FIX ASCII text messages and
    /// the typed Core.FixMessages classes) is performed by a
    /// FixInterpreterActor.
    /// </summary>
    public class FixServerActor : ReceiveActor
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(FixServerActor));
        
        #region Incoming messages

        /// <summary>
        /// Instructs FixServer to start listening for client connections.
        /// </summary>
        public class StartListening { }

        /// <summary>
        /// Instructs FixServer to close any client connections and to return
        /// to its initial Ready state.
        /// </summary>
        public class Shutdown { }

        #endregion

        #region Internal messages

        /// <summary>
        /// Causes the FixServer to send a Heartbeat message to the connected
        /// client.
        /// </summary>
        private class SendHeartbeat { }

        /// <summary>
        /// Causes the FixServer to perform regular admin functions
        /// e.g. checking the client connection is still alive.
        /// </summary>
        private class PerformAdmin { }

        /// <summary>
        /// Indicates the FixServer has given up waiting for the 
        /// client to reply to a Logout message.
        /// </summary>
        private class ClientLogoutTimedOut { }

        /// <summary>
        /// Indicates the FixServer has given up waiting for the
        /// client to reply to a Test Request message.
        /// </summary>
        private class TestRequestTimedOut { }

        #endregion

        /// <summary>
        /// The name of this FixServer is the FIX world.
        /// </summary>
        private readonly string _serverCompID = "FIXTEST";

        /// <summary>
        /// The name of the connected FIX client.
        /// </summary>
        private string _clientCompID;

        /// <summary>
        /// The interval between heartbeat messages sent to the client.
        /// This is negotiated in the Logon message received by the client.
        /// </summary>
        private TimeSpan _heartbeatInterval;

        private ICancelable _heartbeatCanceller;

        private DateTime _lastHeartbeatArrivalTime;

        /// <summary>
        /// The maximum time spent waiting for the client to reciprocate a
        /// Logout message before assuming the client is lost.
        /// </summary>
        private readonly TimeSpan LogoutTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The interval for admin functions e.g. checking a heartbeat message 
        /// has been received from the client in the last interval.
        /// </summary>
        private TimeSpan _adminInterval;

        private ICancelable _adminCanceller;

        /// <summary>
        /// Cancels the waiting for client logout message.
        /// </summary>
        private ICancelable _clientLogoutWaitCanceller;

        private ICancelable _testRequestCanceller;

        /// <summary>
        /// The sequence number of the last message received from the client.
        /// </summary>
        private int _inboundSequenceNumber;

        /// <summary>
        /// The sequence number of the last message sent to the client.
        /// </summary>
        private int _outboundSequenceNumber;

        /// <summary>
        /// A set of instrument rates to be used for quotes sent to the client.
        /// </summary>
        private Dictionary<string, double> _instrumentOfferRates;

        /// <summary>
        /// Enables communicating with a client via a TCP socket.
        /// </summary>
        private IActorRef _tcpServerActor;

        /// <summary>
        /// Converts between typed (Core.FixMessages) messages and ASCII text
        /// message understood by the FIX client.
        /// </summary>
        private IActorRef _fixInterpreterActor;

        /// <param name="prices">
        /// A dictionary of instrument prices (symbol to rate).
        /// For example: USDEUR : 0.87964
        /// </param>
        public FixServerActor(Func<IActorRefFactory, IActorRef> tcpServerCreator,
            Func<IActorRefFactory, IActorRef> fixInterpreterCreator,
            Dictionary<string,double> prices = null)
        {
            _instrumentOfferRates = prices;

            _tcpServerActor = tcpServerCreator(Context);
            _fixInterpreterActor = fixInterpreterCreator(Context);

            _fixInterpreterActor.Tell(new FixInterpreterActor.SetServer(Self));
            _fixInterpreterActor.Tell(new FixInterpreterActor.SetClient(_tcpServerActor));           
            _tcpServerActor.Tell(new TcpServerActor.Subscribe(_fixInterpreterActor));

            BecomeReady();
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

        /// <summary>
        /// A client has connected, now wait for its Logon message.
        /// </summary>
        public void Connected()
        {
            // Messages from client
            Receive<LogonMessage>(message =>
            {
                _clientCompID = message.SenderCompID;
                _heartbeatInterval = message.HeartBeatInterval;
                _adminInterval = TimeSpan.FromMilliseconds(_heartbeatInterval.TotalMilliseconds / 2);
                _inboundSequenceNumber = message.MessageSequenceNumber;
                BecomeLoggedOn();
            });

            Receive<HeartbeatMessage>(message =>
            {
                _lastHeartbeatArrivalTime = DateTime.UtcNow;
                _inboundSequenceNumber = message.MessageSequenceNumber;
            });

            // Exogenous system messages
            Receive<Shutdown>(message =>
            {
                _log.Debug("Shutting down.");
                BecomeShutDown();
            });
        }

        /// <summary>
        /// The main processing state: the client is successfully logged on
        /// and we process its requests until Logout.
        /// </summary>
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

            Receive<QuoteRequestMessage>(message =>
            {
                if (_instrumentOfferRates != null && 
                    _instrumentOfferRates.ContainsKey(message.Symbol))
                {
                    _log.Debug("Responding to RFQ for " + message.Symbol);

                    string quoteID = "Quote" + _outboundSequenceNumber;

                    var quote = new QuoteMessage(_serverCompID, _clientCompID,
                        _outboundSequenceNumber++, message.QuoteReqID, quoteID,
                        message.Symbol, _instrumentOfferRates[message.Symbol]);

                    _fixInterpreterActor.Tell(quote);
                }
                else
                {
                    _log.Error("Unable to quote client for requested instrument: " + 
                        message.Symbol);
                    //TODO: Implement unable to quote message
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
                    _log.Debug("Heartbeat message has not been received from client.");
                    _log.Debug("Sending TestRequest to client.");
                    
                    var testRequest = new TestRequestMessage(_serverCompID, _clientCompID,
                        _outboundSequenceNumber++, "1");
                    _fixInterpreterActor.Tell(testRequest);
                    // We expect to receive a heartbeat with matching TestReqID

                    BecomeWaitingForTestRequestResponse();
                }
            });
        }

        public void WaitingForTestRequestResponse()
        {
            //TODO: Should the server respond to normal messages in this state?

            Receive<HeartbeatMessage>(message =>
            {
                _lastHeartbeatArrivalTime = DateTime.UtcNow;
                _inboundSequenceNumber = message.MessageSequenceNumber;
                _testRequestCanceller.CancelIfNotNull();
                UnbecomeStacked();
            });

            Receive<TestRequestTimedOut>(message =>
            {
                _log.Debug("Timed out waiting for client to respond to Test Request");
                BecomeShutDown();
            });
        }

        /// <summary>
        /// A Logout message has been sent to the client and we're waiting for
        /// this to be reciprocated before shutting down the server.
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
            _heartbeatCanceller =  Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                _heartbeatInterval, _heartbeatInterval, Self, new SendHeartbeat(), ActorRefs.Nobody);

            // And check for returned heartbeats in an admin function
            _adminCanceller = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                _adminInterval, _adminInterval, Self, new PerformAdmin(), ActorRefs.Nobody);
        }

        public void BecomeWaitingForClientLogout()
        {
            _log.Debug("Waiting for client logout confirmation.");

            // Schedule waiting for client logout
            _clientLogoutWaitCanceller = new Cancelable(Context.System.Scheduler);
            _clientLogoutWaitCanceller = Context.System.Scheduler.ScheduleTellOnceCancelable(
                LogoutTimeout, Self, new ClientLogoutTimedOut(), ActorRefs.Nobody);
            
            Become(WaitingForClientLogout);
        }

        public void BecomeWaitingForTestRequestResponse()
        {
            BecomeStacked(WaitingForTestRequestResponse);

            _testRequestCanceller = Context.System.Scheduler.ScheduleTellOnceCancelable(
                _heartbeatInterval, Self, new TestRequestTimedOut(), ActorRefs.Nobody);
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
