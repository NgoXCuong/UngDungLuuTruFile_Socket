using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    internal class Program
    {
        private static readonly string storagePath = Path.Combine(Directory.GetCurrentDirectory(), "ServerStorage");
        private static readonly string usersFile = "users.txt";
        private static Dictionary<string, string> users = new Dictionary<string, string>(); // username -> password
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
                    TcpClient client = listener.AcceptTcpClient();
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

        static void SaveUser(string username, string password)
        {
            File.AppendAllText(usersFile, $"{username}:{password}\n");
            users[username] = password;
        }

        static void HandleClient(TcpClient client)
        {
            NetworkStream stream = null;
            string currentUser = null;

            try
            {
                stream = client.GetStream();
                byte[] buffer = new byte[4096]; // Tăng kích thước buffer

                while (client.Connected)
                {
                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            Console.WriteLine("Client đã bị ngắt kết nối.");
                            break;
                        }

                        string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"Đã nhận được yêu cầu: {request}");
                        var parts = request.Split('|');
                        string command = parts[0];

                        switch (command)
                        {
                            case "LOGIN":
                                currentUser = HandleLogin(parts, stream);
                                Console.WriteLine($"currentUser sau đăng nhập: {currentUser}");
                                break;
                            case "LIST":
                                if (parts.Length < 2)
                                {
                                    SendResponse(stream, "ERROR| Định dạng LIST không hợp lệ: Thiếu username");
                                    break;
                                }
                                string listUser = parts[1];
                                if (string.IsNullOrEmpty(listUser))
                                {
                                    SendResponse(stream, "ERROR| Username không hợp lệ");
                                    break;
                                }
                                if (users.ContainsKey(listUser))
                                {
                                    HandleList(listUser, stream);
                                }
                                else
                                {
                                    SendResponse(stream, "ERROR| Người dùng không tồn tại hoặc chưa đăng nhập");
                                }
                                break;
                            case "UPLOAD":
                                if (parts.Length < 4)
                                {
                                    SendResponse(stream, "ERROR| Định dạng UPLOAD không hợp lệ");
                                    break;
                                }
                                string uploadUser = parts[1];
                                if (users.ContainsKey(uploadUser))
                                {
                                    HandleUpload(parts, uploadUser, stream);
                                }
                                else
                                {
                                    SendResponse(stream, "ERROR| Người dùng không tồn tại hoặc chưa đăng nhập");
                                }
                                break;
                            case "DOWNLOAD":
                                if (parts.Length < 3)
                                {
                                    SendResponse(stream, "ERROR| Định dạng DOWNLOAD không hợp lệ");
                                    break;
                                }
                                string downloadUser = parts[1];
                                if (users.ContainsKey(downloadUser))
                                {
                                    HandleDownload(parts, downloadUser, stream);
                                }
                                else
                                {
                                    SendResponse(stream, "ERROR| Người dùng không tồn tại hoặc chưa đăng nhập");
                                }
                                break;
                            case "CREATE_DIR":
                                if (parts.Length < 3)
                                {
                                    SendResponse(stream, "ERROR| Định dạng CREATE_DIR không hợp lệ");
                                    break;
                                }
                                string createDirUser = parts[1];
                                if (users.ContainsKey(createDirUser))
                                {
                                    HandleCreateDir(parts, createDirUser, stream);
                                }
                                else
                                {
                                    SendResponse(stream, "ERROR| Người dùng không tồn tại hoặc chưa đăng nhập");
                                }
                                break;
                            case "DELETE":
                                if (parts.Length < 3)
                                {
                                    SendResponse(stream, "ERROR| Định dạng DELETE không hợp lệ");
                                    break;
                                }
                                string deleteUser = parts[1];
                                if (users.ContainsKey(deleteUser))
                                {
                                    HandleDelete(parts, deleteUser, stream);
                                }
                                else
                                {
                                    SendResponse(stream, "ERROR| Người dùng không tồn tại hoặc chưa đăng nhập");
                                }
                                break;
                            default:
                                SendResponse(stream, "ERROR| Lệnh không hợp lệ");
                                break;
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

            if (users.TryGetValue(username, out string storedPassword) && storedPassword == password)
            {
                SendResponse(stream, "SUCCESS| Đăng nhập thành công");
                Console.WriteLine($"Đăng nhập thành công: {username}"); // Log để debug
                return username;
            }
            else
            {
                SendResponse(stream, "ERROR| Tên đăng nhập hoặc mật khẩu không hợp lệ");
                return null;
            }
        }

        static void HandleList(string username, NetworkStream stream)
        {
            try
            {
                string userPath = Path.Combine(storagePath, username);
                var files = Directory.GetFiles(userPath).Select(Path.GetFileName);
                var dirs = Directory.GetDirectories(userPath).Select(Path.GetFileName);
                string result = string.Join("|", files.Concat(dirs));
                SendResponse(stream, $"SUCCESS| {result}");
            }
            catch (Exception ex)
            {
                SendResponse(stream, $"ERROR| Không thể liệt kê các tập tin: {ex.Message}");
            }
        }

        static void HandleUpload(string[] parts, string username, NetworkStream stream)
        {
            if (parts.Length != 3)
            {
                SendResponse(stream, "ERROR| Định dạng tải lên không hợp lệ");
                return;
            }

            try
            {
                string filename = parts[1];
                string fileData = parts[2];
                string filePath = Path.Combine(storagePath, username, filename);

                File.WriteAllBytes(filePath, Convert.FromBase64String(fileData));
                SendResponse(stream, "SUCCESS| Tệp đã được tải lên");
            }
            catch (Exception ex)
            {
                SendResponse(stream, $"ERROR| Tải lên thất bại: {ex.Message}");
            }
        }

        static void HandleDownload(string[] parts, string username, NetworkStream stream)
        {
            if (parts.Length != 2)
            {
                SendResponse(stream, "ERROR| Định dạng tải xuống không hợp lệ");
                return;
            }

            try
            {
                string filename = parts[1];
                string filePath = Path.Combine(storagePath, username, filename);

                if (File.Exists(filePath))
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    string base64Data = Convert.ToBase64String(fileData);
                    SendResponse(stream, $"SUCCESS| {base64Data}");
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
            if (parts.Length != 2)
            {
                SendResponse(stream, "ERROR| Định dạng thư mục tạo không hợp lệ");
                return;
            }

            try
            {
                string dirName = parts[1];
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
            if (parts.Length != 2)
            {
                SendResponse(stream, "ERROR| Định dạng xóa không hợp lệ");
                return;
            }

            try
            {
                string itemName = parts[1];
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
                byte[] data = Encoding.UTF8.GetBytes(response);
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

