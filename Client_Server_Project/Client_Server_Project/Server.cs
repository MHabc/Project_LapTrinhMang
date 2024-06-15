using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace Client_Server_Project
{
    public partial class Server : Form
    {
        private Thread serverThread;
        private Socket listenerSocket;
        private int clientCounter = 0; // Thêm bộ đếm client

        public Server()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            int port = int.Parse(txbPort.Text);
            TcpListener server = new TcpListener(IPAddress.Parse(GetLocalIPv4(NetworkInterfaceType.Wireless80211)), port);
            server.Start();
            serverThread = new Thread(() => StartListen(server, port)); // Truyền port vào StartListen
            serverThread.Start();
            listView1.Items.Add($"Server started on port {port}");
            LoadDrives();
        }

        private void StartListen(TcpListener server, int port)
        {            
            while (true)
            {
                var clientSocket = server.AcceptSocket();
                int clientId = Interlocked.Increment(ref clientCounter);
                Thread thread = new Thread(() => ReceiveData(clientSocket, clientId));
                thread.Start();
            }
        }

        private void ReceiveData(Socket clientSocket, int clientId)
        {
            int bytesReceived = 0;
            byte[] receiveBuffer = new byte[1024];

            if (listView1.InvokeRequired)
            {
                listView1.Invoke((MethodInvoker)delegate
                {
                    listView1.Items.Add($"Connected from Client {clientId}");
                });
            }

            while (clientSocket.Connected)
            {
                try
                {
                    string text = "";
                    do
                    {
                        bytesReceived = clientSocket.Receive(receiveBuffer);
                        text += Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);
                    } while (!text.EndsWith("\n"));

                    text = text.Trim();

                    if (text == "getDirectories")
                    {
                        SendDirectories(clientSocket, "D:\\Picture");
                        continue;
                    }

                    if (text == "quit")
                    {
                        if (listView1.InvokeRequired)
                        {
                            listView1.Invoke((MethodInvoker)delegate
                            {
                                listView1.Items.Add($"Client {clientId} quit!");
                            });
                        }
                        break;
                    }

                    if (listView1.InvokeRequired)
                    {
                        listView1.Invoke((MethodInvoker)delegate
                        {
                            listView1.Items.Add($"Receive from Client {clientId}: {text}");
                        });
                    }

                    string filePath = text.Trim();
                    byte[] fileData;

                    if (File.Exists(filePath))
                    {
                        fileData = File.ReadAllBytes(filePath);
                        //fileData = Encrypt(fileData);
                    }
                    else
                    {
                        string errorMsg = "File not found.";
                        fileData = Encoding.ASCII.GetBytes(errorMsg);
                    }

                    clientSocket.Send(fileData);

                    if (listView1.InvokeRequired)
                    {
                        listView1.Invoke((MethodInvoker)delegate
                        {
                            listView1.Items.Add($"Sent to Client {clientId}: {fileData.Length} bytes");
                        });
                    }
                }
                catch (SocketException ex)
                {
                    if (listView1.InvokeRequired)
                    {
                        listView1.Invoke((MethodInvoker)delegate
                        {
                            listView1.Items.Add($"Client {clientId}: Error Socket: {ex.Message}");
                        });
                    }
                    clientSocket.Close();
                    break;
                }
                catch (Exception ex)
                {
                    if (listView1.InvokeRequired)
                    {
                        listView1.Invoke((MethodInvoker)delegate
                        {
                            listView1.Items.Add($"Client {clientId}: Error: {ex.Message}");
                        });
                    }
                    break;
                }
            }
            clientSocket.Close();
        }

        private void SendDirectories(Socket clientSocket, string directory)
        {
            string str = "";
           
            
                // Gửi tất cả các thư mục trong thư mục hiện tại
                string[] directories = Directory.GetDirectories(directory);
                foreach (string dir in directories)
                {
                    str += dir + "\n"; // Dấu 'D' đánh dấu là thư mục
                    SendDirectories(clientSocket, dir); // Đệ quy để gửi thư mục con và tập tin
                }

                // Gửi tất cả các tập tin trong thư mục hiện tại
                string[] files = Directory.GetFiles(directory);
                foreach (string file in files)
                {
                    str += file + "\n"; // Dấu 'F' đánh dấu là tập tin
                }
                byte[] data = Encoding.ASCII.GetBytes(str);
                clientSocket.Send(data);    
        }

        private void LoadDrives()
        {
            treeView1.Nodes.Clear();
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                TreeNode node = new TreeNode(drive.Name);
                node.Tag = drive.RootDirectory;
                node.Nodes.Add(new TreeNode()); // Thêm một nút giả để hiển thị biểu tượng mở rộng
                treeView1.Nodes.Add(node);
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

        private void Server_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(0);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void Server_Load(object sender, EventArgs e)
        {
            {
                txbServerAddress.Text = GetLocalIPv4(NetworkInterfaceType.Wireless80211);
            }
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
    }
}
