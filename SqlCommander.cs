using System.Text;
using System.Net.WebSockets;
using Npgsql;
using System.Security.Cryptography;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Specialized;

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

        public async Task ExecuteSqlCommand(Lobby lobby, WebSocket webSocket, string sqlCommand, Player player, int id_msg, int id_user, string message)
        {
            Console.WriteLine(sqlCommand);
            // Создание соединения с базой данных
            using (var dbConnection = new NpgsqlConnection($"Host={host};Username={user};Password={password};Database={database};Port={port}"))
            {
                // -------------------------------------------------------------------------------------------------

                // Открытый и закрытые ключи

                RSA rsa = RSA.Create();

                // Экспортируем открытый ключ
                RSAParameters publicKey = rsa.ExportParameters(false);

                // Экспортируем закрытый ключ
                RSAParameters privateKey = rsa.ExportParameters(true);

                // -------------------------------------------------------------------------------------------------

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
                            await Task.Run(() => SendMessage(sqlCommand, senderId, dbConnection, lobby, webSocket, publicKey));
                            break;
                        case string s when s.StartsWith("GetMessage"):
                            await Task.Run(() => GetMessage(sqlCommand, senderId, dbConnection, lobby, webSocket, privateKey));
                            break;
                        case string s when s.StartsWith("GenerateUniqueUserId"):
                            await Task.Run(() => GenerateUniqueUserId(sqlCommand, senderId, dbConnection, lobby, webSocket));
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

        private async Task<int> GenerateUniqueUserId(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            const int MaxUserId = int.MaxValue;
            Random random = new Random();

            int userId;

            do
            {
                userId = random.Next(MaxUserId);

                using (var cursor = dbConnection.CreateCommand())
                {
                    if (cursor.CommandText == $"SELECT COUNT(*) FROM users WHERE id_user = {userId}")
                    {
                        userId = -1;
                    }
                }
            } while (userId == -1);

            return userId;
        }

        private async Task GetMessage(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws, RSAParameters privateKey)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    var commandParts = sqlCommand.Split(' ');
                    int id_msg = int.Parse(commandParts[1]);
                    int id_user = int.Parse(commandParts[2]);

                    cursor.CommandText = $"SELECT msg FROM users WHERE id_msg = {id_msg} AND id_user = {id_user}";

                    byte[] msg = (byte[])await cursor.ExecuteScalarAsync();

                    if (msg != null)
                    {
                        // Расшифровка сообщения
                        RSA rsa = RSA.Create();
                        rsa.ImportParameters(privateKey);

                        byte[] decryptedData = rsa.Decrypt(msg, RSAEncryptionPadding.OaepSHA256);

                        // Преобразуем данные в строку
                        string message = Encoding.UTF8.GetString(decryptedData);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error GetMessage command: {e}");
                }
            }
        }

        private async Task SendMessage(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws, RSAParameters PublicKey, int id_msg = -1, int id_user = -1, string msg = "")
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    cursor.CommandText = $"INSERT INTO users (id_msg, id_user) VALUES ('{id_msg}', '{id_user}')";

                    RSA rsa = RSA.Create();
                    rsa.ImportParameters(PublicKey);

                    // Переводим в двоичный и шифруем данные
                    byte[] data = Encoding.UTF8.GetBytes(msg);
                    byte[] encryptedData = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);

                    cursor.CommandText = $"INSERT INTO users (msg) VALUES ('{encryptedData}')";
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