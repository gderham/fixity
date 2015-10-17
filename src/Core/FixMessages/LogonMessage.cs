namespace Fixity.Core.FixMessages
{
    using System;

    /// <summary>
    /// Logon message A.
    /// http://www.onixs.biz/fix-dictionary/4.3/msgType_A_65.html
    /// </summary>
    public class LogonMessage : BaseMessage
    {
        // Mandatory fields left out:
        // 1. EncryptMethod (assume = 0 - Unencrypted)

        public LogonMessage(string senderCompID, string targetCompID,
            int messageSequenceNumber, TimeSpan heartBeatInterval) :
            base(senderCompID, targetCompID, messageSequenceNumber)
        {
            HeartBeatInterval = heartBeatInterval;
        }
            
        public TimeSpan HeartBeatInterval { get; private set; }
    }
}
