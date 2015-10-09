﻿namespace Fixity.FIXServer.Actors
{
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using System.Collections.Generic;

    using Akka.Actor;
    using log4net;

    using Core;
    using System;

    class FixServerActor : ReceiveActor
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(FixServerActor));
        
        #region Messages

        public class StartListening { }

        public class Shutdown { }

        public class ClientConnected { }

        public class ReceivedText
        {
            //TODO: Improve naming
            public ReceivedText(FixMessageInfo messageInfo)
            {
                MessageInfo = messageInfo;
            }

            public FixMessageInfo MessageInfo { get; private set; }
        }

        private class SingleFixMessage
        {
            public SingleFixMessage(string message)
            {
                Message = message;
            }
            public string Message;
        }

        private class WaitForNextMessage
        {
            /// <param name="partialMessage">If the socket has already received
            /// part of a message.</param>
            public WaitForNextMessage(string partialMessage)
            {
                Message = partialMessage;
            }

            public WaitForNextMessage()
            {
            }
            public string Message { get; private set; }
        }

        private class SendHeartbeat { }

        #endregion

        private readonly IPAddress _localhost = IPAddress.Parse("127.0.0.1");
        private readonly int _port;

        private TcpListener _tcpListener;
        private TcpClient _tcpClient;

        /// <summary>
        /// The interval between heartbeat messages sent to the client.
        /// This is negotiated in the received Logon message.
        /// </summary>
        private TimeSpan _heartbeatInterval;

        private ICancelable _heartbeatCanceller;

        /// <summary>
        /// The sequence number of the last message received from the client.
        /// </summary>
        private int _inboundSequenceNumber;

        /// <summary>
        /// The sequence number of the last message sent to the client.
        /// </summary>
        private int _outboundSequenceNumber;

        /// <param name="serverAddress">The port this server should listen
        /// to for clients.</param>
        public FixServerActor(int port)
        {
            _port = port;
            Ready();
        }

        protected override void PreStart()
        {
            _tcpListener = new TcpListener(new IPEndPoint(_localhost, _port));
        }

        #region States

        /// <summary>
        /// Not listening for client connections.
        /// Waiting for a StartListening message.
        /// </summary>
        public void Ready()
        {
            Receive<StartListening>(message =>
            {
                Become(ListeningForClient);
            });
        }

        public void ListeningForClient()
        {
            _log.Debug("Listening for FIX clients on port " + _port.ToString());

            Receive<ClientConnected>(message =>
            {
                Become(Connected);
            });

            _tcpListener.Start();

            _tcpListener.AcceptTcpClientAsync().ContinueWith(task =>
            {
                _tcpClient = task.Result;
                return new ClientConnected();
            },
            TaskContinuationOptions.AttachedToParent &
            TaskContinuationOptions.ExecuteSynchronously)
            .PipeTo(Self);
        }

        public void ReceiveMessages()
        {
            Receive<WaitForNextMessage>(message =>
            {
                NetworkStream stream = _tcpClient.GetStream();

                // Read from stream until we have a full message
                int bufferSize = 500; // this doesn't restrict the message size
                var buffer = new byte[bufferSize];

                stream.ReadAsync(buffer, 0, bufferSize).ContinueWith(task =>
                {
                    int bytesReceived = task.Result;
                    // Note here we start with the previously received partial message.
                    var text = message.Message + Encoding.ASCII.GetString(buffer, 0, bytesReceived);

                    FixMessageInfo messageInfo = FIXUtilities.ParseFixMessagesFromText(text);
                    //TODO: Just return the text and process in the handler
                    return new ReceivedText(messageInfo);
                },
                TaskContinuationOptions.AttachedToParent &
                TaskContinuationOptions.ExecuteSynchronously)
                .PipeTo(Self);
            });

            Receive<ReceivedText>(message =>
            {
                // We received some bytes which contains (possibly multiple) FIX messages.
                foreach (string msg in message.MessageInfo.CompleteMessages)
                {
                    Self.Tell(new SingleFixMessage(msg));
                }

                // And possibly also received a partial message at the end,
                // in which case we keep it to prefix the subsequently
                // received data.
                if (message.MessageInfo.RemainingText != null)
                {
                    Self.Tell(new WaitForNextMessage(message.MessageInfo.RemainingText));
                }
            });
        }

        public void Connected()
        {
            _log.Debug("Connected to FIX client.");

            Receive<SingleFixMessage>(message =>
            {
                Dictionary<string,string> fixFields = FIXUtilities.ParseFixMessage(message.Message);
                
                // To connect, a client must send a Logon (A) message
                //TODO: Check this is a valid logon message (or heartbeat?) then Become(LoggedOn)
                if (fixFields["35"] == "A")
                {
                    //TODO: Move this into a FIXMessage class
                    _heartbeatInterval = TimeSpan.FromSeconds(int.Parse(fixFields["108"]));
                    _inboundSequenceNumber = int.Parse(fixFields["34"]);
                    
                    Become(LoggedOn);
                    Self.Tell(new WaitForNextMessage());
                }
                else // TODO: else if heartbeat (ok)
                {
                    // else error and disconnect
                }
            });

            ReceiveMessages(); //TODO: Split out into a separate TCP -> FIX message actor

            Self.Tell(new WaitForNextMessage());
        }

        public void LoggedOn()
        {
            _log.Debug("Client is logged on.");

            _outboundSequenceNumber = 0;
            
            //TODO: Improve by receiving messages typed by FIX message type.
            Receive<SingleFixMessage>(message =>
            {
                // if heartbeat... etc
                _log.Debug("Received messsage: " + message.Message);

                Self.Tell(new WaitForNextMessage());
            });

            Receive<SendHeartbeat>(message =>
            {
                string fixHeartbeatMessage = FIXUtilities.CreateHeartbeatMessage(_outboundSequenceNumber++);
                byte[] buffer = Encoding.ASCII.GetBytes(fixHeartbeatMessage);

                NetworkStream stream = _tcpClient.GetStream();
                stream.WriteAsync(buffer, 0, buffer.Length);
            });

            ReceiveMessages();

            // Start sending heartbeat messages to the clietn
            _heartbeatCanceller = new Cancelable(Context.System.Scheduler);
            Context.System.Scheduler.ScheduleTellRepeatedly(_heartbeatInterval,
                _heartbeatInterval, Self, new SendHeartbeat(), ActorRefs.Nobody,
                _heartbeatCanceller);
            
        }

        #endregion

    }
}