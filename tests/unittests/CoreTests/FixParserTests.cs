namespace Fixity.CoreTests
{
    using System;
    using System.Collections.Generic;

    using FluentAssertions;
    using Xunit;

    using Core;
    using Core.FixMessages;

    public class FixParserTests
    {
        // FIX message format: (delimited by x01)
        //   8=FIXT1.1
        //  BodyLength (message length in bytes following this field up to and including the delimiter preceding the Checksum tag)
        //   9=100
        //  MsgType
        //   35=A
        //  SenderCompID
        //   49=Client name
        //  TargetCompID
        //   56=Server name
        //  MsgSeqNum
        //   34=1
        //  ...
        //  Checksum
        //   10=21 (sum of bytes up to that preceding the checksum mod 256)

        //TODO: Move to separate class - with raw/fix format in same object
        private readonly string _logonMessage1;
        private readonly string _logonMessage1WithoutChecksum;

        private readonly string _partialMessage;

        public FixParserTests()
        {
            _logonMessage1 =
                SetFIXDelimiter("8=FIXT1.1|9=100|35=A|49=SomeClient|56=SomeFacility|34=1|108=30|10=80|");

            _logonMessage1WithoutChecksum =
                SetFIXDelimiter("8=FIXT1.1|9=100|35=A|49=SomeClient|56=SomeFacility|34=1|108=30|");

            _partialMessage = SetFIXDelimiter("8=FIXT1.1|9=10");
        }

        #region Test helper methods

        private static string SetFIXDelimiter(string message)
        {
            return message.Replace('|', FixParser.FixDelimiter);
        }

        /// <summary>
        /// Converts a | delimited string message to use the FIX SOH delimiter
        /// and appends the correct checksum.
        /// </summary>
        /// <param name="message">
        /// For example: 8=FIXT1.1|9=100|35=A|49=SomeClient|56=SomeFacility|34=1|
        /// </param>
        /// <returns>
        /// For example: 8=FIXT1.1[SOH]9=100[SOH]35=A[SOH]49=SomeClient[SOH]56=SomeFacility[SOH]34=1[SOH]10=21[SOH]
        /// </returns>
        private static string CreateFixMessage(string message)
        {
            if (!message.EndsWith("|"))
            {
                throw new ArgumentException("Message must be be terminated by a | delimiter.",
                    "message");
            }

            return FixParser.AddChecksum(SetFIXDelimiter(message));
        }

        #endregion

        #region ConvertFixMessageToFixObject tests
        
        [Fact]
        public void ConvertFixMessageToFixObject_ReturnsLogonMessageObject_ForLogonMessage()
        {
            string message = "8=FIXT1.1\u00019=35\u000135=A\u000149=Client\u000156=Bank\u000134=1\u0001108=30\u000110=70\u0001";

            BaseMessage result = new FixParser().ConvertFixMessageToFixObject(message);

            result.Should().BeOfType<LogonMessage>();
            result.As<LogonMessage>().HeartBeatInterval.TotalSeconds.Should().Be(30);
            result.As<LogonMessage>().MessageSequenceNumber.Should().Be(1);
            result.As<LogonMessage>().SenderCompID.Should().Be("Client");
            result.As<LogonMessage>().TargetCompID.Should().Be("Bank");
        }

        [Fact]
        public void ConvertFixMessageToFixObject_ReturnsQuoteRequestObject_ForQuoteRequestMessage()
        {
            string message = "8=FIXT1.1\u00019=71\u000135=R\u000149=Client\u000156=Bank\u000134=7\u0001131=rfq712\u000155=USDJPY\u000110=171\u0001";

            BaseMessage result = new FixParser().ConvertFixMessageToFixObject(message);

            result.Should().BeOfType<QuoteRequest>();
            result.As<QuoteRequest>().SenderCompID.Should().Be("Client");
            result.As<QuoteRequest>().TargetCompID.Should().Be("Bank");
            result.As<QuoteRequest>().QuoteReqID.Should().Be("rfq712");
            result.As<QuoteRequest>().Symbol.Should().Be("USDJPY");
        }

        [Fact]
        public void ConvertFixMessageToFixObject_ReturnsLogoutObject_ForLogoutMessage()
        {
            string message = "8=FIXT1.1\u00019=28\u000135=5\u000149=Client\u000156=Bank\u000134=1\u000110=2\u0001";

            BaseMessage result = new FixParser().ConvertFixMessageToFixObject(message);

            result.Should().BeOfType<LogoutMessage>();
            result.As<LogoutMessage>().SenderCompID.Should().Be("Client");
            result.As<LogoutMessage>().TargetCompID.Should().Be("Bank");
        }

        [Fact]
        public void ConvertFixMessageToFixObject_ReturnsHeartbeatObject_ForHeartbeatMessage()
        {
            string message = "8=FIXT1.1\u00019=28\u000135=0\u000149=Client\u000156=Bank\u000134=1\u000110=253\u0001";

            BaseMessage result = new FixParser().ConvertFixMessageToFixObject(message);

            result.Should().BeOfType<HeartbeatMessage>();
            result.As<HeartbeatMessage>().SenderCompID.Should().Be("Client");
            result.As<HeartbeatMessage>().TargetCompID.Should().Be("Bank");
        }

        [Fact]
        public void ConvertFixMessageToFixObject_ThrowsException_ForUnknownMessage()
        {
            string message = "8=FIXT1.1\u00019=28\u000135=Z\u000149=Client\u000156=Bank\u000134=1\u000110=253\u0001";

            new FixParser().Invoking(fp => fp.ConvertFixMessageToFixObject(message))
                .ShouldThrow<ArgumentException>()
                .WithMessage("Cannot parse FIX message of type: Z");
        }

        // Add tests for logout, heartbeat, and parse failure

        #endregion

        #region ConvertFixObjectToFixMessage tests

        [Fact]
        public void ConvertFixObjectToFixMessage_ReturnsCorrectString_ForLogonMessage()
        {
            var messageObject = new LogonMessage("Client", "Bank", 1,
                TimeSpan.FromSeconds(30));
            
            string result = new FixParser().ConvertFixObjectToFixMessage(messageObject);

            string expected = "8=FIXT1.1\u00019=35\u000135=A\u000149=Client\u000156=Bank\u000134=1\u0001108=30\u000110=70\u0001";

            result.Should().Be(expected);
        }

        [Fact]
        public void ConvertFixObjectToFixMessage_ReturnsCorrectString_ForLogoutMessage()
        {
            var messageObject = new LogoutMessage("Client", "Bank", 1);

            string result = new FixParser().ConvertFixObjectToFixMessage(messageObject);

            string expected = "8=FIXT1.1\u00019=28\u000135=5\u000149=Client\u000156=Bank\u000134=1\u000110=2\u0001";

            result.Should().Be(expected);
        }

        [Fact]
        public void ConvertFixObjectToFixMessage_ReturnsCorrectString_ForHeartbeatMessage()
        {
            var messageObject = new HeartbeatMessage("Client", "Bank", 1);

            string result = new FixParser().ConvertFixObjectToFixMessage(messageObject);

            string expected = "8=FIXT1.1\u00019=28\u000135=0\u000149=Client\u000156=Bank\u000134=1\u000110=253\u0001";

            result.Should().Be(expected);
        }

        [Fact]
        public void ConvertFixObjectToFixMessage_ReturnsCorrectString_ForTestRequestMessage()
        {
            var messageObject = new TestRequest("Client", "Bank", 7, "Attempt1");

            string result = new FixParser().ConvertFixObjectToFixMessage(messageObject);

            string expected = "8=FIXT1.1\u00019=33\u000135=1\u000149=Client\u000156=Bank\u000134=7\u0001112=\u000110=210\u0001";

            result.Should().Be(expected);
        }

        [Fact]
        public void ConvertFixObjectToFixMessage_ReturnsCorrectString_ForQuoteMessage()
        {
            var messageObject = new Quote("Client", "Bank", 7, "rfq712", "q712", "USDJPY", 119.55);

            string result = new FixParser().ConvertFixObjectToFixMessage(messageObject);

            string expected = "8=FIXT1.1\u00019=71\u000135=S\u000149=Client\u000156=Bank\u000134=7\u0001131=rfq712\u0001117=q712\u000155=USDJPY\u0001133=119.5500\u000110=171\u0001";

            result.Should().Be(expected);
        }

        [Fact]
        public void ConvertFixObjectToFixMessage_ThrowsException_ForUnsupportedMessageType()
        {
            // There is no converter for the QuoteRequest message because only
            // the client sends it; the server has no need to generate it.
            var messageObject = new QuoteRequest("Client", "Bank", 6, "rfq712", "USDJPY");

            new FixParser().Invoking(fp => fp.ConvertFixObjectToFixMessage(messageObject))
                .ShouldThrow<ArgumentException>()
                .WithMessage("Unable to convert Fixity.Core.FixMessages.QuoteRequest to FIX message.");
        }

        #endregion

        #region ParseFixMessageIntoDictionary tests

        [Fact]
        public void ParseFixMessageIntoDictionary_ReturnEmptyDictionary_ForEmptyText()
        {
            Dictionary<string, string> result = FixParser.ParseFixMessageIntoDictionary("");

            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseFixMessageIntoDictionary_ReturnsCorrectFields_ForLogonMessage()
        {   
            var expected = new Dictionary<string, string>()
            {
                {FixParser.BEGINSTRING_FIELD,  FixParser.BEGINSTRING},
                {FixParser.BODYLENGTH_FIELD,   "100"},
                {FixParser.MESSAGETYPE_FIELD,  FixParser.LOGON_MESSAGE},
                {FixParser.SENDERCOMPID_FIELD, "SomeClient"},
                {FixParser.TARGETCOMPID_FIELD, "SomeFacility"},
                {FixParser.MSGSEQNUM_FIELD,    "1"},
                {FixParser.HEARTBTINT_FIELD,   "30"},
                {FixParser.CHECKSUM_FIELD,     "80"}
            };

            Dictionary<string, string> result = FixParser.ParseFixMessageIntoDictionary(_logonMessage1);

            result.ShouldBeEquivalentTo(expected);
        }

        #endregion

        #region ExtractFixMessages tests

        [Fact]
        public void ExtractFixMessages_ReturnsNoMessagesAndEmptyRemainder_FromEmptyText()
        {
            MessageInfo result = FixParser.ExtractFixMessages("");

            result.CompleteMessages.Should().BeEmpty();
            result.RemainingText.Should().BeEmpty();
        }

        [Fact]
        public void ExtractFixMessages_ReturnsMessage_FromTextContainingSingleMessage()
        { 
            MessageInfo result = FixParser.ExtractFixMessages(_logonMessage1);

            result.Should().NotBeNull();
            result.CompleteMessages.Should().Contain(_logonMessage1);
            result.CompleteMessages.Should().ContainSingle();
        }

        [Fact]
        public void ExtractFixMessages_ReturnsMessageAndRemainder_FromTextContainingMessageAndPartialMessage()
        {
            string text = _logonMessage1 + _partialMessage;
            MessageInfo result = FixParser.ExtractFixMessages(text);

            result.RemainingText.Should().Be(_partialMessage);
            result.CompleteMessages.Should().Contain(_logonMessage1);
            result.CompleteMessages.Should().ContainSingle();
        }

        [Fact]
        public void ExtractFixMessages_ReturnsTwoMessagesAndRemainder_FromTextContainingTwoMessagesAndPartialMessage()
        {
            string text = _logonMessage1 + _logonMessage1 + _partialMessage;
            MessageInfo result = FixParser.ExtractFixMessages(text);

            result.RemainingText.Should().Be(_partialMessage);
            result.CompleteMessages.Should().Contain(_logonMessage1);
            result.CompleteMessages.Should().HaveCount(2);
        }

        #endregion

    }
}
