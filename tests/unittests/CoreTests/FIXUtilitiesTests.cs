namespace Fixity.CoreTests
{
    using System.Collections.Generic;

    using FluentAssertions;
    using Xunit;

    using Core;

    public class FIXUtilitiesTests
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

        public FIXUtilitiesTests()
        {
            //TODO: Set the BodyLength (9) correctly
            _logonString1WithoutChecksum = "8=FIXT1.1|9=100|35=A|49=SomeClient|56=SomeFacility|34=1|108=30|";

            _logonMessage1 =
                FIXUtilities.SetFIXDelimiter("8=FIXT1.1|9=100|35=A|49=SomeClient|56=SomeFacility|34=1|108=30|10=80|");
            _logonMessage1WithoutChecksum =
                FIXUtilities.SetFIXDelimiter("8=FIXT1.1|9=100|35=A|49=SomeClient|56=SomeFacility|34=1|108=30|");

            _partialMessage = FIXUtilities.SetFIXDelimiter("8=FIXT1.1|9=10");
        }

        #region CreateFixMessage tests

        //TODO: Add more tests

        [Fact]
        public void CreateFixMessage_AppendsCorrectChecksum_ForValidMessage()
        {
            string result = FIXUtilities.CreateFixMessage(_logonString1WithoutChecksum);

            result.Should().Be(_logonMessage1);
        }

        #endregion

        #region ParseFixMessage tests

        [Fact]
        public void ParseFixMessage_ReturnsCorrectFields_ForLogonMessage()
        {   
            var expected = new Dictionary<string, string>()
            {
                {"8",  "FIXT1.1"},
                {"9",  "100"},
                {"35", "A"},
                {"49", "SomeClient"},
                {"56", "SomeFacility"},
                {"34", "1"},
                {"108","30"},
                {"10", "80"}
            };

            Dictionary<string, string> result = FIXUtilities.ParseFixMessage(_logonMessage1);

            result.ShouldBeEquivalentTo(expected);
        }

        #endregion

        #region ParseFixMessagesFromText tests

        //TODO: Test empty message

        [Fact]
        public void ParseFixMessagesFromText_ReturnsMessage_FromTextContainingSingleMessage()
        { 
            MessageInfo result = FIXUtilities.ParseFixMessagesFromText(_logonMessage1);

            result.Should().NotBeNull();
            result.CompleteMessages.Should().Contain(_logonMessage1);
            //TODO: Check enumerable has a single item
        }

        [Fact]
        public void ParseFixMessagesFromText_ReturnsMessageAndRemainder_FromTextContainingMessageAndPartialMessage()
        {
            string text = _logonMessage1 + _partialMessage;
            MessageInfo result = FIXUtilities.ParseFixMessagesFromText(text);

            result.RemainingText.Should().Be(_partialMessage);
            result.CompleteMessages.Should().Contain(_logonMessage1);
            //TODO: Check enumerable has a single item
        }

        [Fact]
        public void ParseFixMessagesFromText_ReturnsTwoMessagesAndRemainder_FromTextContainingTwoMessagesAndPartialMessage()
        {
            string text = _logonMessage1 + _logonMessage1 + _partialMessage; //TODO: Change second to a heartbeat message
            MessageInfo result = FIXUtilities.ParseFixMessagesFromText(text);

            result.RemainingText.Should().Be(_partialMessage);
            result.CompleteMessages.Should().Contain(_logonMessage1);
            //TODO: Check enumerable has two items
        }

        #endregion

    }
}
