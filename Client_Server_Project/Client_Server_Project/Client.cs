using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Xml.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Client_Server_Project
{
    public partial class Client : Form
    {
        private TcpClient client;
        private NetworkStream stream;

        public Client()
        {
            InitializeComponent();
        }

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            string serverAddress = txbServerAddress.Text;
            if (!int.TryParse(txbPort.Text, out int port))
            {
                MessageBox.Show("Invalid port!", "Error");
                return;
            }

            try
            {
                client = new TcpClient(serverAddress, port);
                stream = client.GetStream();
                MessageBox.Show("Connect to server successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //LoadDrives();
                await RequestDirectories();
                btnConnect.Enabled = true; // Vô hiệu hóa nút kết nối sau khi kết nối
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error connecting to server: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            btnConnect.Enabled = false;
        }

        private async Task RequestDirectories()
        {
            try
            {
                byte[] request = Encoding.ASCII.GetBytes("getDirectories\n");
                await stream.WriteAsync(request, 0, request.Length);
                await stream.FlushAsync();

                string response = await ReceiveDataAsync(stream);

                LoadDrives(response);

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error requesting directories: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<string> ReceiveDataAsync(NetworkStream stream)
        {
            byte[] buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            return Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
        }
        
        private void LoadDrives(string directoryString)
        {
            // Tách chuỗi thành mảng các đường dẫn
            string[] directories = directoryString.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Sử dụng Dictionary để lưu trữ thư mục theo từng đường dẫn
            Dictionary<string, TreeNode> directoryDict = new Dictionary<string, TreeNode>();

            foreach (string directory in directories)
            {
                try
                {
                    string[] parts = directory.Split('\\');

                    TreeNode parentNode = null;
                    string currentPath = "";

                    // Tạo cây thư mục dựa trên đường dẫn
                    foreach (string part in parts)
                    {
                        currentPath = Path.Combine(currentPath, part);

                        if (!directoryDict.ContainsKey(currentPath))
                        {
                            TreeNode newNode = new TreeNode(part);

                            // Lưu đường dẫn đầy đủ vào thuộc tính Tag của nút
                            newNode.Tag = currentPath;

                            if (parentNode != null)
                            {
                                parentNode.Nodes.Add(newNode);
                            }
                            else
                            {
                                treeView1.Nodes.Add(newNode);
                            }
                            directoryDict[currentPath] = newNode;
                        }
                        parentNode = directoryDict[currentPath];
                    }
                }
                catch (Exception ex)
                {
                    // Xử lý lỗi nếu cần thiết
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }
       
        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode node = e.Node;
            if (node.Nodes[0].Text == "" && node.Nodes[0].Tag == null) // Kiểm tra nút giả
            {
                node.Nodes.Clear(); // Xóa nút giả
                LoadNode(node);
            }
        }

        private void LoadNode(TreeNode parentNode)
        {
            DirectoryInfo directoryInfo = (DirectoryInfo)parentNode.Tag;
            try
            {
                DirectoryInfo[] directories = directoryInfo.GetDirectories();
                foreach (DirectoryInfo directory in directories)
                {
                    TreeNode node = new TreeNode(directory.Name);
                    node.Tag = directory;
                    node.Nodes.Add(new TreeNode()); // Thêm một nút giả để hiển thị biểu tượng mở rộng
                    parentNode.Nodes.Add(node);
                }

                FileInfo[] files = directoryInfo.GetFiles();
                foreach (FileInfo file in files)
                {
                    TreeNode node = new TreeNode(file.Name);
                    node.Tag = file;
                    parentNode.Nodes.Add(node);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Bỏ qua các thư mục không thể truy cập
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading directories: " + ex.Message);
            }
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            txtFilePath.Text = e.Node.Tag?.ToString();
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                if (client == null || !client.Connected)
                {
                    MessageBox.Show("You must connect to the server first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var x = client;
                string filePath = txtFilePath.Text.Trim();
                byte[] data = Encoding.ASCII.GetBytes(filePath + "\n");

                // Gửi đường dẫn tới Server
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();

                byte[] buffer = new byte[1024];
                int bytesRead;
                MemoryStream memoryStream = new MemoryStream();

                // Nhận dữ liệu từ Server cho đến khi không còn dữ liệu
                do
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        memoryStream.Write(buffer, 0, bytesRead);
                    }
                } while (stream.DataAvailable);

                byte[] encryptedFileData = memoryStream.ToArray();

                // Giải mã dữ liệu
                //byte[] fileData = Decrypt(encryptedFileData, _privateKey);
                byte[] fileData = encryptedFileData;
                // Hiển thị dữ liệu tùy thuộc vào định dạng của file
                if (CheckImage(filePath))
                {
                    ShowImage(fileData);
                    MessageBox.Show("File content received successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (CheckVideo(filePath))
                {
                    ShowVideo(filePath);
                    MessageBox.Show("File content received successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (CheckText(filePath))
                {
                    ShowText(fileData);
                    MessageBox.Show("File content received successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("The requested file format is incorrect", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        // Kiểm tra định dạng của tệp tin
        private bool CheckImage(string filePath)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".tiff", ".raw", ".bmp", ".svg", ".eps" };
            string extension = Path.GetExtension(filePath);
            return imageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private bool CheckVideo(string filePath)
        {
            string[] videoExtensions = { ".mp4", ".mp3", ".avi", ".mov", ".wmv", ".mkv" };
            string fileExtension = Path.GetExtension(filePath);
            return videoExtensions.Contains(fileExtension.ToLower());
        }

        private bool CheckText(string filePath)
        {
            string[] textExtensions = { ".txt", ".rtf", ".doc", ".docx", ".pdf" };
            string extension = Path.GetExtension(filePath);
            return textExtensions.Contains(extension.ToLower(), StringComparer.OrdinalIgnoreCase);
        }

        // Hiển thị hình ảnh
        private void ShowImage(byte[] imageData)
        {
            try
            {
                txtFileContent.Visible = false;
                videoPlayer.Visible = false;
                pictureBox1.Visible = true;
                using (MemoryStream ms = new MemoryStream(imageData))
                {
                    pictureBox1.Image = Image.FromStream(ms);
                }
                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            }
            catch
            {
                MessageBox.Show("Cannot display image!", "Error");
            }
        }

        // Hiển thị video/ audio
        private void ShowVideo(string videoPath)
        {
            txtFileContent.Visible = false;
            pictureBox1.Visible = false;
            videoPlayer.Visible = true;
            videoPlayer.URL = videoPath;
            videoPlayer.Dock = DockStyle.Fill;
            videoPlayer.Ctlcontrols.play();
        }

        // Hiển thị văn bản
        private void ShowText(byte[] fileData)
        {
            try
            {
                string content = Encoding.UTF8.GetString(fileData);
                txtFileContent.Text = content;
                txtFileContent.Visible = true;
                pictureBox1.Visible = false;
                videoPlayer.Visible = false;
            }
            catch
            {
                MessageBox.Show("Cannot display text!", "Error");
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                string message = "quit\n";      // Thông điệp báo ngắt kết nối
                byte[] data = Encoding.ASCII.GetBytes(message);
                stream.Write(data, 0, data.Length);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message + "\n");
            }
        }
    }
}
