using System.Text;
using System.Net.WebSockets;
using Npgsql;
using System.Security.Cryptography;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Specialized;
using System.Globalization;

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
                        case string s when s.StartsWith("Login"):
                            await Task.Run(() => ExecuteLogin(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("Registration"):
                            await Task.Run(() => ExecuteRegistration(sqlCommand, senderId, dbConnection, player));
                            break;
                        case string s when s.StartsWith("SendMessage"):
                            await Task.Run(() => SendMessage(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("GetMessage"):
                            await Task.Run(() => GetMessages(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("GenerateUniqueUserId"):
                            await Task.Run(() => GenerateUniqueUserId(sqlCommand, senderId, dbConnection, lobby, webSocket));
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
        private async Task GenerateUniqueUserId(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                List<string> credentials = new List<string>(sqlCommand.Split(' '));
                credentials.RemoveAt(0);

                int requestId = int.Parse(credentials[0]);

                const int max_user_id = int.MaxValue;
                Random random = new Random();

                int idUser;

                do
                {
                    idUser = random.Next(max_user_id);

                    using (var cursor = dbConnection.CreateCommand())
                    {
                        if (cursor.CommandText == $"SELECT COUNT(*) FROM users WHERE idUser = {idUser}")
                        {
                            idUser = -1;
                        }
                    }
                } while (idUser == -1);

                lobby.SendMessagePlayer($"/ans true {idUser}", ws, requestId);

                //SendRegistrationResponse(idUser, "success");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error GenerateUniqueUserId command: {e}");
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
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error RefactorMessage command: {e}");
                }
            }
        }

        // CheckWebSocket requestId
        private async Task CheckWebSocket(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                List<string> credentials = new List<string>(sqlCommand.Split(' '));

                credentials.RemoveAt(0);

                int requestId = int.Parse(credentials[0]);

                if (ws.State == WebSocketState.Open)
                {
                    Console.WriteLine("WebSocket is open");
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
                                idRecepient = reader.GetInt32(1),
                                idMsg = reader.GetInt32(2),
                                timeMsg = reader.GetDateTime(3),
                                msg = reader.GetFieldValue<byte[]>(4),
                                isRead = reader.GetFieldValue<bool>(5)
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

        // GetMessages requestId idSender idRecepient
        private async Task<List<Message>> GetMessages(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
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

                    cursor.CommandText = @"SELECT * FROM chat_users
                            WHERE (idRecepient = @recepientId AND idSender = @idSender)
                               OR (idSender = @idSender AND idRecepient = @idRecepient)
                            ORDER BY timeMsg DESC
                            LIMIT 100;";

                    cursor.Parameters.AddWithValue("idSender", idSender);
                    cursor.Parameters.AddWithValue("idRecepient", idRecepient);

                    using (var reader = await cursor.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Message message = new Message
                            {
                                idSender = reader.GetInt32(0),
                                idRecepient = reader.GetInt32(1),
                                idMsg = reader.GetInt32(2),
                                timeMsg = reader.GetDateTime(3),
                                msg = reader.GetFieldValue<byte[]>(4),
                                isRead = reader.GetFieldValue<bool>(5)
                            };

                            messages.Add(message);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error GetMessages command: {e}");
                }
            }

            return messages;
        }

        // SendMessage requestId idSender idRecepient idMsg timeMsg msg
        private async Task SendMessage(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
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
                    int idMsg = int.Parse(credentials[3]);

                    string time = credentials[4];
                    string format = "yyyy-MM-dd HH:mm:ss.fff";
                    CultureInfo provider = CultureInfo.InvariantCulture;
                    DateTimeOffset timeMsg = DateTimeOffset.ParseExact(time, format, provider);

                    byte[] msg = Encoding.UTF8.GetBytes(credentials[5]);

                    // Добавление параметров в команду для предотвращения SQL-инъекций
                    cursor.CommandText = "INSERT INTO chat_users (idSender, idRecepient, idMsg, timeMsg, msg) VALUES (@idSender, @idRecepient, @idMsg, @timeMsg, @msg)";
                    cursor.Parameters.AddWithValue("id_sender", idSender);
                    cursor.Parameters.AddWithValue("id_recepient", idRecepient);
                    cursor.Parameters.AddWithValue("id_msg", idMsg);
                    cursor.Parameters.AddWithValue("time_msg", timeMsg);
                    cursor.Parameters.AddWithValue("msg", msg);

                    await cursor.ExecuteNonQueryAsync();

                    if (lobby.Players.ContainsKey(ws))
                    {
                        Player receiver = lobby.Players[ws];

                        if (receiver.Id == idRecepient)
                        {
                            await receiver.SendMessageAsync(ws, Encoding.UTF8.GetString(msg));
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error SendMessage command: {e}");
                }
            }
        }      

        private async Task ExecuteLogin(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    // Убираем "Login" из начала SQL-команды
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);
                    string username = credentials[1], password = credentials[2];

                    // Проверка, что пользователь с таким именем существует
                    cursor.CommandText = $"SELECT * FROM users WHERE username='{username}'";
                    using (NpgsqlDataReader reader = cursor.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string storedPassword = reader.GetString(2);
                            string storedSalt = reader.GetString(3);

                            string saltedPassword = password + storedSalt;

                            using (SHA256 sha256 = SHA256.Create())
                            {
                                // Кодируем соленый пароль в байтовую строку перед передачей его объекту хэша
                                byte[] saltedPasswordBytes = Encoding.UTF8.GetBytes(saltedPassword);

                                // Обновляем объект хэша с байтами соленого пароля
                                byte[] hashedPasswordBytes = sha256.ComputeHash(saltedPasswordBytes);

                                // Получаем шестнадцатеричное представление хэша
                                string hashedPassword = BitConverter.ToString(hashedPasswordBytes).Replace("-", "");

                                if (hashedPassword == storedPassword)
                                {
                                    int userId = reader.GetInt32(0);
                                    // Сохраняем id в экземпляре SqlCommander
                                    lobby.Players[ws].Id = userId;
                                    // Вызываем add_player и передаем id
                                    lobby.SendMessageExcept($"Welcome, Player {lobby.Players[ws].Id}", ws);
                                    lobby.SendMessagePlayer($"/ans true", ws, requestId);
                                    SendLoginResponse(senderId, userId, "success");
                                }
                                else
                                {
                                    SendLoginResponse(senderId, -1, "error", "Invalid password");
                                    lobby.SendMessagePlayer($"/ans false", ws, requestId);
                                }
                            }
                        }
                        else
                        {
                            reader.Close();
                            // Пользователь с таким именем не существует
                            SendLoginResponse(senderId, -1, "error", "User not found");
                        }
                    }
                }
                catch (Exception e)
                {
                    SendLoginResponse(senderId, -1, "error", "User not found");
                    Console.WriteLine($"Error executing Login command: {e}");
                }
            }
        }

        private async Task ExecuteRegistration(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Player player)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);
                    string username = credentials[1], password = credentials[2];

                    // Начало транзакции
                    using (var transaction = dbConnection.BeginTransaction())
                    {
                        try
                        {
                            // Проверка, что пользователь с таким именем не существует
                            cursor.CommandText = $"SELECT * FROM users WHERE username='{username}'";
                            using (NpgsqlDataReader reader = cursor.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    reader.Close();
                                    // Генерируем случайную соль
                                    string salt = Guid.NewGuid().ToString("N").Substring(0, 16);

                                    // Добавляем соль к паролю
                                    string saltedPassword = password + salt;

                                    // Создаем объект хэша с использованием алгоритма SHA-256
                                    using (SHA256 sha256 = SHA256.Create())
                                    {
                                        // Кодируем соленый пароль в байтовую строку перед передачей его объекту хэша
                                        byte[] saltedPasswordBytes = Encoding.UTF8.GetBytes(saltedPassword);

                                        // Обновляем объект хэша с байтами соленого пароля
                                        byte[] hashedPasswordBytes = sha256.ComputeHash(saltedPasswordBytes);

                                        // Получаем шестнадцатеричное представление хэша
                                        string hashedPassword = BitConverter.ToString(hashedPasswordBytes).Replace("-", "");

                                        // Регистрация пользователя
                                        Console.WriteLine($"('{username}', '{hashedPassword}', '{salt}')");
                                        cursor.CommandText = $"INSERT INTO users (username, password, salt) VALUES ('{username}', '{hashedPassword}', '{salt}')";
                                        cursor.ExecuteNonQuery();

                                        // Подтверждение изменений
                                        transaction.Commit();

                                        SendRegistrationResponse(senderId, "success");
                                    }
                                }
                                else
                                {
                                    // Пользователь с таким именем уже существует
                                    SendRegistrationResponse(senderId, "error", "Username already exists");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // В случае ошибки откатываем транзакцию
                            transaction.Rollback();
                            Console.WriteLine($"Error executing Registration command: {e}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error executing Registration command: {e}");
                }
            }
        }

        private async Task SendLoginResponse(int senderId, int sqlId, string status, string message = "")
        {
            Console.WriteLine($"{senderId} {sqlId} {status} {message}");
            // Отправка ответа на вход
            // ... (ваш код отправки сообщения)
        }

        private async Task SendRegistrationResponse(int senderId, string status, string message = "")
        {
            Console.WriteLine($"{senderId} {status} {message}");
            // Отправка ответа на регистрацию
            // ... (ваш код отправки сообщения)
        }
    }
}