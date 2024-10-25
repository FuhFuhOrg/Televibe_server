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
            // Создание соединения с базой данных
            using (var dbConnection = new NpgsqlConnection($"Host={host};Username={user};Password={password};Database={database};Port={port}"))
            {
                await dbConnection.OpenAsync();
                //Console.WriteLine(dbConnection.ConnectionString);

                int senderId = player.Id;

                if (dbConnection.State != ConnectionState.Open)
                {
                    //Console.WriteLine("DB connection error");

                    return;
                }

                //Console.WriteLine(sqlCommand);

                try
                {
                    // Определение типа SQL-команды
                    switch (sqlCommand)
                    {

                        case string s when s.StartsWith("AddQueue"):
                            //RW
                            await Task.Run(() => AltSendMessage(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("AddUserToChat"):
                            //RW
                            await Task.Run(() => AddSubuserToChat(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("ChatCreate"):
                            //OK
                            await Task.Run(() => ChatCreate(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("DeleteChat"):
                            //RW
                            await Task.Run(() => DeleteChat(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("Login"):
                            //OK
                            await Task.Run(() => Login(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("AltRegister"):
                            //RW
                            await Task.Run(() => Register(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        default:
                            Console.WriteLine("Command not found");
                            break;
                    }
                }
                catch (Exception e)
                {
                    //Console.WriteLine($"Error executing SQL command: {e}");
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
                //Console.WriteLine($"Error DeleteMessages command: {e}");
            }
        }


        // Добавить подюзера в чат
        private async Task AddSubuserToChat(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    // addSubuserToChat requestId chatId privateKey publicKey anonId
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);

                    int requestId = int.Parse(credentials[0]);

                    byte[] privateKey = Encoding.UTF8.GetBytes(credentials[1]);

                    byte[] publicKey = Encoding.UTF8.GetBytes(credentials[2]);

                    string chatId = credentials[3];

                    byte[] subuserid = Encoding.UTF8.GetBytes(credentials[4]);

                    cursor.Parameters.AddWithValue("chatid", Encoding.UTF8.GetBytes(chatId));

                    // Проверка существования чата
                    cursor.CommandText = @"SELECT chatid FROM chat WHERE chatid = @chatid;";

                    using (var reader = await cursor.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            reader.Close();

                            // Создание нового подюзера
                            int subuserId = GenerateUniqueSubuserId(dbConnection);
                            string username = "std";

                            cursor.Parameters.AddWithValue("subuserid", subuserId);
                            cursor.Parameters.AddWithValue("privatekey", privateKey);
                            cursor.Parameters.AddWithValue("publickey", publicKey);
                            cursor.Parameters.AddWithValue("username", username);
                            cursor.Parameters.AddWithValue("subuserid", subuserid);

                            cursor.CommandText = @"
                                INSERT INTO subuser (chatid, subuserid, unicalcode, username, privatekey, publickey)
                                VALUES (@chatid, @subuserid, @unicalcode, @username, @privatekey, @publickey);";

                            await cursor.ExecuteNonQueryAsync();

                            // Отправка подтверждения
                            lobby.SendMessagePlayer($"true {subuserId.ToString()}", ws, requestId);
                        }
                        else
                        {
                            // Логирование или отправка сообщения об ошибке
                            Console.WriteLine("Chat not found.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Обработка исключений
                Console.WriteLine($"Error in AddSubuserToChat command: {e}");
            }
        }

        private int GenerateUniqueSubuserId(NpgsqlConnection dbConnection)
        {
            int newId = -1;
            bool isUnique = false;

            while (!isUnique)
            {
                // Генерируем случайное число для SubuserId
                newId = new Random().Next(1, int.MaxValue);

                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.CommandText = @"SELECT COUNT(*) FROM subuser WHERE subuserid = @subuserId;";
                    cursor.Parameters.AddWithValue("subuserId", newId);

                    var count = cursor.ExecuteScalar();

                    // Если ID уникален, выходим из цикла
                    if (((long)count) == 0)
                    {
                        isUnique = true;
                    }
                }
            }

            return newId;
        }

        private async Task AltSendMessage(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
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
                    //Console.WriteLine("Error decoding Base64 string: " + ex.Message);
                }

                string idChat = "";
                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.Parameters.AddWithValue("idSender", idSender);
                    cursor.CommandText = "SELECT id_chat FROM users WHERE id_user = @idSender";

                    object result = await cursor.ExecuteScalarAsync();
                    if (result != null)
                    {
                        idChat = (string)result;
                    }
                }

                long idMsg;

                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.Parameters.AddWithValue("idSender", idSender);
                    cursor.CommandText = "SELECT MAX(changeid) FROM chatqueue WHERE user_id = @idSender;";

                    object result = await cursor.ExecuteScalarAsync();

                    if (result != DBNull.Value)
                    {
                        idMsg = (long)result;
                    }
                    else
                    {
                        idMsg = 0;
                    }
                }


                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.CommandText = "INSERT INTO chatqueue (chatid, changeid, changedata, user_id) VALUES (@idChat, @idMsg, @msg, @idSender)";
                    // Добавление параметров в команду для предотвращения SQL-инъекций
                    cursor.Parameters.AddWithValue("idSender", idSender);
                    cursor.Parameters.AddWithValue("idMsg", idMsg + 1);
                    cursor.Parameters.AddWithValue("idChat", idChat);
                    cursor.Parameters.AddWithValue("msg", msg);

                    await cursor.ExecuteNonQueryAsync();

                    lobby.SendMessagePlayer($"true", ws, requestId);
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine($"Error SendMessage command: {e}");
            }
        }

        public async Task ChatCreate(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                using (var cursor = dbConnection.CreateCommand())
                {
                    // sqlCommand: "ChatCreate requestId isPrivacy chatPassword"
                    var credentials = sqlCommand.Split(' ').ToList();

                    credentials.RemoveAt(0); // Remove "ChatCreate"

                    int requestId = int.Parse(credentials[0]);
                    string chatPassword = credentials[1];
                    bool isPrivacy = bool.Parse(credentials[2]);

                    // Генерация уникального ChatId из 256 байтовых символов
                    string chatId = GenerateUniqueChatId(dbConnection);

                    cursor.CommandText = "INSERT INTO chat (chatid, password, isgroup) VALUES (@chatId, @chatPassword, @isGroup);";
                    cursor.Parameters.AddWithValue("chatId", Encoding.UTF8.GetBytes(chatId));
                    cursor.Parameters.AddWithValue("chatPassword", Encoding.UTF8.GetBytes(chatPassword));
                    cursor.Parameters.AddWithValue("isGroup", isPrivacy);

                    await cursor.ExecuteNonQueryAsync();

                    // Отправка сообщения о создании чата клиенту
                    lobby.SendMessagePlayer($"true {chatId}", ws, requestId);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in ChatCreate: {e.Message}");
            }
        }

        private string GenerateUniqueChatId(NpgsqlConnection dbConnection)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            string chatId;
            bool isUnique = false;

            Random random = new Random(); // Создаем объект Random для генерации случайных чисел

            while (!isUnique)
            {
                // Генерация 64 случайных символов
                char[] chatIdChars = new char[64];

                for (int i = 0; i < chatIdChars.Length; i++)
                {
                    // Выбор случайного символа из chars
                    chatIdChars[i] = chars[random.Next(chars.Length)];
                }

                chatId = new string(chatIdChars);

                // Проверка на уникальность в базе данных
                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.CommandText = "SELECT COUNT(*) FROM chat WHERE chatid = @chatId;";
                    cursor.Parameters.AddWithValue("chatId", chatId);

                    var result = cursor.ExecuteScalar();
                    isUnique = result != null && Convert.ToInt32(result) == 0;
                }
            }

            return chatId; // Возвращаем уникальный идентификатор в строковом формате
        }

        private async Task Login(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                // Извлекаем параметры запроса: requestId, login и password
                List<string> credentials = new List<string>(sqlCommand.Split(' '));
                credentials.RemoveAt(0); // Убираем саму команду из запроса

                int requestId = int.Parse(credentials[0]);
                byte[] login = Convert.FromBase64String(credentials[1]);
                byte[] password = Convert.FromBase64String(credentials[2]);

                // Выполняем запрос для получения AnonId по логину и паролю
                using (var cursor = dbConnection.CreateCommand())
                {
                    cursor.CommandText = @"
                        SELECT 
                            a.AnonId 
                        FROM 
                            Anon a
                        WHERE 
                            a.Login = @login AND a.Password = @password";

                    // Добавляем параметры запроса
                    cursor.Parameters.AddWithValue("login", login);
                    cursor.Parameters.AddWithValue("password", password);

                    // Выполняем запрос и читаем результат
                    using (var reader = await cursor.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            int anonId = reader.GetInt32(0);

                            // Отправляем AnonId в ответ на успешный логин
                            string result = $"true {anonId}";
                            lobby.SendMessagePlayer(result, ws, requestId);
                        }
                        else
                        {
                            // Если логин или пароль неверны
                            string result = "false Invalid login or password";
                            lobby.SendMessagePlayer(result, ws, requestId);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error SendMessage command: {e}");
            }
        }



        private async Task Register(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                // SendMessage requestId login password
                List<string> credentials = new List<string>(sqlCommand.Split(' '));

                credentials.RemoveAt(0);

                int requestId = int.Parse(credentials[0]);
                byte[] login = Convert.FromBase64String(credentials[1]);
                byte[] password = Convert.FromBase64String(credentials[2]);

                // Check if the login already exists
                using (var checkCmd = dbConnection.CreateCommand())
                {
                    checkCmd.CommandText = "SELECT COUNT(*) FROM Anon WHERE Login = @login";
                    checkCmd.Parameters.AddWithValue("login", login);

                    int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                    if (count > 0)
                    {
                        // Login already exists
                        lobby.SendMessagePlayer($"User with this login already exists", ws, requestId);
                    }
                    else
                    {
                        // Insert new record
                        using (var insertCmd = dbConnection.CreateCommand())
                        {
                            insertCmd.CommandText = "INSERT INTO Anon (Login, Password) VALUES (@login, @password)";
                            insertCmd.Parameters.AddWithValue("login", login);
                            insertCmd.Parameters.AddWithValue("password", password);

                            await insertCmd.ExecuteNonQueryAsync();

                            // Registration successful
                            lobby.SendMessagePlayer($"true", ws, requestId);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error SendMessage command: {e}");
            }
        }

    }
}
