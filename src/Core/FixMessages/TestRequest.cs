namespace Fixity.Core.FixMessages
{
    /// <summary>
    /// Test Request message 1.
    /// http://www.onixs.biz/fix-dictionary/4.3/msgType_1_1.html
    /// </summary>
    public class TestRequest : BaseMessage
    {
        public TestRequest(string senderCompID, string targetCompID,
            int messageSequenceNumber, string testReqID) :
            base(senderCompID, targetCompID, messageSequenceNumber)
        {
        }

        public string TestReqID { get; private set; }
    }
}
