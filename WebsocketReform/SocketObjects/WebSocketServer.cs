using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using WebsocketReform.Objects;

namespace WebsocketReform.SocketObjects
{
    public class Logger
    {
        public bool LogEvents { get; set; }

        public Logger()
        {
            LogEvents = true;
        }

        public void Log(string Text)
        {
            if (LogEvents) Console.WriteLine(Text);
        }
    }

    public enum ServerStatusLevel { Off, WaitingConnection, ConnectionEstablished };

    public delegate void NewConnectionEventHandler(SocketConnection sender, SocketConnection.NewConnectionEventArgs e);
    public delegate void DataReceivedEventHandler(SocketConnection sender, string message, EventArgs e);
    public delegate void DisconnectedEventHandler(SocketConnection sender, EventArgs e);
    public delegate void BroadcastEventHandler(string message, EventArgs e);

    public class WebSocketServer : IDisposable
    {
        #region MyRegion
        private bool _alreadyDisposed;
        private Socket _listener;
        private int _connectionsQueueLength;
        private int _maxBufferSize;
        private string _handshake;
        private StreamReader _connectionReader;
        private StreamWriter _connectionWriter;
        private Logger _logger;
        private byte[] _firstByte;
        private byte[] _lastByte;
        private byte[] _serverKey1;
        private byte[] _serverKey2;


        public ServerStatusLevel Status { get; private set; }
        public int ServerPort { get; set; }
        public string ServerLocation { get; set; }
        public string ConnectionOrigin { get; set; }
        public bool LogEvents
        {
            get { return _logger.LogEvents; }
            set { _logger.LogEvents = value; }
        }

        public Dictionary<string, Timer> ClassOwnerReconnectMonitors { get; set; }

        public Socket Listener
        {
            get { return _listener; }
            set { _listener = value; }
        }

        private void Initialize()
        {
            _alreadyDisposed = false;
            _logger = new Logger();
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            Status = ServerStatusLevel.Off;
            _connectionsQueueLength = 500;
            _maxBufferSize = 1024 * 100;
            _firstByte = new byte[_maxBufferSize];
            _lastByte = new byte[_maxBufferSize];
            _firstByte[0] = 0x00;
            _lastByte[0] = 0xFF;
            _logger.LogEvents = true;
            _listener.Bind(new IPEndPoint(getLocalmachineIPAddress(), ServerPort));

            ClassOwnerReconnectMonitors = new Dictionary<string, Timer>();
        }

        public WebSocketServer(string address = "chat", int port = 4141)
        {
            ServerPort = port;
            ServerLocation = $"ws://{getLocalmachineIPAddress()}:{port}/{address}";
            Initialize();
        }

        public WebSocketServer(int serverPort, string serverLocation, string connectionOrigin)
        {
            ServerPort = serverPort;
            ConnectionOrigin = connectionOrigin;
            ServerLocation = serverLocation;
            Initialize();
        }


        ~WebSocketServer()
        {
            Close();
        }


        public void Dispose()
        {
            Close();
        }

        private void Close()
        {
            if (!_alreadyDisposed)
            {
                _alreadyDisposed = true;
                if (_listener != null) _listener.Close();
                //清除（关闭）所有连接
                //UserInfo.clearAll();

                GC.SuppressFinalize(this);
            }
        }

        public static IPAddress getLocalmachineIPAddress()
        {
            string strHostName = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);
            //
            //return ipEntry.AddressList[5];
            //
            foreach (IPAddress ip in ipEntry.AddressList)
            {

                //IPV4
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip;
            }

            return ipEntry.AddressList[0];
        } 
        #endregion

        public ChatRoom ChatRoom { get; } = new ChatRoom();

        public void StartServer()
        {
            Char char1 = Convert.ToChar(65533);
            _listener.Listen(_connectionsQueueLength);
            _logger.Log(string.Format("聊天服务器启动。监听地址：{0}, 端口：{1}", getLocalmachineIPAddress(), ServerPort));
            _logger.Log(string.Format("WebSocket服务器地址: ws://{0}:{1}/chat", getLocalmachineIPAddress(), ServerPort));
            while (true)
            {
                Socket sc = _listener.Accept();

                if (sc != null)
                {
                    //System.Threading.Thread.Sleep(100);
                    SocketConnection socketConn = new SocketConnection(sc);
                    socketConn.NewConnection += new NewConnectionEventHandler(OnNewConnection);
                    socketConn.DataReceived += new DataReceivedEventHandler(OnDataReceived);
                    socketConn.Disconnected += new DisconnectedEventHandler(OnDisconnected);

                    socketConn.Socket.BeginReceive(socketConn.receivedDataBuffer,
                                                             0, socketConn.receivedDataBuffer.Length,
                                                             0, new AsyncCallback(socketConn.ManageHandshake),
                                                             socketConn.Socket.Available);
                }
            }
        }

        public void OnDisconnected(SocketConnection sender, EventArgs e)
        {
            Debug.WriteLine("a connection closed");
            ChatRoom.OnDisconnected(sender,e);
            //SocketConnection sConn = sender as SocketConnection;
            //if(sConn != null && !sConn.User.HasClass)
            //{
            //    UserList.DeleteUserByID(sConn.User.Id);
            //}
            //else if (sConn != null && sConn.User.HasClass)
            //{
            //    //提示管理员离开
            //    string messageToSend = string.Format("C;{0};;ClassOwnerLeft", sConn.User.Id);
            //    MsgSender.SendMessage(sConn.User.Class, messageToSend, this);
            //    Timer timer = new Timer(15 * 60 * 1000);
            //    timer.AutoReset = false;
            //    timer.Elapsed += (o, args) => UserList.DeleteUserByID(sConn.User.Id);
            //    try
            //    {
            //        ClassOwnerReconnectMonitors.Add(
            //        sConn.User.Id, timer);
            //        timer.Start();
            //    }
            //    catch(Exception ex)
            //    {
            //        ClassOwnerReconnectMonitors.Remove(sConn.User.Id);
            //        ClassOwnerReconnectMonitors.Add(
            //        sConn.User.Id, timer);
            //        timer.Start();
            //        Debug.WriteLine("重新启动计时!");
            //    }
            //}
            //else
            //{
            //    Console.WriteLine("连接异常关闭!");
            //}
        }

        public void OnDataReceived(SocketConnection sender, string message, EventArgs e)
        {
            Debug.WriteLine("a new message");
            ChatRoom.OnDataReceived(sender,message,e);
            ////第一个用户登陆，初始化默认区域和默认教室
            //if (ClassList.ClassIDHashTab.Count == 0 && DomainList.DomainIDHashTab.Count == 0)
            //{
            //    DomainList.DomainIDHashTab.Add("default", new Domain() { Id = "default" });
            //}
            //SocketConnection sConn = sender as SocketConnection;
            ////用户第一次连接，用户与socket绑定
            //if (sConn != null && sConn.User == null)
            //{
            //    User user = new User();
            //    user.Socket = sConn;
            //    sConn.User = user;
            //    user.Socket.Server = this;
            //}
            //object target;
            //string messageToOthers;
            //string messageToUser;
            //MsgDealer.PrepareMessageToSend(sConn.User, message, this, out target, out messageToOthers,out messageToUser);
            //MsgSender.SendMessage(sConn.User, messageToUser, this);
            //MsgSender.SendMessage(target, messageToOthers, this);
        }

        public void OnNewConnection(SocketConnection sender, SocketConnection.NewConnectionEventArgs e)
        {
            Debug.WriteLine("a new connection");
            ChatRoom.OnNewConnection(sender,e);
        }
    }
}



