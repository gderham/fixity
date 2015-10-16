namespace Fixity.CoreTests
{
    using System.Collections.Generic;

    using FluentAssertions;
    using Xunit;

    using Core;
    using System;
    using FixMessages;

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
        private readonly string _logonString1WithoutChecksum;
        private readonly string _logonMessage1;
        private readonly string _logonMessage1WithoutChecksum;

        private readonly string _partialMessage;

        public FixParserTests()
        {
            //TODO: Set the BodyLength (9) correctly
            _logonString1WithoutChecksum = "8=FIXT1.1|9=100|35=A|49=SomeClient|56=SomeFacility|34=1|108=30|";

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


        #endregion

        #region ConvertFixObjectToFixMessage tests

        [Fact]
        public void ConvertFixObjectToFixMessage_ReturnsCorrectString_ForLogonMessage()
        {
            var messageObject = new LogonMessage("Client", "Bank", 1,
                TimeSpan.FromSeconds(30));
            
            string result = (new FixParser()).ConvertFixObjectToFixMessage(messageObject);

            string expected = "8=FIXT1.1\u00019=35\u000135=A\u000149=Client\u000156=Bank\u000134=1\u0001108=30\u000110=70\u0001";

            result.Should().Be(expected);
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
