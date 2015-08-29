//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Text;
//using WebsocketReform.Objects;
//using WebsocketReform;

//namespace WebSocketServer.Dealers
//{
//    public static class MsgSender
//    {
//        internal static void SendMessage(string messageToSend, Domain domain = null, Class cls = null, User user = null)
//        {
//            if (string.IsNullOrWhiteSpace(messageToSend)) return;
//            try
//            {
//                user?.PushMessage(messageToSend);
//                cls?.BroadCast(messageToSend);
//                domain?.BroadCast(messageToSend);
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine(ex.Message);
//            }
//        }
//    }
//}
