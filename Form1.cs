using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace ServerChatForm
{
    public partial class Form1 : Form
    {
        private bool isListening = false;
        private Thread listenThread;
        private TcpListener tcpListener;
        private bool stopChatServer = true;
        private Dictionary<string, SslStream> dict = new Dictionary<string, SslStream>();
        public class ClientInfo
        {
            public string Username { get; set; }
            public SslStream Stream { get; set; }
        }
        public Form1()
        {
            InitializeComponent();
        }
        public void Listen()
        {
            try
            {
                tcpListener = new TcpListener(new IPEndPoint(IPAddress.Parse(textBox1.Text), 8080));
                tcpListener.Start();
                while (!stopChatServer)
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    Thread clientThread = new Thread(() =>
                    {
                        HandleClient(client);
                    });
                    clientThread.Start();
                }
            }
            catch (SocketException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public void HandleClient(TcpClient client)
        {
            try
            {
                SslStream sslStream = new SslStream(client.GetStream(), false);
                X509Certificate2 serverCertificate = GetServerCert();
                sslStream.AuthenticateAsServer(serverCertificate, false, SslProtocols.Tls12, true);

                StreamReader sr = new StreamReader(sslStream);
                StreamWriter sw = new StreamWriter(sslStream);
                sw.AutoFlush = true;

                string username = sr.ReadLine();
                if (string.IsNullOrEmpty(username))
                {
                    sw.WriteLine("Please pick a username");
                    client.Close();
                    return;
                }
                else
                {
                    lock (dict)
                    {
                        if (!dict.ContainsKey(username))
                        {
                            var message = JsonConvert.DeserializeObject<MessagePost>(username);
                            dict.Add(message.From_Username, sslStream);
                        }
                        else
                        {
                            MessageBox.Show("Username already exists, pick another one");
                            client.Close();
                            return;
                        }
                    }
                }

                ClientRecv(username, sr, sslStream);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                client.Close();
            }
        }



        public void ClientRecv(string username, StreamReader sr, SslStream ssl)
        {
            try
            {
                while (!stopChatServer)
                {
                    Application.DoEvents();
                    string jsonMessage = sr.ReadLine();
                    var messagePost = JsonConvert.DeserializeObject<MessagePost>(jsonMessage);
                    SslStream friendSsl;
                    if (messagePost.Message.StartsWith("[Image]"))
                    {
                        string base64Image = messagePost.Message.Substring(7);
                        if (dict.TryGetValue(messagePost.To_Username, out friendSsl))
                        {
                            MessagePost message = new MessagePost
                            {
                                From_Username = messagePost.From_Username,
                                To_Username = messagePost.To_Username,
                                Message = messagePost.Message
                            };
                            StreamWriter receiverSw = new StreamWriter(friendSsl);
                            receiverSw.WriteLine(JsonConvert.SerializeObject(message));
                            receiverSw.Flush();
                        }
                        StreamWriter sw2 = new StreamWriter(ssl);
                        sw2.WriteLine(jsonMessage);
                        sw2.AutoFlush = true;
                    }
                    else
                    {
                        if (dict.TryGetValue(messagePost.To_Username, out friendSsl))
                        {
                            StreamWriter receiverSw = new StreamWriter(friendSsl);
                            receiverSw.WriteLine(jsonMessage);
                            receiverSw.Flush();
                        }

                        StreamWriter sw2 = new StreamWriter(ssl);
                        sw2.WriteLine(jsonMessage);
                        sw2.AutoFlush = true;
                    }

                    UpdateChatHistoryThreadSafe(jsonMessage);
                }
            }
            catch (SocketException sockEx)
            {
                sr.Close();
            }
        }



        private X509Certificate2 GetServerCert()
        {
            string certificatePath = @"D:\SSL Certificate\MySslSocketCertificate.cer";
            string certificatePassword = "123";
            X509Certificate2 certificate = new X509Certificate2(certificatePath, certificatePassword);
            return certificate;
        }

        private delegate void SafeCallDelegate(string text);

        private void UpdateChatHistoryThreadSafe(string text)
        {
            if (richTextBox1.InvokeRequired)
            {
                var d = new SafeCallDelegate(UpdateChatHistoryThreadSafe);
                richTextBox1.Invoke(d, new object[] { text });
            }
            else
            {
                var messagePost = JsonConvert.DeserializeObject<MessagePost>(text);
                string formattedMsg = $"[{DateTime.Now:MM/dd/yyyy h:mm tt}] {messagePost.From_Username}: {messagePost.Message}\n";
                richTextBox1.Text += formattedMsg;
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (stopChatServer)
            {
                stopChatServer = false;
                listenThread = new Thread(this.Listen);
                listenThread.Start();
                MessageBox.Show(@"Start listening for incoming connections");
                button1.Text = @"Stop";
            }
            else
            {
                stopChatServer = true;
                button1.Text = @"Start listening";
                tcpListener.Stop();
                listenThread = null;
               
            }
        }
    }
}
