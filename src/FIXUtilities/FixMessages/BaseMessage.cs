namespace Fixity.FixMessages
{
    /// <summary>
    /// Base class for all typed FIX messages.
    /// See http://www.onixs.biz/fix-dictionary.html for property name definitions.
    /// </summary>
    public abstract class BaseMessage
    {
        // Mandatory fields left out:
        // 1. BodyLength
        // 2. Checksum (these are specific to the text representation)
        // 3. MsgType (is inherent to the specific subclass)

        public BaseMessage(string senderCompID, string targetCompID, int messageSequenceNumber)
        {
            SenderCompID = senderCompID;
            TargetCompID = targetCompID;
            MessageSequenceNumber = messageSequenceNumber;
        }

        public string SenderCompID { get; private set; }
        public string TargetCompID { get; private set; }
        public int MessageSequenceNumber { get; private set; }
    }
}