namespace Fixity.Core.FixMessages
{
    /// <summary>
    /// Heartbeat message 0.
    /// http://www.onixs.biz/fix-dictionary/4.3/msgType_0_0.html
    /// </summary>
    public class HeartbeatMessage : BaseMessage
    {
        public HeartbeatMessage(string senderCompID, string targetCompID,
            int messageSequenceNumber) :
            base(senderCompID, targetCompID, messageSequenceNumber)
        {
        }
    }
}
