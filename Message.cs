namespace shooter_server
{
    public class Message
    {
        public int idSender { get; set; }
        public int idRecepient { get; set; }
        public int idMsg { get; set; }
        public DateTime timeMsg { get; set; }
        public byte[] msg { get; set; }
        public bool isRead {  get; set; }

        public string GetString()
        {
            return "";
        }
    }
}