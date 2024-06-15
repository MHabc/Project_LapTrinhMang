using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Project2
{
    public partial class Server : Form
    {
        private TcpListener server;
        private TcpClient client;
        private NetworkStream stream;

        public Server()
        {
            InitializeComponent();
        }

        private void selectButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePathTextBox.Text = openFileDialog.FileName;
            }
        }

        private async void sendButton_Click(object sender, EventArgs e)
        {
            string filename = filePathTextBox.Text;
            if (!File.Exists(filename))
            {
                AppendText("File does not exist.");
                return;
            }

            await Task.Run(() => SendFile(filename));
        }

        private void SendFile(string filename)
        {
            long filesize = new FileInfo(filename).Length;
            DateTime startTime = DateTime.Now;

            try
            {
                if (client == null || !client.Connected)
                {
                    AppendText("No client connected.");
                    return;
                }

                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    // Gửi thông tin tập tin
                    writer.Write(Path.GetFileName(filename));
                    writer.Write(filesize);

                    // Mở tập tin để đọc
                    using (FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        byte[] buffer = new byte[1024];
                        long bytesSent = 0;
                        int bytesRead;

                        while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            stream.Write(buffer, 0, bytesRead);
                            bytesSent += bytesRead;
                            UpdateProgress((int)(bytesSent * 100 / filesize));
                        }
                    }

                    DateTime endTime = DateTime.Now;
                    AppendText($"File {filename} sent successfully. Size: {filesize} bytes. Time: {endTime - startTime}");
                }
            }
            catch (Exception ex)
            {
                AppendText($"Error sending file: {ex.Message}");
            }
        }

        private async void startServerButton_Click(object sender, EventArgs e)
        {
            int port = int.Parse(serverPortTextBox.Text);
            server = new TcpListener(IPAddress.Parse(GetLocalIPv4(NetworkInterfaceType.Wireless80211)), port);
            server.Start();
            AppendText($"Server started on port {port}");

            await Task.Run(() => StartServer());
        }

        private void StartServer()
        {
            while (true)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    stream = client.GetStream();
                    AppendText("Client connected");

                    Task.Run(() => HandleClient(client, stream));
                }
                catch (Exception ex)
                {
                    AppendText($"Error receiving file: {ex.Message}");
                }
            }
        }

        private void HandleClient(TcpClient client, NetworkStream stream)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    while (true)
                    {
                        // Nhận thông tin tập tin
                        string filename = reader.ReadString();
                        long filesize = reader.ReadInt64();

                        // Mở tập tin để ghi
                        using (FileStream fileStream = new FileStream(Path.GetFileName(filename), FileMode.Create, FileAccess.Write))
                        {
                            byte[] buffer = new byte[1024];
                            long bytesReceived = 0;

                            while (bytesReceived < filesize)
                            {
                                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                                if (bytesRead == 0)
                                {
                                    break;
                                }
                                fileStream.Write(buffer, 0, bytesRead);
                                bytesReceived += bytesRead;
                                UpdateProgress((int)(bytesReceived * 100 / filesize));
                            }
                        }

                        AppendText($"File {filename} received successfully. Size: {filesize} bytes.");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendText($"Error receiving file: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        private void AppendText(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendText), text);
                return;
            }
            logTextBox.AppendText(text + Environment.NewLine);
        }

        public string GetLocalIPv4(NetworkInterfaceType _type)
        {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }

        private void UpdateProgress(int progress)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int>(UpdateProgress), progress);
                return;
            }
            progressBar.Value = progress;
        }

        private void Server_Load(object sender, EventArgs e)
        {
            txtServerIp.Text = GetLocalIPv4(NetworkInterfaceType.Wireless80211);
        }

        private async void downloadFileButton_Click(object sender, EventArgs e)
        {
            if (client == null || !client.Connected)
            {
                AppendText("Client is not connected.");
                return;
            }

            string filename = filePathTextBox.Text;
            if (string.IsNullOrEmpty(filename))
            {
                AppendText("No file specified.");
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = Path.GetFileName(filename);
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string savePath = saveFileDialog.FileName;
                await Task.Run(() => ReceiveFile(savePath));
            }
        }

        private void ReceiveFile(string savePath)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Nhận thông tin tập tin
                    string filename = reader.ReadString();
                    long filesize = reader.ReadInt64();

                    // Mở tập tin để ghi
                    using (FileStream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[1024];
                        long bytesReceived = 0;
                        int bytesRead;

                        while (bytesReceived < filesize)
                        {
                            bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead == 0)
                            {
                                break;
                            }
                            fileStream.Write(buffer, 0, bytesRead);
                            bytesReceived += bytesRead;
                            UpdateProgress((int)(bytesReceived * 100 / filesize));
                        }
                    }

                    AppendText($"File {filename} downloaded successfully. Size: {filesize} bytes.");
                }
            }
            catch (Exception ex)
            {
                AppendText($"Error receiving file: {ex.Message}");
            }
        }
    }
}

