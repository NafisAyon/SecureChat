using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Collections.Generic;
using Chatting;

public partial class ChatForm : Form
{
    private TcpClient client;
    private TcpListener server;
    private NetworkStream stream;
    private Thread listenThread;

    private readonly CryptoManager crypto = new CryptoManager();
    private bool isSecure = false;
    private string userName;
    private string peerName = "Peer";

    public ChatForm()
    {
        //InitializeComponent();
        SetupUser();
        SetupUI();
        SetupConnection();
    }
    private void SetupUser()
    {
        userName = Microsoft.VisualBasic.Interaction.InputBox("Enter your name:", "Your Name");
        if (string.IsNullOrWhiteSpace(userName))
        {
            MessageBox.Show("Name required");
            this.Close();
        }
    }

    //private RichTextBox chatBox;
    private FlowLayoutPanel chatPanel;
    private TextBox inputBox;
    private Button sendButton;
    private Button secureButton;

    private void SetupUI()
    {
        this.Text = "Secure Chat (C#)";
        this.Width = 600;
        this.Height = 500;

        // Chat Box
        //chatBox = new RichTextBox
        //{
        //    ReadOnly = true,
        //    Dock = DockStyle.Fill,
        //    Font = new Font("Segoe UI", 10),
        //    BackColor = Color.White,
        //    ScrollBars = RichTextBoxScrollBars.Vertical
        //};
        chatPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        // Bottom Panel
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40
        };

        // Input Text Box
        inputBox = new TextBox
        {
            Font = new Font("Segoe UI", 10),
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        // Send Button with icon
        sendButton = new Button
        {
            Text = "📤 Send",
            Width = 80,
            Dock = DockStyle.Right,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        sendButton.Click += (s, e) => SendMessage();

        // Secure Button with icon
        secureButton = new Button
        {
            Text = "🛡️ Secure",
            Width = 90,
            Dock = DockStyle.Right,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        secureButton.Click += (s, e) => InitiateKeyExchange();

        // Order matters: right-most button gets added first
        bottomPanel.Controls.Add(sendButton);
        bottomPanel.Controls.Add(secureButton);
        bottomPanel.Controls.Add(inputBox);

        // Add controls to form
        //this.Controls.Add(chatBox);
        this.Controls.Add(chatPanel);
        this.Controls.Add(bottomPanel);

        // Enter key = send
        inputBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                SendMessage();
                e.SuppressKeyPress = true;
            }
        };
        this.Resize += (s, e) => AdjustBubbleWidths();
    }
    private void AdjustBubbleWidths()
    {
        int availableWidth = chatPanel.ClientSize.Width - 40;

        foreach (Control ctrl in chatPanel.Controls)
        {
            if (ctrl is ChatBubble bubble)
            {
                bubble.MaxWidth = availableWidth;

                // Set bubble.Width so it wraps text correctly
                bubble.Width = availableWidth;

                // Recalculate height based on text wrapping
                int textHeight = TextRenderer.MeasureText(
                    bubble.Message,
                    bubble.Font,
                    new Size(availableWidth - 20, int.MaxValue),
                    TextFormatFlags.WordBreak
                ).Height + 20;

                bubble.Height = textHeight;
            }
        }

        chatPanel.PerformLayout();
    }

    private void SetupConnection()
    {
        string role = Microsoft.VisualBasic.Interaction.InputBox("Host or Connect? (h/c):", "Connection Role");

        if (role.ToLower() == "h")
        {
            StartListener();
        }
        else if (role.ToLower() == "c")
        {
            string ip = Microsoft.VisualBasic.Interaction.InputBox("Enter host IP:", "Connect to Host");
            //peerName = Microsoft.VisualBasic.Interaction.InputBox("Enter peer's name:", "Peer Name");
            StartConnector(ip);
        }
        else
        {
            MessageBox.Show("Invalid role selected.");
            this.Close();
        }
    }
    private void StartListener()
    {
        //peerName = Microsoft.VisualBasic.Interaction.InputBox("Enter peer's name:", "Peer Name");

        server = new TcpListener(IPAddress.Any, 12345);
        server.Start();
        AppendBubble("--- Listening for peer...", true);

        listenThread = new Thread(() =>
        {
            client = server.AcceptTcpClient();
            stream = client.GetStream();
            AppendBubble("--- Peer connected.", true);
            SendName();
            //InitiateKeyExchange();
            new Thread(ReceiveMessages).Start();
        });
        listenThread.Start();
    }
    private void StartConnector(string ip)
    {
        try
        {
            client = new TcpClient();
            client.Connect(IPAddress.Parse(ip), 12345);
            stream = client.GetStream();
            AppendBubble("--- Connected to peer.", true);
            SendName();
            //InitiateKeyExchange();
            new Thread(ReceiveMessages).Start();
        }
        catch
        {
            MessageBox.Show("Failed to connect.");
            this.Close();
        }
    }

    private void SendName()
    {
        var nameMsg = new Dictionary<string, string>
        {
            ["type"] = "name_announcement",
            ["payload"] = userName
        };

        string json = JsonConvert.SerializeObject(nameMsg) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(json);
        stream.Write(data, 0, data.Length);
    }


    private void StartServer()
    {
        server = new TcpListener(IPAddress.Any, 12345);
        server.Start();
        AppendBubble("--- Server started. Waiting for client...", true);

        listenThread = new Thread(() =>
        {
            client = server.AcceptTcpClient();
            stream = client.GetStream();
            AppendBubble("--- Client connected.", true);
            ReceiveMessages();
        });
        listenThread.Start();
    }

    private void StartClient()
    {
        string ip = Microsoft.VisualBasic.Interaction.InputBox("Enter server IP:", "Connect");
        try
        {
            client = new TcpClient();
            client.Connect(IPAddress.Parse(ip), 12345);
            stream = client.GetStream();
            AppendBubble("--- Connected to server.", true);
            new Thread(ReceiveMessages).Start();
        }
        catch
        {
            MessageBox.Show("Failed to connect.");
            this.Close();
        }
    }

    private void SendMessage()
    {
        if (client == null || !client.Connected) return;

        string text = inputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        AppendBubble($"{userName}: {text}", true);
        inputBox.Clear();

        var msg = new Dictionary<string, string>();

        if (isSecure)
        {
            msg["type"] = "secure_msg";
            msg["payload"] = crypto.EncryptMessage(text);
        }
        else
        {
            msg["type"] = "plaintext_msg";
            msg["payload"] = text;
        }

        string json = JsonConvert.SerializeObject(msg) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(json);
        stream.Write(data, 0, data.Length);
    }

    private void ReceiveMessages()
    {
        byte[] buffer = new byte[4096];
        StringBuilder sb = new StringBuilder();

        while (client.Connected)
        {
            try
            {
                int bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes == 0) break;

                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytes));
                while (sb.ToString().Contains("\n"))
                {
                    string line = sb.ToString();
                    int idx = line.IndexOf('\n');
                    string json = line.Substring(0, idx);
                    sb.Remove(0, idx + 1);

                    var msg = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    HandleMessage(msg);
                }
            }
            catch
            {
                AppendBubble("--- Connection lost.", true);
                break;
            }
        }
    }

    private void HandleMessage(Dictionary<string, string> msg)
    {
        string type = msg["type"];
        string payload = msg["payload"];

        switch (type)
        {
            case "plaintext_msg":
                AppendBubble($"{peerName}: {payload}", false);
                break;
            case "secure_msg":
                string decrypted = crypto.DecryptMessage(payload);
                AppendBubble($"{peerName} (Encrypted): {decrypted}", false);
                break;
            case "name_announcement":
                peerName = payload;
                AppendBubble($"--- Connected to {peerName}", true);
                break;
            case "key_exchange_init":
                HandleKeyExchangeInit(payload);
                break;
            case "key_exchange_ack":
                HandleKeyExchangeAck(payload);
                break;
        }
    }

    private void InitiateKeyExchange()
    {
        AppendBubble("--- Initiating key exchange...", true);
        secureButton.Enabled = false;
        secureButton.Text = "Secured 🔒";

        byte[] dhPub = crypto.GetDhPublicKey();
        byte[] sig = crypto.SignData(dhPub);

        var msg = new
        {
            type = "key_exchange_init",
            payload = JsonConvert.SerializeObject(new
            {
                rsa_pub = crypto.ExportPublicRsa(),
                dh_pub = Convert.ToBase64String(dhPub),
                sig = Convert.ToBase64String(sig)
            })
        };

        string json = JsonConvert.SerializeObject(msg) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(json);
        stream.Write(data, 0, data.Length);
    }

    private void HandleKeyExchangeInit(string payloadJson)
    {
        var payload = JsonConvert.DeserializeObject<Dictionary<string, string>>(payloadJson);
        byte[] dhPub = Convert.FromBase64String(payload["dh_pub"]);
        byte[] sig = Convert.FromBase64String(payload["sig"]);

        crypto.ImportPeerRsa(payload["rsa_pub"]);

        if (!crypto.VerifySignature(sig, dhPub))
        {
            AppendBubble("--- Signature verification failed!", true);
            return;
        }

        crypto.GenerateSharedKey(dhPub);
        AppendBubble("--- Shared AES key generated.", true);

        // Send ack
        byte[] myDhPub = crypto.GetDhPublicKey();
        byte[] mySig = crypto.SignData(myDhPub);

        var ack = new
        {
            type = "key_exchange_ack",
            payload = JsonConvert.SerializeObject(new
            {
                rsa_pub = crypto.ExportPublicRsa(),
                dh_pub = Convert.ToBase64String(myDhPub),
                sig = Convert.ToBase64String(mySig)
            })
        };

        stream.Write(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ack) + "\n"));
        isSecure = true;

        if (InvokeRequired)
        {
            this.Invoke(new Action(() =>
            {
                secureButton.Enabled = false;
                secureButton.Text = "Secured 🔒";
            }));
        }
        else
        {
            secureButton.Enabled = false;
            secureButton.Text = "Secured 🔒";
        }

        AppendBubble("--- Secure session established.", true);
    }

    private void HandleKeyExchangeAck(string payloadJson)
    {
        var payload = JsonConvert.DeserializeObject<Dictionary<string, string>>(payloadJson);
        byte[] dhPub = Convert.FromBase64String(payload["dh_pub"]);
        byte[] sig = Convert.FromBase64String(payload["sig"]);

        crypto.ImportPeerRsa(payload["rsa_pub"]);

        if (!crypto.VerifySignature(sig, dhPub))
        {
            AppendBubble("--- ACK signature verification failed!", true);
            return;
        }

        crypto.GenerateSharedKey(dhPub);
        isSecure = true;
        AppendBubble("--- Secure session established.", true);
    }
    private void AppendBubble(string msg, bool isMe)
    {
        if (InvokeRequired)
        {
            this.Invoke(new Action(() => AppendBubble(msg, isMe)));
            return;
        }

        var bubble = new ChatBubble
        {
            Message = msg,
            IsMe = isMe,
            Font = new Font("Segoe UI", 10),
            Padding = new Padding(10),
            Margin = new Padding(isMe ? 50 : 10, 5, isMe ? 10 : 50, 5),
            Width = chatPanel.ClientSize.Width - 40,
            Height = TextRenderer.MeasureText(msg, new Font("Segoe UI", 10),
                new Size(chatPanel.ClientSize.Width - 60, int.MaxValue),
                TextFormatFlags.WordBreak).Height + 20
        };

        chatPanel.Controls.Add(bubble);
        AdjustBubbleWidths(); // make sure width fits
        chatPanel.ScrollControlIntoView(bubble); // Auto scroll to bottom
    }

    //private void AppendText(string msg)
    //{
    //    if (InvokeRequired)
    //    {
    //        this.Invoke(new Action(() => AppendText(msg)));
    //        return;
    //    }
    //    if (msg.StartsWith($"{userName}:") || msg.StartsWith($"{peerName}:"))
    //    {
    //        int colonIndex = msg.IndexOf(':');
    //        if (colonIndex > 0)
    //        {
    //            string name = msg.Substring(0, colonIndex + 1); // "You:" or "Peer:"
    //            string message = msg.Substring(colonIndex + 1); // rest of the message

    //            // Bold name
    //            chatBox.SelectionStart = chatBox.TextLength;
    //            chatBox.SelectionFont = new Font(chatBox.Font, FontStyle.Bold);
    //            chatBox.AppendText(name);

    //            // Normal message
    //            chatBox.SelectionStart = chatBox.TextLength;
    //            chatBox.SelectionFont = new Font(chatBox.Font, FontStyle.Regular);
    //            chatBox.AppendText(message + Environment.NewLine);
    //            return;
    //        }
    //    }

    //    // Default append
    //    chatBox.SelectionStart = chatBox.TextLength;
    //    chatBox.SelectionFont = new Font(chatBox.Font, FontStyle.Regular);
    //    chatBox.AppendText(msg + Environment.NewLine);
    //}
}
