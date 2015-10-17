namespace Fixity.Core.Actors
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using System.Text;

    using Akka.Actor;
    using log4net;

    /// <summary>
    /// An actor that implements a basic TCP server which can handle a
    /// single client connection and parse received data in whole
    /// messages.
    /// </summary>
    public class TcpServerActor : ReceiveActor
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(TcpServerActor));

        #region Incoming messages

        /// <summary>
        /// Instruction to listen for client connections.
        /// A ClientConnected message is returned when a client connects.
        /// </summary>
        public class StartListening { }

        /// <summary>
        /// Instruction to start reading messages from the client.
        /// </summary>
        public class AcceptMessages { }

        /// <summary>
        /// Sends the specified text to the connected client.
        /// </summary>
        public class SendMessage
        {
            public SendMessage(string text)
            {
                Text = text;
            }

            public string Text { get; private set; }
        }

        public class Subscribe
        {
            public Subscribe(IActorRef actor)
            {
                Actor = actor;
            }

            public IActorRef Actor { get; private set; }
        }

        public class Shutdown { }

        #endregion

        #region Outgoing messages

        public class ClientConnected { }

        public class ReceivedMessage
        {
            public ReceivedMessage(string text)
            {
                Text = text;
            }

            public string Text { get; private set; }
        }

        #endregion

        #region Internal messages

        /// <summary>
        /// Instruction to wait for the next chunk of data to be received
        /// from the TCP client.
        /// </summary>
        private class WaitForNextChunk
        {
            /// <param name="buffer">
            /// Previously received data that isn't a whole message.
            /// </param>
            public WaitForNextChunk(string buffer)
            {
                Buffer = buffer;
            }

            public WaitForNextChunk()
            {
            }
            public string Buffer { get; private set; }
        }

        private class ReceivedChunk
        {
            /// <param name="buffer">
            /// Previously received data that isn't a whole message.
            /// </param>
            public ReceivedChunk(string text)
            {
                Text = text;
            }

            public ReceivedChunk()
            {
            }

            public string Text { get; private set; }
        }

        #endregion

        private readonly IPAddress _localhost = IPAddress.Parse("127.0.0.1");
        private readonly int _port;
        private const int BufferSize = 512; // This doesn't restrict message size.

        private TcpListener _tcpListener;
        private TcpClient _tcpClient;
        private NetworkStream _tcpStream;

        private Func<string, MessageInfo> _messageSplitter;

        /// <summary>
        /// TCP messages are sent to this actor.
        /// </summary>
        private IActorRef _listener;

        /// <summary>
        /// Creates a TCP server that will listen to the specified port
        /// on localhost when it is told to StartListening.
        /// Sends admin messages to parent, sends received TCP messages
        /// to the listener.
        /// </summary>
        /// <param name="messageSplitter">A function that can parse complete
        /// messages from some text, return those and the remaining text.</param>
        public TcpServerActor(int port, Func<string, MessageInfo> messageSplitter)
        {
            _port = port;
            _messageSplitter = messageSplitter;
            Ready();
        }

        #region States
        // State methods should contain only Receive calls. Other logic should go in Transitions.

        /// <summary>
        /// Not listening for client connections. Waiting for a StartListening message.
        /// </summary>
        public void Ready()
        {
            Receive<StartListening>(message =>
            {
                BecomeListeningForClient();
            });

            Receive<Subscribe>(message =>
            {
                _listener = message.Actor;
            });
        }

        public void IsShutdown()
        {
        }
        
        /// <summary>
        /// Listen for and accept client connections.
        /// </summary>
        public void ListeningForClient()
        {
            Receive<ClientConnected>(message =>
            {
                BecomeConnected();
            });

            //TODO: If other messages e.g. SendMessage are received, inform sender of error.
        }

        /// <summary>
        /// We're connected, but don't read data from client until told to.
        /// </summary>
        public void ConnectedToClient()
        {
            Receive<AcceptMessages>(message =>
            {
                BecomeAcceptingMessagesFromClient();
            });
        }

        public void AcceptingMessagesFromClient()
        {
            Receive<SendMessage>(message => // Send message text to the client as ASCII.
            {
                byte[] buffer = Encoding.ASCII.GetBytes(message.Text);
                _tcpStream = _tcpClient.GetStream();
                _tcpStream.WriteAsync(buffer, 0, buffer.Length);
            });
            
            Receive<WaitForNextChunk>(message =>
            {
                var buffer = new byte[BufferSize];

                _tcpStream.ReadAsync(buffer, 0, BufferSize).ContinueWith(task =>
                {
                    if (task.Status == TaskStatus.Faulted)
                    {
                        // This happens if the socket is closed server side.
                        return null;
                    }
                    //TODO: Check for other error states before reading the result

                    int bytesReceived = task.Result;
                    // Note here we start with the previously received chunk.
                    var text = message.Buffer + Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                    
                    return new ReceivedChunk(text);
                },
                TaskContinuationOptions.AttachedToParent &
                TaskContinuationOptions.ExecuteSynchronously)
                .PipeTo(Self);
            });

            Receive<ReceivedChunk>(message =>
            {
                // Check if we have a complete message, if so send to client (who?)
                MessageInfo info = _messageSplitter(message.Text);

                // We received some bytes which contains (possibly multiple) messages.
                foreach (string msg in info.CompleteMessages)
                {
                    _listener.Tell(new ReceivedMessage(msg));
                }

                // And possibly also received a partial message at the end,
                // in which case we keep it to prefix the subsequently
                // received data.
                if (info.RemainingText != null)
                {
                    Self.Tell(new WaitForNextChunk(info.RemainingText));
                }
            });

            Receive<Shutdown>(message =>
            {
                BecomeShutDown();
            });
        }

        #endregion

        #region Transitions
        
        public void BecomeListeningForClient()
        {
            _log.Debug("Listening for TCP clients on port " + _port.ToString());

            Become(ListeningForClient);

            _tcpListener = new TcpListener(new IPEndPoint(_localhost, _port));
            _tcpListener.Start();
            _tcpListener.AcceptTcpClientAsync().ContinueWith(task =>
            {
                _tcpClient = task.Result;
                _tcpStream = _tcpClient.GetStream();
                return new ClientConnected();
            },
            TaskContinuationOptions.AttachedToParent &
            TaskContinuationOptions.ExecuteSynchronously)
            .PipeTo(Self);
        }

        public void BecomeConnected()
        {
            _log.Debug("Connected to client.");
            Become(ConnectedToClient);
            Context.Parent.Tell(new ClientConnected());
        }

        public void BecomeAcceptingMessagesFromClient()
        {
            _log.Debug("Accepting messages from client.");
            Become(AcceptingMessagesFromClient);
            Self.Tell(new WaitForNextChunk());
        }

        public void BecomeShutDown()
        {
            _tcpClient.GetStream().Close();
            _tcpClient.Close();
            _tcpListener.Stop();
            Become(IsShutdown);
        }

        #endregion
    }
}
