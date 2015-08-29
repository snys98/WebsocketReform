using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebsocketReform.Objects;
using WebSocketServer = WebsocketReform.SocketObjects.WebSocketServer;

namespace WebsocketReform
{
    class Program
    {
        public static SocketObjects.WebSocketServer WsServer = new SocketObjects.WebSocketServer("chat",4141);
        static void Main(string[] args)
        {
            WsServer.StartServer();
        }
    }
}
