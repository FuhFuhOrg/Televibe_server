namespace shooter_server
{
    public class Message
    {
        public int idSender { get; set; }
        public int idMsg { get; set; }
        public DateTime timeMsg { get; set; }
        public byte[] msg { get; set; }

        public Message()
        {
            msg = new byte[0];
        }

        public string GetString()
        {
            return idSender.ToString() + " " + idMsg.ToString() + " " + timeMsg.ToString() + " " + msg.ToString() + " ";
        }
    }
}