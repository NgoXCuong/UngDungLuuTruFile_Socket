using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class MainForm : Form
    {
        private TcpClient client;
        private NetworkStream stream;

        private string currentDirectory = "";

        public string CurrentUser { get; set; }

        public MainForm()
        {
            InitializeComponent();
        }

        class FileItem
        {
            public string Name { get; set; }
            public bool IsDirectory { get; set; }

            public override string ToString()
            {
                return IsDirectory ? $"[Dir]{Name}" : $"[File]{Name}";
            }
        }

        private async Task RefreshFileListAsync()
        {
            try
            {
                btnDelete.Enabled = false;
                btnDownload.Enabled = false;
                btnUpload.Enabled = true;

                listFoder.Items.Clear();

                // Construct and send LIST command based on current directory
                string listCommand = string.IsNullOrEmpty(currentDirectory) ?
                    $"LIST|{CurrentUser}" : $"LIST|{CurrentUser}|{currentDirectory}";
                Console.WriteLine($"[RefreshFileList] Đang gửi: {listCommand}");
                await SendRequestAsync(listCommand);
                string response = await ReceiveResponseAsync();
                Console.WriteLine($"[RefreshFileList] Nhận được response: {response}");

                if (response.StartsWith("SUCCESS"))
                {
                    // Tách response thành các phần, bỏ qua phần "SUCCESS"
                    var responseParts = response.Split('|');
                    Console.WriteLine($"[RefreshFileList] Số phần trong response: {responseParts.Length}");

                    // Bắt đầu từ index 1 để bỏ qua "SUCCESS"
                    for (int i = 1; i < responseParts.Length; i++)
                    {
                        string item = responseParts[i].Trim();
                        Console.WriteLine($"[RefreshFileList] Xử lý item: '{item}'");

                        if (string.IsNullOrEmpty(item)) continue;

                        if (item.StartsWith("[Dir]"))
                        {
                            string name = item.Substring(5); // Bỏ "[Dir]"
                            if (!string.IsNullOrEmpty(name))
                            {
                                listFoder.Items.Add(new FileItem { Name = name, IsDirectory = true });
                                Console.WriteLine($"[RefreshFileList] Thêm thư mục: {name}");
                            }
                        }
                        else if (item.StartsWith("[File]"))
                        {
                            string name = item.Substring(6); // Bỏ "[File]"
                            if (!string.IsNullOrEmpty(name))
                            {
                                listFoder.Items.Add(new FileItem { Name = name, IsDirectory = false });
                                Console.WriteLine($"[RefreshFileList] Thêm file: {name}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[RefreshFileList] Item không hợp lệ: {item}");
                        }
                    }
                }
                else
                {
                    MessageBox.Show(response, "Lỗi danh sách tệp", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                UpdateCurrentPathLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi làm mới danh sách tệp: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
            }
        }

        private void UpdateCurrentPathLabel()
        {
            // Update label or use MessageBox if lblCurrentPath is not added
            string pathDisplay = string.IsNullOrEmpty(currentDirectory) ? "Root" : currentDirectory + "/";
            if (lblCurrentPath != null)
            {
                lblCurrentPath.Text = $"Thư mục hiện tại: {pathDisplay}";
            }
            Console.WriteLine($"[UI] Thư mục hiện tại: {pathDisplay}");
        }

        public void SetConnection(TcpClient tcpClient, NetworkStream networkStream)
        {
            client = tcpClient;
            stream = networkStream;
        }

        private async Task SendRequestAsync(string request)
        {
            try
            {
                if (client == null || !client.Connected || stream == null)
                {
                    throw new Exception("Không có kết nối đến server. Vui lòng đăng nhập lại.");
                }

                request = request.Trim();
                byte[] data = Encoding.UTF8.GetBytes(request + "\n");
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                Console.WriteLine($"[SendRequest] Gửi: {request}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendRequest] Lỗi: {ex.Message}");
                throw;
            }
        }

        private async Task<string> ReceiveResponseAsync()
        {
            try
            {
                byte[] buffer = new byte[8192];
                StringBuilder responseBuilder = new StringBuilder();
                int bytesRead;

                do
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        throw new IOException("Server đã đóng kết nối.");

                    string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    responseBuilder.Append(chunk);

                    if (chunk.Contains("\n"))
                        break;
                } while (bytesRead > 0);

                string response = responseBuilder.ToString().Trim();
                Console.WriteLine($"[ReceiveResponse] Đã nhận: {response}");
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReceiveResponse] Lỗi: {ex.Message}");
                throw;
            }
        }

        private void Disconnect()
        {
            try
            {
                stream?.Close();
                client?.Close();
                client = null;
                stream = null;
                Console.WriteLine("[Disconnect] Đã đóng kêt nối.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Disconnect] Lỗi: {ex.Message}");
            }
        }

        private void RedirectToLogin()
        {
            MessageBox.Show("Mất kết nối với server. Vui lòng đăng nhập lại.", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Disconnect();
            using (var loginForm = new LoginForm())
            {
                this.Hide();
                loginForm.ShowDialog();
                this.Close();
            }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentUser))
            {
                MessageBox.Show("Không thể tải danh sách tệp: Người dùng chưa đăng nhập.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
                return;
            }
            btnDelete.Enabled = false;
            btnDownload.Enabled = false;
            btnUpload.Enabled = true;

            await RefreshFileListAsync();
        }

        private async void btnUpload_Click(object sender, EventArgs e)
        {
            try
            {
                // Kiểm tra thư mục được chọn
                string targetDirectory = currentDirectory;
                if (listFoder.SelectedItem != null)
                {
                    FileItem selectedItem = listFoder.SelectedItem as FileItem;
                    if (selectedItem != null && selectedItem.IsDirectory)
                    {
                        targetDirectory = string.IsNullOrEmpty(currentDirectory) ?
                            selectedItem.Name : $"{currentDirectory}/{selectedItem.Name}";
                    }
                }

                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        string filename = Path.GetFileName(ofd.FileName);
                        if (string.IsNullOrEmpty(filename) || Path.GetInvalidFileNameChars().Any(filename.Contains))
                        {
                            MessageBox.Show($"Tên tệp chứa ký tự không hợp lệ: {filename}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        byte[] fileData;
                        try
                        {
                            fileData = File.ReadAllBytes(ofd.FileName);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Không thể đọc tệp: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        if (fileData.Length == 0)
                        {
                            MessageBox.Show("Tệp rỗng, không thể tải lên.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        if (fileData.Length > 100 * 1024 * 1024)
                        {
                            MessageBox.Show("Tệp quá lớn (giới hạn 100MB).", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        string base64Data = Convert.ToBase64String(fileData);
                        Console.WriteLine($"[Upload] File: {filename}, Size: {fileData.Length} bytes, Base64: {base64Data}, Target directory: {(string.IsNullOrEmpty(targetDirectory) ? "Root" : targetDirectory)}");

                        // Construct and normalize target path
                        string targetPath = string.IsNullOrEmpty(targetDirectory) ?
                            filename : $"{targetDirectory}/{filename}";
                        targetPath = targetPath.Replace("\\", "/").TrimEnd('/').Trim();
                        Console.WriteLine($"[Upload] Đường dẫn đích: {targetPath}");

                        // Show target directory in UI
                        string pathDisplay = string.IsNullOrEmpty(targetDirectory) ? "Root" : targetDirectory + "/";
                        MessageBox.Show($"Đang tải lên vào: {pathDisplay}", "Thông tin", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        string request = $"UPLOAD|{CurrentUser}|{targetPath}|{base64Data}";
                        await SendRequestAsync(request);
                        string response = await ReceiveResponseAsync();

                        MessageBox.Show(response, "Kết quả tải lên", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                        await RefreshFileListAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải lên: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
            }
        }

        private async void btnDownload_Click(object sender, EventArgs e)
        {
            if (listFoder.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một tệp để tải xuống.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FileItem selectedItem = listFoder.SelectedItem as FileItem;
            if (selectedItem == null || selectedItem.IsDirectory)
            {
                MessageBox.Show("Chỉ có thể tải xuống tệp, không phải thư mục.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string filename = string.IsNullOrEmpty(currentDirectory) ?
                    selectedItem.Name : $"{currentDirectory}/{selectedItem.Name}";

                //string filename = selectedItem.Name;
                await SendRequestAsync($"DOWNLOAD|{CurrentUser}|{filename}");
                string response = await ReceiveResponseAsync();

                if (response.StartsWith("SUCCESS"))
                {
                    string base64Data = response.Split('|')[1];
                    byte[] fileData = Convert.FromBase64String(base64Data);

                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.FileName = filename;
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            File.WriteAllBytes(sfd.FileName, fileData);
                            MessageBox.Show("Tệp đã được tải xuống thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                else
                {
                    MessageBox.Show(response, "Tải xuống thất bại", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải xuống: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
            }
        }

        //private async void btnDownload_Click(object sender, EventArgs e)
        //{
        //    if (listFoder.SelectedItem == null)
        //    {
        //        MessageBox.Show("Vui lòng chọn một tệp để tải xuống.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //        return;
        //    }

        //    FileItem selectedItem = listFoder.SelectedItem as FileItem;
        //    if (selectedItem == null || selectedItem.IsDirectory)
        //    {
        //        MessageBox.Show("Chỉ có thể tải xuống tệp, không phải thư mục.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //        return;
        //    }

        //    try
        //    {
        //        // Use only the filename, assuming server handles directory structure
        //        string filename = selectedItem.Name;
        //        await SendRequestAsync($"DOWNLOAD|{CurrentUser}|{filename}");
        //        string response = await ReceiveResponseAsync();

        //        if (response.StartsWith("SUCCESS"))
        //        {
        //            string[] parts = response.Split('|');
        //            string base64Data = parts[1];
        //            Console.WriteLine($"Received Base64 length: {base64Data.Length}");
        //            byte[] fileData = Convert.FromBase64String(base64Data);

        //            using (SaveFileDialog sfd = new SaveFileDialog())
        //            {
        //                sfd.FileName = Path.GetFileName(filename);
        //                sfd.Filter = "All Files (*.*)|*.*";
        //                sfd.DefaultExt = Path.GetExtension(filename).TrimStart('.');
        //                if (sfd.ShowDialog() == DialogResult.OK)
        //                {
        //                    File.WriteAllBytes(sfd.FileName, fileData);
        //                    MessageBox.Show("Tệp đã được tải xuống thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //                }
        //            }
        //        }
        //        else
        //        {
        //            MessageBox.Show(response, "Tải xuống thất bại", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Lỗi khi tải xuống: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        RedirectToLogin();
        //    }
        //}
        private async void btnCreate_Click(object sender, EventArgs e)
        {
            try
            {
                string dirName = Prompt.ShowDialog("Nhập tên thư mục:", "Tạo thư mục");
                if (!string.IsNullOrEmpty(dirName))
                {
                    if (Path.GetInvalidFileNameChars().Any(dirName.Contains))
                    {
                        MessageBox.Show("Tên thư mục chứa ký tự không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    string targetDir = string.IsNullOrEmpty(currentDirectory) ?
                        dirName : $"{currentDirectory}/{dirName}";
                    await SendRequestAsync($"CREATE_DIR|{CurrentUser}|{targetDir}");

                    //await SendRequestAsync($"CREATE_DIR|{CurrentUser}|{dirName}");
                    string response = await ReceiveResponseAsync();
                    MessageBox.Show(response, "Kết quả tạo thư mục", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                    await RefreshFileListAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tạo thư mục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
            }
        }

        private async void btnDelete_Click(object sender, EventArgs e)
        {
            if (listFoder.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một mục để xóa.", "Lỗi lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FileItem selectedItem = listFoder.SelectedItem as FileItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Dữ liệu chọn không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string targetItem = string.IsNullOrEmpty(currentDirectory) ?
                    selectedItem.Name : $"{currentDirectory}/{selectedItem.Name}";
                await SendRequestAsync($"DELETE|{CurrentUser}|{targetItem}");

                string response = await ReceiveResponseAsync();
                MessageBox.Show(response, "Kết quả xóa", MessageBoxButtons.OK, response.StartsWith("SUCCESS") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                await RefreshFileListAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xóa: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RedirectToLogin();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();
        }

        private void listFoder_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool hasSelection = listFoder.SelectedItem != null;
            btnDelete.Enabled = hasSelection;
            btnDownload.Enabled = hasSelection && (listFoder.SelectedItem as FileItem)?.IsDirectory == false;
            btnUpload.Enabled = true;
        }

        private async void listFoder_DoubleClick(object sender, EventArgs e)
        {
            if (listFoder.SelectedItem != null)
            {
                FileItem selectedItem = listFoder.SelectedItem as FileItem;
                if (selectedItem != null && selectedItem.IsDirectory)
                {
                    // Navigate into the selected directory
                    currentDirectory = string.IsNullOrEmpty(currentDirectory) ?
                        selectedItem.Name : $"{currentDirectory}/{selectedItem.Name}";
                    await RefreshFileListAsync();
                }
            }
        }

        // Quay lại thư mục cha trước đó
        private async void btnBack_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                currentDirectory = Path.GetDirectoryName(currentDirectory)?.Replace(Path.DirectorySeparatorChar, '/') ?? "";
                await RefreshFileListAsync();
            }
        }
    }
}