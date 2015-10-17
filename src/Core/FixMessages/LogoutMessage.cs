namespace Fixity.Core.FixMessages
{
    /// <summary>
    /// Logout message 5.
    /// http://www.onixs.biz/fix-dictionary/4.3/msgType_5_5.html
    /// </summary>
    public class LogoutMessage : BaseMessage
    {
        // Mandatory fields left out:
        // 1. EncryptMethod (assume = 0 - Unencrypted)

        public LogoutMessage(string senderCompID, string targetCompID,
            int messageSequenceNumber) :
            base(senderCompID, targetCompID, messageSequenceNumber)
        {
        }
    }
}
