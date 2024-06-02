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
using System.ComponentModel;
using System;
using System.Collections.Generic;


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
            Console.WriteLine(sqlCommand.Substring(0, 100));
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
                        case string s when s.StartsWith("GetMessages"):
                            await Task.Run(() => GetMessages(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("RefactorMessage"):
                            await Task.Run(() => RefactorMessage(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("addUserToChat"):
                            await Task.Run(() => addUserToChat(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("DeleteMessages"):
                            await Task.Run(() => DeleteMessages(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("DeleteChat"):
                            await Task.Run(() => DeleteChat(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("DeleteAllMessagesFromUser"):
                            await Task.Run(() => DeleteAllMessagesFromUser(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("AddUserData"):
                            await Task.Run(() => AddUserData(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("GetAllUserData"):
                            await Task.Run(() => GetUserData(sqlCommand, senderId, dbConnection, lobby, webSocket));
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


        // Удаление чата со всеми пользователями и всеми сообщениями
        private async Task DeleteChat(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    // DeleteChat requestId idChat
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    string idChat = credentials[1];

                    cursor.CommandText = @"SELECT id_user FROM users WHERE id_chat = @idChat;";
                    cursor.Parameters.AddWithValue("idChat", idChat);

                    var reader = await cursor.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        string idUser = reader.GetString(0);

                        using (var command = dbConnection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM messages WHERE id_sender = @idUser;";
                            command.Parameters.AddWithValue("idUser", idUser);

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    reader.Close();

                    cursor.CommandText = @"DELETE FROM users WHERE id_chat = @idChat;";
                    cursor.Parameters.AddWithValue("idChat", idChat);

                    await cursor.ExecuteNonQueryAsync();

                    cursor.CommandText = @"DELETE FROM chat WHERE id_chat = @idChat;";
                    cursor.Parameters.AddWithValue("idChat", idChat);

                    await cursor.ExecuteNonQueryAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error DeleteMessages command: {e}");
            }
        }


        // Удаление сообщений от пользователя
        private async Task DeleteAllMessagesFromUser(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    // DeleteAllMessagesFromUser requestId idSender
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idSender = int.Parse(credentials[1]);

                    cursor.CommandText = @"DELETE FROM messages WHERE id_sender = @idSender;";
                    cursor.Parameters.AddWithValue("idSender", idSender);

                    await cursor.ExecuteNonQueryAsync();

                    cursor.CommandText = @"DELETE FROM users WHERE id_user = @idSender;";
                    cursor.Parameters.AddWithValue("idSender", idSender);

                    await cursor.ExecuteNonQueryAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error DeleteMessages command: {e}");
            }
        }


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
                        cursor.Parameters.AddWithValue("idSender", idSender);

                        cursor.CommandText = $"SELECT COUNT(*) FROM users WHERE id_user = @idSender";

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


        private string GenerateUniqueChatId(NpgsqlConnection dbConnection)
        {
            try
            {
                Random random = new Random();

                string idChat = "";

                do
                {
                    StringBuilder sb = new StringBuilder();

                    sb.Append(random.Next(9) + 1);

                    for (int i = 0; i < 127; ++i)
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


        // Создание нового чата 
        private async Task ChatCreate(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    // ChatCreate requestId isPrivacy chatPassword
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);
                    Console.WriteLine("\n\n" + "YAAAAAAAAAAAAAAAAAAAAAAA NE JIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIV" + "\n\n");

                    int requestId = int.Parse(credentials[0]);
                    bool isPrivacy = bool.Parse(credentials[1]);
                    string chatPassword = credentials.Count == 3 ? credentials[2] : "";

                    string idChat = GenerateUniqueChatId(dbConnection);

                    cursor.CommandText = @"INSERT INTO chat (id_chat, chat_password, is_privacy) VALUES (@idChat, @chatPassword, @isPrivacy);";
                    cursor.Parameters.AddWithValue("idChat", idChat);
                    cursor.Parameters.AddWithValue("chatPassword", chatPassword);
                    cursor.Parameters.AddWithValue("isPrivacy", isPrivacy);

                    await cursor.ExecuteNonQueryAsync();

                    Console.WriteLine("Chat Created");

                    lobby.SendMessagePlayer(idChat, ws, requestId);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error ChatCreate command: {e}");
            }
        }


        // Добавить пользователя в чат
        private async Task addUserToChat(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    Console.WriteLine(sqlCommand);

                    int idUser = GenerateUniqueUserId(dbConnection);

                    // addUserToChat requestId publicKey idChat chatPassword 
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    credentials.RemoveAt(0);

                    byte[] publicKey = Convert.FromBase64String(credentials[0]);
                    credentials.RemoveAt(0);

                    if (credentials.Count == 2 || credentials.Count == 1)
                    {
                        // Если чат с паролем
                        string idChat = credentials[0];
                        string chatPassword = credentials.Count == 2 ? credentials[1] : "";

                        cursor.Parameters.AddWithValue("idChat", idChat);
                        if (chatPassword != null)
                        {
                            cursor.Parameters.AddWithValue("chatPassword", chatPassword);
                        }

                        cursor.CommandText = @"SELECT id_chat FROM chat WHERE id_chat = @idChat" +
                            (chatPassword != null ? " AND chat_password = @chatPassword" : "") + ";";

                        using (var reader = cursor.ExecuteReader())
                        {
                            if (await reader.ReadAsync())
                            {
                                reader.Close();

                                cursor.Parameters.AddWithValue("idUser", idUser);
                                cursor.Parameters.AddWithValue("idChat", idChat);
                                cursor.Parameters.AddWithValue("publicKey", publicKey);

                                cursor.CommandText = @"INSERT INTO users (id_user, id_chat, public_key) VALUES (@idUser, @idChat, @publicKey);";

                                await cursor.ExecuteNonQueryAsync();

                                Console.WriteLine($"Success");

                                lobby.SendMessagePlayer(idUser.ToString(), ws, requestId);
                            }
                            else
                            {
                                Console.WriteLine("No matching records found.");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error addUserToChat command: {e}");
            }
        }


        // Изменение сообщения
        private async Task RefactorMessage(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    // RefactorMessage requestId idMsg idSender msg
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idMsg = int.Parse(credentials[1]);
                    int idSender = int.Parse(credentials[2]);
                    byte[] msg = Convert.FromBase64String(credentials[3]);

                    cursor.Parameters.AddWithValue("idMsg", idMsg);
                    cursor.Parameters.AddWithValue("msg", msg);
                    cursor.Parameters.AddWithValue("idSender", idSender);

                    cursor.CommandText = @"UPDATE messages SET msg = @msg WHERE (id_msg = @idMsg AND id_sender = @idSender);";

                    await cursor.ExecuteNonQueryAsync();

                    lobby.SendMessagePlayer("true", ws, requestId);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error RefactorMessage command: {e}");
            }
        }


        // Удаление сообщения
        private async Task DeleteMessages(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    // DeleteMessages requestId idSender idMsg
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));

                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);
                    int idSender = int.Parse(credentials[1]);
                    int idMsg = int.Parse(credentials[2]);

                    cursor.Parameters.AddWithValue("idMsg", idMsg);
                    cursor.Parameters.AddWithValue("idSender", idSender);

                    cursor.CommandText = @"DELETE FROM messages WHERE (id_msg = @idMsg AND id_sender = @idSender);";

                    await cursor.ExecuteNonQueryAsync();

                    lobby.SendMessagePlayer($"/ans true", ws, requestId);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error DeleteMessages command: {e}");
            }
        }


        // Вернуть сообщения
        private async Task GetMessages(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            List<string> credentials = new List<string>(sqlCommand.Split(' '));
            credentials.RemoveAt(0);
            int requestId = int.Parse(credentials[0]);
            long kChats = long.Parse(credentials[1]);
            int index = 2;

            try
            {
                var (adI, messageString) = await CreateGetMessagesStrAsync(dbConnection, credentials, kChats, index);
                index = adI;
                if (!string.IsNullOrEmpty(messageString))
                {
                    lobby.SendMessagePlayer(messageString, ws, requestId);
                }
                else
                {
                    lobby.SendMessagePlayer("0", ws, requestId);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in GetMessages: {e}");
            }
        }


        private async Task<(int, string)> CreateGetMessagesStrAsync(NpgsqlConnection dbConnection, List<string> credentials, long kChats, int startIndex)
        {
            StringBuilder str = new StringBuilder();
            int index = startIndex;
            int returnChatsCount = 0;

            for (int k = 0; k < kChats; k++)
            {
                string chatId = credentials[index++];
                long kSender = long.Parse(credentials[index++]);

                var (adI, missMsg) = await GetMessagesForAuthorsAsync(dbConnection, chatId, credentials, kSender, index);
                index = adI;
                if (missMsg == null || missMsg.Count == 0)
                {
                    continue;
                }

                returnChatsCount++;
                str.Append($" {chatId} {missMsg.Count}");

                foreach (var entry in missMsg)
                {
                    var (authorId, publicKey) = entry.Key;
                    var messages = entry.Value;
                    int messageCount = messages.Count;

                    if (publicKey == Array.Empty<byte>())
                    {
                        str.Append($" {authorId} false {messageCount}");
                    }
                    else
                    {
                        str.Append($" {authorId} true {Convert.ToBase64String(publicKey)} {messageCount}");
                    }

                    foreach (var msg in messages)
                    {
                        if (msg.is_erase)
                        {
                            str.Append($" {msg.id_msg} {msg.is_erase}");
                        }
                        else
                        {
                            str.Append($" {msg.id_msg} {msg.is_erase} {msg.time_msg.ToString("dd.MM.yyyy HH:mm:ss")} {Convert.ToBase64String(msg.msg)}");
                        }
                    }
                }
            }

            return (index, returnChatsCount > 0 ? $"{returnChatsCount}{str}" : "");
        }


        private async Task<(int, Dictionary<(int, byte[]), List<Message>>)> GetMessagesForAuthorsAsync(NpgsqlConnection dbConnection, string chatId, List<string> credentials, long kSender, int startIndex)
        {
            Dictionary<(int, byte[]), List<Message>> messagesByAuthors = new Dictionary<(int, byte[]), List<Message>>();
            int index = startIndex;

            List<int> idUsers = new List<int>();

            using (var cursor = dbConnection.CreateCommand())
            {
                cursor.Parameters.AddWithValue("chatId", chatId);

                cursor.CommandText = @"SELECT id_user FROM users WHERE id_chat = @chatId";

                using (var reader = await cursor.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        idUsers.Add(reader.GetInt32(0));
                    }
                }
            }

            for (int i = 0; i < kSender; i++)
            {
                if (!int.TryParse(credentials[index++], out int authorId))
                {
                    Console.WriteLine($"Invalid authorId at index {index - 1}");
                    continue;
                }

                idUsers.Remove(authorId);

                if (!bool.TryParse(credentials[index++], out bool authorKey))
                {
                    Console.WriteLine($"Invalid authorId at index {index - 1}");
                    continue;
                }

                if (!int.TryParse(credentials[index++], out int kMsg))
                {
                    Console.WriteLine($"Invalid kMsg at index {index - 1}");
                    continue;
                }

                byte[] publicKey = Array.Empty<byte>();

                if (!authorKey)
                {
                    using (var cursor = dbConnection.CreateCommand())
                    {
                        cursor.Parameters.AddWithValue("authorId", authorId);

                        cursor.CommandText = @"SELECT public_key FROM users WHERE id_user = @authorId";

                        using (var reader = await cursor.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                publicKey = reader.GetFieldValue<byte[]>(0);
                            }
                        }
                    }
                }

                List<int> messageIds = new List<int>();
                for (int j = 0; j < kMsg; j++)
                {
                    if (!int.TryParse(credentials[index++], out int messageId))
                    {
                        Console.WriteLine($"Invalid messageId at index {index - 1}");
                        continue;
                    }
                    messageIds.Add(messageId);
                }

                using (var command = dbConnection.CreateCommand())
                {
                    command.CommandText = @"
                SELECT m.id_msg, m.time_msg, m.msg, m.is_erase
                FROM messages m
                JOIN users u ON m.id_sender = u.id_user
                WHERE u.id_chat = @chatId 
                  AND m.id_sender = @authorId
                  AND ((m.id_msg = ANY(@messageIds) OR m.id_msg > @lastMsgId) OR (m.is_erase = true))
                ORDER BY m.id_msg";
                    command.Parameters.AddWithValue("@chatId", chatId);
                    command.Parameters.AddWithValue("@authorId", authorId);
                    command.Parameters.AddWithValue("@messageIds", messageIds.ToArray());
                    command.Parameters.AddWithValue("@lastMsgId", messageIds.Last());

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int messageId = reader.GetInt32(0);
                            DateTime timeMsg = reader.GetDateTime(1);
                            byte[] msg = reader.GetFieldValue<byte[]>(2);
                            bool is_erase = reader.GetBoolean(3);

                            if (!messagesByAuthors.ContainsKey((authorId, publicKey)))
                            {
                                messagesByAuthors[(authorId, publicKey)] = new List<Message>();
                            }

                            messagesByAuthors[(authorId, publicKey)].Add(new Message
                            {
                                id_msg = messageId,
                                time_msg = timeMsg,
                                msg = msg,
                                is_erase = is_erase
                            });
                        }
                    }
                }
            }

            for (int i = 0; i < idUsers.Count; i++)
            {
                bool authorKey = false;

                byte[] publicKey = Array.Empty<byte>();

                if (!authorKey)
                {
                    using (var cursor = dbConnection.CreateCommand())
                    {
                        cursor.Parameters.AddWithValue("authorId", idUsers[i]);

                        cursor.CommandText = @"SELECT public_key FROM users WHERE id_user = @authorId";

                        using (var reader = await cursor.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                publicKey = reader.GetFieldValue<byte[]>(0);
                            }
                        }
                    }
                }

                int messageIds = 0;

                using (var command = dbConnection.CreateCommand())
                {
                    command.CommandText = @"
                SELECT m.id_msg, m.time_msg, m.msg, m.is_erase
                FROM messages m
                JOIN users u ON m.id_sender = u.id_user
                WHERE u.id_chat = @chatId 
                  AND m.id_sender = @authorId
                  AND ((m.id_msg >= @messageIds) OR (m.is_erase = true))
                ORDER BY m.id_msg";
                    command.Parameters.AddWithValue("@chatId", chatId);
                    command.Parameters.AddWithValue("@authorId", idUsers[i]);
                    command.Parameters.AddWithValue("@messageIds", messageIds);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int messageId = reader.GetInt32(0);
                            DateTime timeMsg = reader.GetDateTime(1);
                            byte[] msg = reader.GetFieldValue<byte[]>(2);
                            bool is_erase = reader.GetBoolean(3);

                            if (!messagesByAuthors.ContainsKey((idUsers[i], publicKey)))
                            {
                                messagesByAuthors[(idUsers[i], publicKey)] = new List<Message>();
                            }

                            messagesByAuthors[(idUsers[i], publicKey)].Add(new Message
                            {
                                id_msg = messageId,
                                time_msg = timeMsg,
                                msg = msg,
                                is_erase = is_erase
                            });
                        }
                    }
                }
            }

            return (index, messagesByAuthors);
        }


        // Отправить сообщение
        private async Task SendMessage(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                // SendMessage requestId id_sender time_msg msg
                List<string> credentials = new List<string>(sqlCommand.Split(' '));

                credentials.RemoveAt(0);

                int requestId = int.Parse(credentials[0]);
                int idSender = int.Parse(credentials[1]);

                string time1 = credentials[2];
                string time2 = credentials[3];
                string time = time1 + " " + time2;
                string format = "yyyy-MM-dd HH:mm:ss";
                CultureInfo provider = CultureInfo.InvariantCulture;
                DateTimeOffset timeMsg = DateTimeOffset.ParseExact(time, format, provider);

                byte[] msg = new byte[0];
                try
                {
                    msg = Convert.FromBase64String(credentials[4]);
                }
                catch (FormatException ex)
                {
                    // Handle the format exception (e.g., invalid Base64 string)
                    Console.WriteLine("Error decoding Base64 string: " + ex.Message);
                }

                long idMsg;

                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.Parameters.AddWithValue("idSender", idSender);
                    cursor.CommandText = "SELECT id_msg FROM messages WHERE msg = '' AND id_sender = @idSender";

                    object result = await cursor.ExecuteScalarAsync();

                    if (result != null)
                    {
                        idMsg = (long)result;
                    }
                    else
                    {
                        cursor.CommandText = "SELECT COUNT(*) FROM messages WHERE id_sender = @idSender";
                        idMsg = (long)await cursor.ExecuteScalarAsync();
                    }
                }

                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.CommandText = "INSERT INTO messages (id_msg, id_sender, time_msg, msg) VALUES (@idMsg, @idSender, @timeMsg, @msg)";
                    // Добавление параметров в команду для предотвращения SQL-инъекций
                    cursor.Parameters.AddWithValue("idSender", idSender);
                    cursor.Parameters.AddWithValue("timeMsg", timeMsg);
                    cursor.Parameters.AddWithValue("msg", msg);
                    cursor.Parameters.AddWithValue("idMsg", idMsg);

                    await cursor.ExecuteNonQueryAsync();

                    lobby.SendMessagePlayer($"true", ws, requestId);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error SendMessage command: {e}");
            }
        }


        // Получение всех данных по id_user
        private async Task GetUserData(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                // SendMessage requestId id_user
                List<string> credentials = new List<string>(sqlCommand.Split(' '));

                credentials.RemoveAt(0);

                int requestId = int.Parse(credentials[0]);

                int idUser = int.Parse(credentials[1]);

                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.CommandText = "SELECT * FROM user_account WHERE id_user = @idUser";
                    // Добавление параметров в команду для предотвращения SQL-инъекций
                    cursor.Parameters.AddWithValue("idUser", idUser);

                    using (var reader = await cursor.ExecuteReaderAsync())
                    {
                        byte[] user_content = reader.GetFieldValue<byte[]>(0);
                        byte[] password = reader.GetFieldValue<byte[]>(1);
                        byte[] login = reader.GetFieldValue<byte[]>(2);

                        string result = Convert.ToBase64String(user_content) + Convert.ToBase64String(password) + Convert.ToBase64String(login);

                        lobby.SendMessagePlayer(result, ws, requestId);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error GetUserData command: {e}");
            }
        }


        // Регистрация юзера
        private async Task AddUserData(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                // SendMessage requestId id_user user_content password login
                List<string> credentials = new List<string>(sqlCommand.Split(' '));

                credentials.RemoveAt(0);

                int requestId = int.Parse(credentials[0]);

                string idUser = credentials[1];

                byte[] userContent = [];
                try
                {
                    userContent = Convert.FromBase64String(credentials[2]);
                }
                catch (FormatException ex)
                {
                    Console.WriteLine("Error decoding Base64 string: " + ex.Message);
                }

                byte[] password = Convert.FromBase64String(credentials[3]);
                byte[] login = Convert.FromBase64String(credentials[4]);

                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.CommandText = "INSERT INTO user_account (id_user, user_content, password, login) VALUES (@idUser, @userContent, @password, @login)";
                    // Добавление параметров в команду для предотвращения SQL-инъекций
                    cursor.Parameters.AddWithValue("idUser", idUser);
                    cursor.Parameters.AddWithValue("userContent", userContent);
                    cursor.Parameters.AddWithValue("login", login);
                    cursor.Parameters.AddWithValue("password", password);

                    await cursor.ExecuteNonQueryAsync();

                    lobby.SendMessagePlayer($"true", ws, requestId);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error AddUserData command: {e}");
            }
        }
    }
}
