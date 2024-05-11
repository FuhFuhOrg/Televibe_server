namespace shooter_server
{
    public class Message
    {
        public string id_chat {  get; set; }
        public int id_sender { get; set; }
        public int id_msg { get; set; }
        public DateTime time_msg { get; set; }
        public byte[] msg { get; set; }

        public Message()
        {
            msg = new byte[0];
        }

        public string GetString()
        {
            string base64Msg = Convert.ToBase64String(msg);
            return $"{id_chat} {id_sender} {id_msg} {time_msg.ToString("dd.MM.yyyy HH:mm:ss")} {base64Msg}";
        }
    }
}