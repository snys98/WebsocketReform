﻿using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using WebsocketReform.Objects;
using Timer = System.Timers.Timer;

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
        private List<Socket> _listener;
        private int _connectionsQueueLength;
        private int _maxBufferSize;
        private string _handshake;
        private StreamReader _connectionReader;
        private StreamWriter _connectionWriter;
        private Logger _logger;
        private readonly ILog _logger4net = LogManager.GetLogger(typeof(WebSocketServer));
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

        public List<Socket> Listener
        {
            get { return _listener; }
            set { _listener = value; }
        }

        private void Initialize()
        {
            _alreadyDisposed = false;
            _logger = new Logger();
            _listener = new List<Socket>();
            Status = ServerStatusLevel.Off;
            _connectionsQueueLength = 500;
            _maxBufferSize = 1024 * 100;
            _firstByte = new byte[_maxBufferSize];
            _lastByte = new byte[_maxBufferSize];
            _firstByte[0] = 0x00;
            _lastByte[0] = 0xFF;
            _logger.LogEvents = true;

            var ipEntry = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in ipEntry.AddressList)
            {

                //IPV4
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                    socket.Bind(new IPEndPoint(ip, ServerPort));
                    _listener.Add(socket);
                }
            }
            

            ClassOwnerReconnectMonitors = new Dictionary<string, Timer>();
        }

        public WebSocketServer(string address = "chat", int port = 4141)
        {
            ServerPort = port;
            //ServerLocation = $"ws://{getLocalmachineIPAddress()}:{port}/{address}";
            Initialize();
        }

        public WebSocketServer(int serverPort, string serverLocation, string connectionOrigin)
        {
            ServerPort = serverPort;
            ConnectionOrigin = connectionOrigin;
            //ServerLocation = serverLocation;
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
                if (_listener != null)
                    _listener.ForEach(item=>item.Close());
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

        public ChatRoom ChatRoom { get; } = ChatRoom.Instance;

        public void StartServer()
        {
            
                Char char1 = Convert.ToChar(65533);
                for (int i = 0; i < _listener.Count; i++)
                {
                    new Thread((socketObj) =>
                    {
                        var socket = socketObj as Socket;
                        socket.Listen(_connectionsQueueLength);
                        _logger.Log(string.Format("聊天服务器启动。监听地址：{0}", socket.LocalEndPoint));
                        _logger.Log(string.Format("WebSocket服务器地址: ws://{0}/chat", socket.LocalEndPoint));
                        while (true)
                        {
                            try
                            {
                                Socket sc = socket.Accept();
                                //Console.WriteLine("*****Available在线程[" + Thread.CurrentThread.ManagedThreadId + "]中为" + sc.Available);
                                if (sc != null)
                                {
                                    //System.Threading.Thread.Sleep(100);   
                                    SocketConnection socketConn = new SocketConnection(sc);
                                    //Console.WriteLine("*****Available在线程[" + Thread.CurrentThread.ManagedThreadId + "]中为" + socketConn.Socket.Available);
                                    socketConn.NewConnection += new NewConnectionEventHandler(OnNewConnection);
                                    socketConn.DataReceived += new DataReceivedEventHandler(OnDataReceived);
                                    socketConn.Disconnected += new DisconnectedEventHandler(OnDisconnected);


                                    //socketConn.Socket.BeginReceive(socketConn.receivedDataBuffer,
                                    //0,
                                    //socketConn.receivedDataBuffer.Length,
                                    //0,
                                    //    ar =>
                                    //    {
                                    //        if ((int)ar.AsyncState == 0)
                                    //        {
                                    //            return;
                                    //        }
                                    //        Console.WriteLine("*****当前线程[" + Thread.CurrentThread.ManagedThreadId + "],Available值为" + socketConn.Socket.Available);
                                    //        socketConn.ManageHandshake(ar);
                                    //    },
                                    //socketConn.Socket.Available);
                                    var dataLength = socketConn.Socket.Receive(socketConn.receivedDataBuffer, 0, socketConn.receivedDataBuffer.Length, 0);
                                    if (dataLength != 0)
                                    {
                                        socketConn.ManageHandshake(dataLength: dataLength);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger4net.Error(ex);
                            }
                        }
                    }).Start(_listener[i]);
                }
            
        }

        public void OnDisconnected(SocketConnection sender, EventArgs e)
        {
            Console.WriteLine("a connection closed");
            ChatRoom.OnDisconnected(sender,e);
            sender.Socket.Close();
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
            //        Console.WriteLine("重新启动计时!");
            //    }
            //}
            //else
            //{
            //    Console.WriteLine("连接异常关闭!");
            //}
        }

        public void OnDataReceived(SocketConnection sender, string message, EventArgs e)
        {
            Console.WriteLine("a new message");
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
            Console.WriteLine("a new connection");
            ChatRoom.OnNewConnection(sender,e);
        }
    }
}



