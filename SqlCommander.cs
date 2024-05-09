using System.Text;
using System.Net.WebSockets;
using Npgsql;
using System.Security.Cryptography;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Specialized;
using System.Globalization;
using System.Numerics;
using System.Diagnostics;
using System.Net;

namespace shooter_server
{
    public class SqlCommander
    {
        private string host;
        private string user;
        private string password;
        private string database;
        private int port;

        public SqlCommander(string host, string user, string password, string database, int port)
        {
            this.host = host;
            this.user = user;
            this.password = password;
            this.database = database;
            this.port = port;
        }

        public async Task ExecuteSqlCommand(Lobby lobby, WebSocket webSocket, string sqlCommand, Player player)
        {
            Console.WriteLine(sqlCommand);
            // Создание соединения с базой данных
            using (var dbConnection = new NpgsqlConnection($"Host={host};Username={user};Password={password};Database={database};Port={port}"))
            {
                await dbConnection.OpenAsync();
                Console.WriteLine(dbConnection.ConnectionString);

                int senderId = player.Id;

                if (dbConnection.State != ConnectionState.Open)
                {
                    Console.WriteLine("DB connection error");

                    return;
                }

                try
                {
                    // Определение типа SQL-команды
                    switch (sqlCommand)
                    {
                        case string s when s.StartsWith("SendMessage"):
                            await Task.Run(() => SendMessage(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("ChatCreate"):
                            await Task.Run(() => ChatCreate(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("GetMessage"):
                            await Task.Run(() => GetMessages(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("RefactorMessage"):
                            await Task.Run(() => RefactorMessage(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("CheckWebSocket"):
                            await Task.Run(() => CheckWebSocket(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("DeleteMessages"):
                            await Task.Run(() => DeleteMessages(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("RefreshListMessages"):
                            await Task.Run(() => RefreshListMessages(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("SearchMessage"):
                            await Task.Run(() => SearchMessageByKeyWord(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("SearchMessage"):
                            await Task.Run(() => SearchMessageByIdMsg(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        default:
                            Console.WriteLine("Command not found");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error executing SQL command: {e}");
                }
            }
        }



        //-------------------------------------------------------------------------------------------------------------------------------------------

        // ИСПРАВИТЬ ПОИСК СООБЩЕНИЙ, ДОБАВИТЬ SQL-КОМАНДУ

        //-------------------------------------------------------------------------------------------------------------------------------------------


        // SearchMessage idSender idRecepient idMsg 
        private async Task SearchMessageByIdMsg(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idSender = int.Parse(credentials[1]);
                    int idRecepient = int.Parse(credentials[2]);
                    byte[] msg = Encoding.UTF8.GetBytes(credentials[3]);

                    cursor.CommandText = @"UPDATE chat_users SET msg = @msg WHERE idMsg = @idMsg;";

                    cursor.Parameters.AddWithValue("idMsg", idSender);
                    cursor.Parameters.AddWithValue("msg", idRecepient);
                    cursor.Parameters.AddWithValue("msg", msg);

                    await cursor.ExecuteNonQueryAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error RefactorMessage command: {e}");
                }
            }
        }


        // SearchMessage idSender idRecepient msg 
        private async Task SearchMessageByKeyWord(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idSender = int.Parse(credentials[1]);
                    int idRecepient = int.Parse(credentials[2]);
                    byte[] msg = Encoding.UTF8.GetBytes(credentials[3]);

                    cursor.CommandText = @"UPDATE chat_users SET msg = @msg WHERE idMsg = @idMsg;";

                    cursor.Parameters.AddWithValue("idMsg", idSender);
                    cursor.Parameters.AddWithValue("msg", idRecepient);
                    cursor.Parameters.AddWithValue("msg", msg);

                    await cursor.ExecuteNonQueryAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error RefactorMessage command: {e}");
                }
            }
        }


        //-------------------------------------------------------------------------------------------------------------------------------------------

        // ИСПРАВИТЬ ПОИСК СООБЩЕНИЙ, ДОБАВИТЬ SQL-КОМАНДУ

        //-------------------------------------------------------------------------------------------------------------------------------------------



        // GenerateUniqueUserId
        private int GenerateUniqueUserId(NpgsqlConnection dbConnection)
        {
            try
            {
                const int max_user_id = int.MaxValue;
                Random random = new Random();

                int idSender;

                do
                {
                    idSender = random.Next(max_user_id);

                    using (var cursor = dbConnection.CreateCommand())
                    {
                        cursor.Parameters.AddWithValue("id_sender", idSender);

                        cursor.CommandText = $"SELECT COUNT(*) FROM users WHERE id_sender = @idSender";

                        long idUserCount = (long)cursor.ExecuteScalar();

                        if (idUserCount > 0)
                        {
                            idSender = -1;
                        }
                    }
                } while (idSender == -1);

                return (int)idSender;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error GenerateUniqueUserId command: {e}");
                return -1;
            }
        }


        // GenerateUniqueChatId
        private string GenerateUniqueChatId(NpgsqlConnection dbConnection)
        {
            try
            {
                Random random = new Random();

                string idChat = "";

                do
                {
                    StringBuilder sb = new StringBuilder();

                    for (int i = 0; i < 128; ++i)
                    {
                        sb.Append(random.Next(10));
                    }

                    idChat = sb.ToString();

                    using (var cursor = dbConnection.CreateCommand())
                    {
                        Console.WriteLine(idChat + " " + idChat.GetType());

                        cursor.Parameters.AddWithValue("idChat", idChat);

                        cursor.CommandText = $"SELECT COUNT(*) FROM chat WHERE id_chat = @idChat";

                        long idChatCount = (long)cursor.ExecuteScalar();

                        if (idChatCount > 0)
                        {
                            idChat = "";
                        }
                    }
                } while (idChat == "");

                return idChat;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error GenerateUniqueUserId command: {e}");
                return "";
            }
        }

        // RegistrationUser
        private async Task RegistrationUser(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);

                    int idSender = GenerateUniqueUserId(dbConnection);

                    cursor.Parameters.AddWithValue("idUser", idSender);
                    cursor.CommandText = @"INSERT INTO users (id_sender) VALUES (@idSender);";
                    await cursor.ExecuteNonQueryAsync();
                }    
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error RegistrationUser command: {e}");
            }
        }


        // ChatCreate requestId chatPassword isPrivacy
        private async Task ChatCreate(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    string chatPassword = credentials[1];
                    bool isPrivacy = bool.Parse(credentials[2]);

                    // ----------------------------------------------------
                    int idUser = GenerateUniqueUserId(dbConnection);
                    // ----------------------------------------------------

                    string idChat = GenerateUniqueChatId(dbConnection);

                    cursor.Parameters.AddWithValue("idUser", idUser);
                    cursor.Parameters.AddWithValue("idChat", idChat);

                    cursor.CommandText = @"INSERT INTO chat_users (id_user, id_chat) VALUES (@idUser, @idChat);";
                    await cursor.ExecuteNonQueryAsync();

                    cursor.Parameters.AddWithValue("idChat", idChat);
                    cursor.Parameters.AddWithValue("chatPassword", chatPassword);
                    cursor.Parameters.AddWithValue("isPrivacy", isPrivacy);

                    cursor.CommandText = @"INSERT INTO chat (id_chat, chat_password, is_privacy) VALUES (@idChat, @chatPassword, @isPrivacy);";
                    await cursor.ExecuteNonQueryAsync();

                    lobby.SendMessagePlayer(idChat + " " + idUser, ws, requestId);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error ChatCreate command: {e}");
                }
            }
        }


        // RefactorMessage idMsg msg
        private async Task RefactorMessage(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idMsg = int.Parse(credentials[1]);
                    byte[] msg = Encoding.UTF8.GetBytes(credentials[2]);

                    cursor.CommandText = @"UPDATE chat_users SET msg = @msg WHERE idMsg = @idMsg;";

                    cursor.Parameters.AddWithValue("idMsg", idMsg);
                    cursor.Parameters.AddWithValue("msg", msg);

                    await cursor.ExecuteNonQueryAsync();

                    // "senderId  "
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error RefactorMessage command: {e}");
                }
            }
        }


        // Возвращать idUser
        private async Task ChatConnect(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {

                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error ChatConnect command: {e}");
                }
            }
        }


        // CheckWebSocket requestId
        private void CheckWebSocket(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                List<string> credentials = new List<string>(sqlCommand.Split(' '));

                credentials.RemoveAt(0);

                int requestId = int.Parse(credentials[0]);

                if (ws.State == WebSocketState.Open)
                {
                    Console.WriteLine("WebSocket is open");

                    //lobby.SendMessagePlayer();
                }
                else
                {
                    Console.WriteLine("WebSocket is closed");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error CheckWebSocket command: {e}");
            }
        }


        // DeleteMessages requestId idMsg
        private async Task DeleteMessages(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idMsg = int.Parse(credentials[1]);

                    cursor.CommandText = @"DELETE FROM chat_users WHERE idMsg = @idMsg;";
                    cursor.Parameters.AddWithValue("idMsg", idMsg);

                    await cursor.ExecuteNonQueryAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error DeleteMessages command: {e}");
                }
            }
        }


        // RefreshListMessages requestId idRecepient kMessages
        private async Task<List<Message>> RefreshListMessages(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            List<Message> messages = new List<Message>();

            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idSender = int.Parse(credentials[1]);
                    int idRecepient = int.Parse(credentials[2]);
                    int kMessages = int.Parse(credentials[3]);

                    // Запрос на последние kMessages непрочитанных сообщений
                    cursor.CommandText = @"SELECT * FROM chat_users
                                    WHERE idRecepient = @idRecepient AND isRead = false
                                    ORDER BY timeMsg DESC
                                    LIMIT @kMessages;";

                    cursor.Parameters.AddWithValue("idRecepient", idRecepient);
                    cursor.Parameters.AddWithValue("kMessages", kMessages);

                    using (var reader = await cursor.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Message message = new Message
                            {
                                idSender = reader.GetInt32(0),
                                idMsg = reader.GetInt32(1),
                                timeMsg = reader.GetDateTime(2),
                                msg = reader.GetFieldValue<byte[]>(3),
                            };

                            messages.Add(message);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error RefreshListMessages command: {e}");
                }
            }

            return messages;
        }


        // Готово
        private async Task GetMessages(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            List<Message> messages = new List<Message>();

            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    // GetMessages requestId idSender idMsg
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idSender = int.Parse(credentials[1]);
                    int idMsg = int.Parse(credentials[2]);

                    cursor.Parameters.AddWithValue("id_sender", idSender);
                    cursor.Parameters.AddWithValue("id_msg", idMsg);

                    cursor.CommandText = @"SELECT * FROM messages
                      WHERE (id_sender = @idSender AND id_msg > @idMsg)
                      ORDER BY id_msg DESC;";

                    string result = "";

                    using (var reader = await cursor.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Message message = new Message
                            {
                                idSender = reader.GetInt32(0),
                                idMsg = reader.GetInt32(2),
                                timeMsg = reader.GetDateTime(3),
                                msg = reader.GetFieldValue<byte[]>(4)
                            };

                            result += message.GetString();
                        }
                    }

                    // Возвращает строку типа: idSender idMsg timeMsg msg 
                    lobby.SendMessagePlayer($"/ans {result}", ws, requestId);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error GetMessages command: {e}");
                }
            }
        }


        // Готово
        private async Task SendMessage(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    // SendMessage requestId id_msg id_sender time_msg msg
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idMsg = int.Parse(credentials[1]);
                    int idSender = int.Parse(credentials[2]);

                    string time = credentials[3];
                    string format = "yyyy-MM-dd HH:mm:ss.fff";
                    CultureInfo provider = CultureInfo.InvariantCulture;
                    DateTimeOffset timeMsg = DateTimeOffset.ParseExact(time, format, provider);

                    byte[] msg = Encoding.UTF8.GetBytes(credentials[3]);

                    // Добавление параметров в команду для предотвращения SQL-инъекций
                    cursor.Parameters.AddWithValue("id_sender", idSender);
                    cursor.Parameters.AddWithValue("id_msg", idMsg);
                    cursor.Parameters.AddWithValue("time_msg", timeMsg);
                    cursor.Parameters.AddWithValue("msg", msg);

                    cursor.CommandText = "INSERT INTO messages (id_msg, id_sender, time_msg, msg) VALUES (@idSender, @idRecepient, @idMsg, @timeMsg, @msg)";

                    await cursor.ExecuteNonQueryAsync();

                    lobby.SendMessagePlayer($"/ans true", ws, requestId);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error SendMessage command: {e}");
            }
        }      
    }
}