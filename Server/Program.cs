using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace Server
{
    internal class Program
    {
        private static readonly string storagePath = Path.Combine(Directory.GetCurrentDirectory(), "ServerUngDungLuuTru");
        private static readonly string usersFile = "users.txt";
        private static readonly Dictionary<string, string> users = new Dictionary<string, string>();
        private static readonly int MaxConnections = 100;
        private static int currentConnections = 0;

        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            LoadUsers();
            Directory.CreateDirectory(storagePath);

            TcpListener listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();
            Console.WriteLine("Server bắt đầu lắng nghe cổng 5000...");

            while (true)
            {
                try
                {
                    if (currentConnections >= MaxConnections)
                    {
                        Console.WriteLine("Đạt giới hạn kết nối, từ chối client mới.");
                        Thread.Sleep(1000);
                        continue;
                    }
                    TcpClient client = listener.AcceptTcpClient();
                    Interlocked.Increment(ref currentConnections);
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi kết nối với client: {ex.Message}");
                }
            }
        }

        static void LoadUsers()
        {
            if (File.Exists(usersFile))
            {
                foreach (var line in File.ReadAllLines(usersFile))
                {
                    var parts = line.Split(':');
                    if (parts.Length == 2) users[parts[0]] = parts[1];
                }
            }
        }

        static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        static void SaveUser(string username, string password)
        {
            string hashedPassword = HashPassword(password);
            File.AppendAllText(usersFile, $"{username}:{hashedPassword}\n");
            users[username] = hashedPassword;
        }

        static void HandleClient(TcpClient client)
        {
            NetworkStream stream = null;
            string currentUser = null;

            try
            {
                stream = client.GetStream();
                stream.ReadTimeout = 10000;
                byte[] buffer = new byte[8192];
                StringBuilder incomingData = new StringBuilder();

                while (client.Connected)
                {
                    try
                    {
                        if (!stream.DataAvailable)
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            Console.WriteLine("Client đã bị ngắt kết nối.");
                            break;
                        }

                        string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        incomingData.Append(chunk);

                        string data = incomingData.ToString();
                        int newlineIndex;
                        while ((newlineIndex = data.IndexOf('\n')) >= 0)
                        {
                            string request = data.Substring(0, newlineIndex).Trim();
                            data = data.Substring(newlineIndex + 1);
                            incomingData.Clear();
                            incomingData.Append(data);

                            if (string.IsNullOrEmpty(request)) continue;

                            Console.WriteLine($"Đã nhận được yêu cầu: {request} (currentUser: {currentUser ?? "null"})");
                            var parts = request.Split('|');
                            string command = parts[0].ToUpperInvariant();

                            switch (command)
                            {
                                case "LOGIN":
                                    currentUser = HandleLogin(parts, stream);
                                    break;
                                case "REGISTER":
                                    HandleRegister(parts, stream);
                                    break;
                                case "LIST":
                                case "UPLOAD":
                                case "DOWNLOAD":
                                case "CREATE_DIR":
                                case "DELETE":
                                    if (string.IsNullOrEmpty(currentUser))
                                    {
                                        SendResponse(stream, "ERROR| Vui lòng đăng nhập trước");
                                        break;
                                    }
                                    if (parts.Length < 2 || parts[1] != currentUser)
                                    {
                                        SendResponse(stream, "ERROR| Không có quyền truy cập");
                                        break;
                                    }
                                    switch (command)
                                    {
                                        case "LIST":
                                            HandleList(parts, currentUser, stream);
                                            break;
                                        case "UPLOAD":
                                            HandleUpload(parts, currentUser, stream);
                                            break;
                                        case "DOWNLOAD":
                                            HandleDownload(parts, currentUser, stream);
                                            break;
                                        case "CREATE_DIR":
                                            HandleCreateDir(parts, currentUser, stream);
                                            break;
                                        case "DELETE":
                                            HandleDelete(parts, currentUser, stream);
                                            break;
                                    }
                                    break;
                                default:
                                    SendResponse(stream, "ERROR| Lệnh không hợp lệ");
                                    break;
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"IOException trong HandleClient: {ex.Message}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi xử lý yêu cầu: {ex.Message}");
                        SendResponse(stream, $"ERROR| Server lỗi: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong HandleClient: {ex.Message}");
            }
            finally
            {
                stream?.Close();
                client?.Close();
                Interlocked.Decrement(ref currentConnections);
                Console.WriteLine("Client đã đóng kết nối.");
            }
        }

        static void HandleRegister(string[] parts, NetworkStream stream)
        {
            if (parts.Length != 3)
            {
                SendResponse(stream, "ERROR| Định dạng đăng ký không hợp lệ");
                return;
            }

            string username = parts[1];
            string password = parts[2];

            if (string.IsNullOrEmpty(username) || Path.GetInvalidFileNameChars().Any(username.Contains))
            {
                SendResponse(stream, "ERROR| Tên người dùng không hợp lệ");
                return;
            }

            if (users.ContainsKey(username))
            {
                SendResponse(stream, "ERROR| Tài khoản đã tồn tại");
            }
            else
            {
                SaveUser(username, password);
                Directory.CreateDirectory(Path.Combine(storagePath, username));
                SendResponse(stream, "SUCCESS| Đăng ký thành công");
            }
        }

        static string HandleLogin(string[] parts, NetworkStream stream)
        {
            if (parts.Length != 3)
            {
                SendResponse(stream, "ERROR| Định dạng đăng nhập không hợp lệ");
                return null;
            }

            string username = parts[1];
            string password = parts[2];

            if (users.TryGetValue(username, out string storedPassword) && storedPassword == HashPassword(password))
            {
                SendResponse(stream, "SUCCESS| Đăng nhập thành công");
                return username;
            }
            else
            {
                SendResponse(stream, "ERROR| Tên đăng nhập hoặc mật khẩu không hợp lệ");
                return null;
            }
        }

        static void HandleList(string[] parts, string username, NetworkStream stream)
        {
            try
            {
                string targetPath = parts.Length > 2 ? parts[2] : "";
                string userPath = Path.Combine(storagePath, username, targetPath.Replace('/', Path.DirectorySeparatorChar));
                Console.WriteLine($"[HandleList] Listing path: {userPath}");
                if (!Directory.Exists(userPath))
                {
                    SendResponse(stream, "ERROR| Thư mục không tồn tại");
                    return;
                }
                var files = Directory.GetFiles(userPath).Select(f => $"[File]{Path.GetFileName(f)}");
                var dirs = Directory.GetDirectories(userPath).Select(d => $"[Dir]{Path.GetFileName(d)}");
                string result = string.Join("|", files.Concat(dirs));
                SendResponse(stream, $"SUCCESS|{result}");
            }
            catch (Exception ex)
            {
                SendResponse(stream, $"ERROR| Không thể liệt kê các tập tin: {ex.Message}");
            }
        }

        static void HandleUpload(string[] parts, string username, NetworkStream stream)
        {
            if (parts.Length != 4)
            {
                SendResponse(stream, $"ERROR| Định dạng tải lên không hợp lệ: Cần 4 phần, nhận được {parts.Length}");
                return;
            }

            try
            {
                string targetPath = parts[2].Trim();
                string fileData = parts[3];

                // Normalize path: remove extra slashes, trim trailing slash
                targetPath = targetPath.Replace("//", "/").TrimEnd('/');
                Console.WriteLine($"[HandleUpload] Normalized targetPath: {targetPath}");

                // Validate targetPath
                if (string.IsNullOrEmpty(targetPath) || targetPath.Contains("..") || targetPath.Contains("\\") || Path.GetInvalidPathChars().Any(targetPath.Contains))
                {
                    SendResponse(stream, "ERROR| Đường dẫn tệp không hợp lệ");
                    return;
                }

                // Validate filename
                string filename = Path.GetFileName(targetPath);
                Console.WriteLine($"[HandleUpload] Filename: {filename}");
                if (string.IsNullOrEmpty(filename) || Path.GetInvalidFileNameChars().Any(filename.Contains))
                {
                    SendResponse(stream, $"ERROR| Tên tệp không hợp lệ: {filename}");
                    return;
                }

                if (string.IsNullOrEmpty(fileData))
                {
                    SendResponse(stream, "ERROR| Dữ liệu tệp trống");
                    return;
                }

                byte[] data = Convert.FromBase64String(fileData);
                if (data.Length > 100 * 1024 * 1024)
                {
                    SendResponse(stream, "ERROR| Tệp quá lớn");
                    return;
                }

                string userPath = Path.Combine(storagePath, username);
                string fullFilePath = Path.Combine(userPath, targetPath.Replace('/', Path.DirectorySeparatorChar));
                Console.WriteLine($"[HandleUpload] Full file path: {fullFilePath}");

                string directoryPath = Path.GetDirectoryName(fullFilePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    Console.WriteLine($"[HandleUpload] Created directory: {directoryPath}");
                }

                File.WriteAllBytes(fullFilePath, data);
                SendResponse(stream, "SUCCESS| Tệp đã được tải lên");
            }
            catch (FormatException)
            {
                SendResponse(stream, "ERROR| Dữ liệu tệp không hợp lệ");
            }
            catch (Exception ex)
            {
                SendResponse(stream, $"ERROR| Tải lên thất bại: {ex.Message}");
            }
        }

        static void HandleDownload(string[] parts, string username, NetworkStream stream)
        {
            if (parts.Length != 3)
            {
                SendResponse(stream, $"ERROR| Định dạng tải xuống không hợp lệ: Cần 3 phần, nhận được {parts.Length}");
                return;
            }

            try
            {
                string filename = parts[2];
                string filePath = Path.Combine(storagePath, username, filename);

                if (File.Exists(filePath))
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    string base64Data = Convert.ToBase64String(fileData);
                    SendResponse(stream, $"SUCCESS|{base64Data}");
                }
                else
                {
                    SendResponse(stream, "ERROR| Không tìm thấy tập tin");
                }
            }
            catch (Exception ex)
            {
                SendResponse(stream, $"ERROR| Tải xuống thất bại: {ex.Message}");
            }
        }

        static void HandleCreateDir(string[] parts, string username, NetworkStream stream)
        {
            if (parts.Length != 3)
            {
                SendResponse(stream, $"ERROR| Định dạng tạo thư mục không hợp lệ: Cần 3 phần, nhận được {parts.Length}");
                return;
            }

            try
            {
                string dirName = parts[2];
                if (string.IsNullOrEmpty(dirName) || Path.GetInvalidFileNameChars().Any(dirName.Contains))
                {
                    SendResponse(stream, "ERROR| Tên thư mục không hợp lệ");
                    return;
                }

                string dirPath = Path.Combine(storagePath, username, dirName);
                Directory.CreateDirectory(dirPath);
                SendResponse(stream, "SUCCESS| Đã tạo thư mục thành công");
            }
            catch (Exception ex)
            {
                SendResponse(stream, $"ERROR| Tạo thư mục thất bại: {ex.Message}");
            }
        }

        static void HandleDelete(string[] parts, string username, NetworkStream stream)
        {
            if (parts.Length != 3)
            {
                SendResponse(stream, $"ERROR| Định dạng xóa không hợp lệ: Cần 3 phần, nhận được {parts.Length}");
                return;
            }

            try
            {
                string itemName = parts[2];
                string itemPath = Path.Combine(storagePath, username, itemName);

                if (File.Exists(itemPath))
                {
                    File.Delete(itemPath);
                    SendResponse(stream, "SUCCESS| Đã xóa tệp");
                }
                else if (Directory.Exists(itemPath))
                {
                    Directory.Delete(itemPath, true);
                    SendResponse(stream, "SUCCESS| Đã xóa thư mục");
                }
                else
                {
                    SendResponse(stream, "ERROR| Không tìm thấy mục");
                }
            }
            catch (Exception ex)
            {
                SendResponse(stream, $"ERROR| Xóa thất bại: {ex.Message}");
            }
        }

        static void SendResponse(NetworkStream stream, string response)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(response + "\n");
                stream.Write(data, 0, data.Length);
                stream.Flush();
                Console.WriteLine($"Đã gửi phản hồi: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi phản hồi: {ex.Message}");
            }
        }
    }
}