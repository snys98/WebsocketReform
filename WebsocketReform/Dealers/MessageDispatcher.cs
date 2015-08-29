//using System;
//using System.Collections.Generic;
//using System.Text;
//using WebsocketReform.commands;
//using WebsocketReform.Message;
//using WebsocketReform.Objects;
//using WebsocketReform.SocketObjects;

//namespace WebSocketServer.Dealers
//{
//    public class MessageDispatcher
//    {
//        public static MessageBase CreateMessage()
//        {
//        }

//        public MessageBase BuildMessageObject(SocketConnection sender, string message)
//        {
//            string[] partList = message.Split(';');
//            string controlHeader = partList[0];
//            string senderId = partList[1];
//            string targetId = partList[2];
//            switch (controlHeader)
//            {
//                case "C"://控制消息
//                    string command = partList[3].Split('|')[0];
//                    string[] paramList = partList[3].Split('|')[1].Split(',');
//                    switch (command)
//                    {
//                        case "CreateDomain"://创建区域
//                            CMDAboutDomain.CreateDomain(user, content, out target, out messageToUser, out messageToOthers);
//                            return;
//                        case "DeleteDomain"://删除区域,破坏了结构，必须在内部先发送消息再解散区域
//                            CMDAboutDomain.DeleteDomain(user, content, out target, out messageToUser, out messageToOthers, server);
//                            return;
//                        case "NewClass"://创建教室
//                            CMDAboutClass.CreateClass(user, content, out target, out messageToUser, out messageToOthers);
//                            return;
//                        case "DismissClass"://删除教室，破坏了结构，必须在内部先发送消息再解散教室
//                            CMDAboutClass.DeleteClass(user, content, out target, out messageToUser, out messageToOthers, server);
//                            return;
//                        case "ChangeDomain"://改变区域
//                            CMDAboutUser.AlterDomain(user, content, out target, out messageToUser, out messageToOthers);
//                            return;
//                        case "EnterClass": //改变教室
//                            CMDAboutUser.AlterClass(user, content, out target, out messageToUser, out messageToOthers);
//                            return;
//                        case "GetGraph"://获取指定图形
//                            target = user;
//                            CMDAboutGraphic.GetGraphic(user, content, out target, out messageToUser, out messageToOthers);
//                            return;
//                        case "LeftClass"://离开教室，前往默认教室
//                            target = user.Class;
//                            CMDAboutUser.GoToDefaultClass(user, content, out target, out messageToUser, out messageToOthers);
//                            return;
//                        case "ModiState"://改变状态
//                            CMDAboutUser.ModifyState(user, content, out target, out messageToUser, out messageToOthers);
//                            target = user.Class.Domain;
//                            return;
//                        case "ModiSign"://改变签名
//                            CMDAboutUser.ModifySign(user, content, out target, out messageToUser, out messageToOthers);
//                            target = user.Class.Domain;
//                            return;
//                        case "InitGraph"://获取所有图形
//                            target = user;
//                            CMDAboutGraphic.InitGraphic(user, content, out target, out messageToUser, out messageToOthers);
//                            return;
//                        case "ClearGraph"://清除用户所在教室图形
//                            target = user.Class;
//                            CMDAboutGraphic.ClearGraphic(user, content, out target, out messageToUser, out messageToOthers);
//                            return;
//                        #region 其他非对象操作
//                        case "GetUsers":
//                            CommandInterface.GetUsers(user, content, out target, out messageToUser, out messageToOthers);
//                            return;
//                            #endregion
//                    }
//                    return;
//                case "T"://文字消息
//                    CMDAboutText.DealTextCMD(user, content, out target, out messageToUser, out messageToOthers, toUserId);
//                    return;
//                case "G"://上传图形消息到用户所在教室
//                    target = user.Class;
//                    CMDAboutGraphic.StoreMessage(user, content, out target, out messageToUser, out messageToOthers);
//                    return;
//            }
//        }
//    }
//}
