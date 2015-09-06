using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using WebsocketReform.SocketObjects;

namespace WebsocketReform.Objects
{
    public class ChatRoom
    {
        public Dictionary<string, Domain> DomainDict { get; }

        public static Dictionary<string, User> UserDict = new Dictionary<string, User>();
        public Domain DefaultDomain => this.DomainDict[""];

        public ChatRoom()
        {
            DomainDict = new Dictionary<string, Domain> {{"", new Domain("")}};
        }

        //private void CheckUserState()
        //{
        //    while (true)
        //    {
        //        //15秒检查一次状态异常的用户,将其移出教室
        //        Thread.Sleep(1000*15);
        //        foreach (var userPair in UserDict)
        //        {
        //            if (!userPair.Value.Socket.Socket.Connected)
        //            {
        //                var thisClass = userPair.Value.Class;
        //                if (userPair.Value.Class.UserDict.Remove(userPair.Key))
        //                {
        //                    userPair.Value.Class = null;
        //                    thisClass.BroadCast($"C;{userPair.Value.Id};;LeftClass|{userPair.Value.Id},{userPair.Value.Class}");
        //                }
        //            }
        //        }
        //    }
        //}

        public void OnDataReceived(SocketConnection sender, string message, EventArgs e)
        {
            var partList = message.Split(';');
            var controlHeader = partList[0];
            var senderId = partList[1];
            var thisUser = UserDict[senderId];
            var thisClass = thisUser.Class;
            var thisDomain = thisUser.Domain;
            var targetId = partList[2];
            switch (controlHeader)
            {
                case "C": //控制消息
                    var command = partList[3].Split('|')[0];
                    var paramList = partList[3].IndexOf('|') != -1 ? partList[3].Split('|')[1].Split(',') : null;
                    switch (command)
                    {
                        case "CreateDomain": //创建区域
                        {
                            var domainId = paramList[0];
                            var domainName = paramList[1];
                            var domainDesc = paramList[2];
                            var maxNum = paramList[3];
                            thisUser.PushMessage(CreateDomain(domainId, domainName, domainDesc, int.Parse(maxNum))
                                ? $"C;{thisUser.Id};;CreateDomainSuccess"
                                : $"C;{thisUser.Id};;CreateDomainFail|{"已有同名区域"}");
                            return;
                        }
                        case "DeleteDomain": //删除区域,破坏了结构，必须在内部先发送消息再解散区域
                        {
                            var domainId = paramList[0];
                            try
                            {
                                var domain = DomainDict[domainId];
                                var usersToNotify =
                                    domain.ClassDict.SelectMany(classPair => classPair.Value.UserDict)
                                        .Select(userPair => userPair.Value);
                                DeleteDomain(domain);
                                thisUser.PushMessage($"C;{thisUser.Id};;DeleteDomainSuccess");
                                foreach (var user in usersToNotify)
                                {
                                    user.PushMessage($"C;{user.Id};;DomainDeleted");
                                }
                            }
                            catch (ArgumentException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DeleteDomainFail|区域不存在");
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DeleteDomainFail|未知错误");
                                Debug.WriteLine(ex.Message);
                            }
                            return;
                        }
                        case "CreateClass": //创建教室
                        {
                            var domainId = paramList[0];
                            var classId = paramList[1];
                            var className = paramList[2];
                            var classDesc = paramList[3];
                            var classPwd = paramList[4];
                            var classMaxNum = paramList[5];
                            try
                            {
                                var targetDomain = DomainDict[domainId];
                                if (targetDomain.CreateClass(thisUser.Id,classId, className, classDesc, classPwd,
                                    int.Parse(classMaxNum)))
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;CreateClassSuccess");
                                    targetDomain.BroadCast(
                                        $"C;{thisUser.Id};;NewClass|{domainId},{classId},{className},{classDesc},{(string.IsNullOrWhiteSpace(classPwd) ? "N" : "Y")},{classMaxNum}");
                                    return;
                                }
                                thisUser.PushMessage($"C;{thisUser.Id};;CreateClassFail|超过区域总人数限制");
                                return;
                            }
                            catch (ArgumentException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;CreateClassFail|已有同名教室");
                                return;
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"{thisUser.Id};;CreateClassFail|{"未知错误"}");
                                Debug.WriteLine(ex.Message);
                                return;
                            }
                        }
                        case "DismissClass": //删除教室，破坏了结构，必须在内部先发送消息再解散教室
                        {
                            var classId = paramList[0];
                            try
                            {
                                var cls = thisDomain.ClassDict[classId];
                                if (cls.Owner!=thisUser)
                                {
                                    return;
                                }
                                thisDomain.DeleteClass(cls);
                                thisDomain.BroadCast($"C;{thisUser.Id};;DissmissClass|{cls.Id}");
                                //thisUser.PushMessage($"C;{thisUser.Id};;DissmissClassSuccess");//是否通知解散成功
                                return;
                            }
                            catch (ArgumentException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DissmissClassFail|教室不存在");
                                return;
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DissmissClassFail|未知错误");
                                Debug.WriteLine(ex.Message);
                                return;
                            }
                        }
                        case "ChangeDomain": //改变区域
                        {
                            var domainId = paramList[0];
                            Domain targetDomain;
                            try
                            {
                                targetDomain = DomainDict[domainId];
                            }
                            catch (KeyNotFoundException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeDomainFail|{"区域不存在"}");
                                return;
                            }
                            if (thisUser.Domain == targetDomain)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"你已经在该区域"}");
                                return;
                            }
                            if (thisUser.ChangeDomain(targetDomain))
                            {
                                thisDomain.BroadCast($"C;{thisUser.Id};;LeftDomain|{thisDomain.Id},{thisDomain.CurNum}");
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeDomainSuccess|{thisUser.Domain.DraftInfo}");
                                targetDomain.BroadCast(
                                    $"C;{thisUser.Id};;EnterDomain|{targetDomain.Id},{targetDomain.CurNum}|{thisUser.Id},{thisUser.Name},{thisUser.NickName},{thisUser.State},{thisUser.ReceiveState},{thisUser.Sign}");
                            }
                            else
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeDomainFail|{"超出区域最大允许人数"}");
                            }
                            return;
                        }
                        case "ChangeClass": //改变教室
                        {
                            var classId = paramList[0];
                            var password = paramList[1];
                            Class targetClass;
                            try
                            {
                                targetClass = thisDomain.ClassDict[classId];
                            }
                            catch (KeyNotFoundException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"教室不存在"}");
                                return;
                            }
                            if (thisUser.Class == targetClass)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"你已经在该教室"}");
                                return;
                            }
                            switch (thisUser.ChangeClass(targetClass, password))
                            {
                                case 0:
                                    thisDomain.BroadCast($"C;{thisUser.Id};;LeftClass|{thisClass.Id},{thisClass.CurNum}",excludeUser: thisUser);
                                    thisUser.PushMessage(
                                        $"C;{thisUser.Id};;ChangeClassSuccess|{thisUser.Class.DraftInfo}");
                                    thisDomain.BroadCast($"C;{thisUser.Id};;EnterClass|{targetClass.Id},{targetClass.CurNum}",excludeUser: thisUser);
                                    return;
                                case 1:
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"教室已满"}");
                                    return;
                                case 2:
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"密码错误"}");
                                    return;
                                default:
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"未知错误"}");
                                    return;
                            }
                        }
                        case "ModiState": //改变状态
                            string state = paramList[0];
                            thisUser.ModifyState(state);
                            thisUser.PushMessage($"C;{thisUser.Id};;ModiStateSuccess|{thisUser.State}|;");
                            thisDomain.BroadCast($"C;{thisUser.Id};;ModiState|{thisUser.State}|;");
                            return;
                        case "ModiSign": //改变签名
                            string sign = paramList[0];
                            thisUser.ModifySign(sign);
                            thisUser.PushMessage($"C;{thisUser.Id};;ModiSignSuccess|{thisUser.Sign}|;");
                            thisDomain.BroadCast($"C;{thisUser.Id};;ModiSign|{thisUser.Sign}|;");
                            return;
                        case "InitGraph": //获取所有图形
                        {
                            try
                            {
                                    var messageToUser = $";{thisUser.Id};InitGraphSuccess|";
                                    messageToUser += thisUser.AppendAllGraphic();
                                    thisUser.PushMessage(messageToUser);
                                }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($";{thisUser.Id};InitGraphFail");
                                Debug.WriteLine(ex.Message);
                            }
                            return;
                        }
                        case "GetGraph": //获取指定图形
                            {
                                int sid = int.Parse(paramList[0])-1;
                                try
                                {
                                    var messageToUser = $"G;{thisUser.Id};GetGraphSuccess|";
                                    messageToUser += thisUser.AppendTargetGraphic(sid);
                                    thisUser.PushMessage(messageToUser);
                                }
                                catch (Exception ex)
                                {
                                    thisUser.PushMessage($"G;{thisUser.Id};InitGraphFail");
                                    Debug.WriteLine(ex.Message);
                                }
                                return;
                            }
                        case "ClearGraph": //清除用户所在教室图形
                            thisClass.ClearGraphic();
                            thisClass.BroadCast(message);
                            return;
                        case "DelLastGraph":
                            thisClass.DelLastGraph();
                            thisClass.BroadCast($"C;fromUserID;;DelLastGraph|{thisClass.CurrentGID}");
                            return;
                        default:
                            if (thisClass!=null)
                            {
                                thisClass.BroadCast(message);
                            }
                            else
                            {
                                thisDomain?.BroadCast(message);
                            }
                            return;
                    }
                case "T": //文字消息
                {
                    try
                    {
                        var targetUser = UserDict[targetId];
                        thisUser.PushMessage($"T;{thisUser.Id};{targetId};{partList[3]}");
                        targetUser.PushMessage($"T;{thisUser.Id};{targetId};{partList[3]}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        thisUser.PushMessage($"C;{thisUser.Id};{targetId};SendMessageFail");
                        return;
                    }
                }

                case "G": //上传图形消息到用户所在教室
                {
                    var content = message.Substring(message.LastIndexOf(';')+1);
                    thisClass.StoreGraphic(content);
                        thisClass.BroadCast($"C;{thisUser.Id};;{content}|{thisClass.CurrentGID}");
                    return;
                }
            }
        }

        private void DeleteDomain(Domain domain)
        {
            var usersToMove = domain.ClassDict.SelectMany(classPair => classPair.Value.UserDict).Select(userPair => userPair.Value);
            foreach (var user in usersToMove)
            {
                user.Class = DefaultDomain.DefaultClass;
                DefaultDomain.DefaultClass.UserDict.Add(user.Id, user);
            }
            this.DomainDict.Remove(domain.Id);
        }

        private bool CreateDomain(string domainId, string domainName = "", string domainDesc = "", int maxNum = 1000)
        {
            var newDomain = new Domain(domainId) {Name = domainName, Description = domainDesc, MaxNum = maxNum};
            try
            {
                DomainDict.Add(domainId, newDomain);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        public void OnDisconnected(SocketConnection sender, EventArgs e)
        {
            User leavingUser = null;
            foreach (var userPair in UserDict)
            {
                if (userPair.Value.Socket==sender)
                {
                    leavingUser = userPair.Value;
                    break;
                }
            }
            if (leavingUser==null)
            {
                return;
            }
            var thisClass = leavingUser.Class;
            var thisDomain = thisClass.Domain;
            if (leavingUser.Class.UserDict.Remove(leavingUser.Id))
            {
                leavingUser.Class = null;
                thisClass.BroadCast($"C;{leavingUser.Id};;LeftClass|{thisClass.Id},{thisClass.CurNum}");
                thisDomain.BroadCast($"C;{leavingUser.Id};;LeftDomain|{thisDomain.Id},{thisDomain.CurNum}");
            }
            //todo:用户离线解散其名下所有教室,那么改变区域是否需要解散其在其他区域的教室
            if (leavingUser.OwnedClasses!=null)
            {
                foreach (Class cls in leavingUser.OwnedClasses)
                {
                    cls.Dismiss();
                }
            }
        }

        public void OnNewConnection(SocketConnection sender, SocketConnection.NewConnectionEventArgs e)
        {
            User thisUser = new User(e.UserId, sender)
            {
                Name = e.UserName,
                NickName = e.UserNickName,
                Description = e.UserDesc,
                State = e.UserState,
                ReceiveState = e.RecState,
                Sign = e.UserSign,
                Class = this.DefaultDomain.DefaultClass,
                Socket = sender,
            };
            //todo:验证相同ID在线,挤下去,消息提示?
            try
            {
                User otherUser = UserDict[thisUser.Id];
                otherUser?.PushMessage($"C;{otherUser.Id};;LoginFail|账号在别处登陆");
                otherUser?.Socket.Socket.Close();
                UserDict[thisUser.Id] = thisUser;
                thisUser.PushMessage($"C;{otherUser.Id};;LoginSuccess|{this.DraftInfo}");
            }
            catch (KeyNotFoundException)
            {
                UserDict.Add(thisUser.Id, thisUser);
                thisUser.PushMessage($"C;{thisUser.Id};;LoginSuccess|{this.DraftInfo}");
            }
            thisUser.Class = DefaultDomain.DefaultClass;
        }

        public string DraftInfo {
            get
            {
                string result = this.DomainDict.Select(item => item.Value)
                    .Aggregate("", (current, domain) => current + $"{domain.Id},{domain.CurNum},{domain.MaxNum}|");
                return result.Remove(result.LastIndexOf('|'));
            }
        }
    }
}
