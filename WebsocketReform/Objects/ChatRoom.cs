using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WebsocketReform.SocketObjects;

namespace WebsocketReform.Objects
{
    public class ChatRoom
    {
        public Dictionary<string, Domain> DomainDcit { get; }

        public Hashtable UserHashtable = Hashtable.Synchronized(new Hashtable());

        public ChatRoom()
        {
            DomainDcit = new Dictionary<string, Domain> {{"0", new Domain("0")}};
        }

        public void OnDataReceived(SocketConnection sender, string message, EventArgs e)
        {
            var partList = message.Split(';');
            var controlHeader = partList[0];
            var senderId = partList[1];
            var thisUser = (User) UserHashtable[senderId];
            var thisClass = thisUser.Class;
            var thisDomain = thisUser.Domain;
            var targetId = partList[2];
            switch (controlHeader)
            {
                case "C": //控制消息
                    var command = partList[3].Split('|')[0];
                    var paramList = partList[3].Split('|')[1].Split(',');
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
                                var domain = DomainDcit[domainId];
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
                        case "NewClass": //创建教室
                        {
                            var domainId = paramList[0];
                            var classId = paramList[1];
                            var className = paramList[2];
                            var classDesc = paramList[3];
                            var classPwd = paramList[4];
                            var classMaxNum = paramList[5];
                            try
                            {
                                var targetDomain = DomainDcit[domainId];
                                if (thisDomain.CreateClass(classId, className, classDesc, classPwd,
                                    int.Parse(classMaxNum)))
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;CreateClassSuccess");
                                    thisDomain.BroadCast(
                                        $"C;{thisUser.Id};;NewClass|{domainId}|{className}|{classDesc}|{(string.IsNullOrWhiteSpace(classPwd) ? "Y" : "N")}");
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
                                var usersToNotify = cls.UserDict.Select(userPair => userPair.Value).ToList();
                                thisDomain.DeleteClass(cls);
                                thisUser.PushMessage($"C;{thisUser.Id};;DissmissClassSuccess");
                                foreach (var user in usersToNotify)
                                {
                                    user.PushMessage($"C;{user.Id};;DissmissClass");
                                }
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
                                targetDomain = DomainDcit[domainId];
                                if (thisUser.Domain == targetDomain)
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"你已经在该区域"}");
                                    return;
                                }
                            }
                            catch (KeyNotFoundException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeDomainFail|{"区域不存在"}");
                                return;
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeDomainFail|{"未知错误"}");
                                Debug.WriteLine(ex.Message);
                                return;
                            }
                            thisUser.PushMessage(
                                thisUser.ChangeDomain(targetDomain)
                                    ? $"C;{thisUser.Id};;ChangeDomainSuccess|{thisUser.Class.Domain.DraftInfo}"
                                    : $"C;{thisUser.Id};;ChangeDomainFail|{"区域已满"}");
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
                                if (thisUser.Class == targetClass)
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"你已经在该教室"}");
                                    return;
                                }
                            }
                            catch (KeyNotFoundException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"教室不存在"}");
                                return;
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"未知错误"}");
                                Debug.WriteLine(ex.Message);
                                return;
                            }
                            switch (thisUser.ChangeClass(targetClass, password))
                            {
                                case 0:
                                    thisUser.PushMessage(
                                        $"C;{thisUser.Id};;ChangeClassSuccess|{thisUser.Class.DraftInfo}");
                                    return;
                                case 1:
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"教室已满"}");
                                    return;
                                case 2:
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|{"密码错误"}");
                                    return;
                                default:
                                    return;
                            }
                        }
                        case "GetGraph": //获取指定图形
                            {
                                int sid = int.Parse(paramList[0]);
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

                        case "ClearGraph": //清除用户所在教室图形
                            thisClass.ClearGraphic();
                            return;
                    }
                    return;
                case "T": //文字消息
                {
                    try
                    {
                        var targetUser = (User) UserHashtable[targetId];
                        thisUser.PushMessage($"T;{thisUser.Id};{targetId};{partList[3]}");
                        targetUser.PushMessage($"T;{thisUser.Id};{targetId};{partList[3]}");
                        return;
                    }
                    catch (Exception)
                    {
                        thisUser.PushMessage($"C;{thisUser.Id};{targetId};SendMessageFail");
                        return;
                    }
                }

                case "G": //上传图形消息到用户所在教室
                {
                    var content = message.Substring(message.IndexOf(';', 0, 3));
                    thisClass.StoreGraphic(content);
                    return;
                }
            }
        }

        private void DeleteDomain(Domain domain)
        {
            domain.Dismiss();
        }

        private bool CreateDomain(string domainID, string domainName = "", string domainDesc = "", int maxNum = 1000)
        {
            var newDomain = new Domain(domainID) {Name = domainName, Description = domainDesc, MaxNum = maxNum};
            try
            {
                DomainDcit.Add(domainID, newDomain);
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
        }

        public void OnNewConnection(SocketConnection sender, EventArgs e)
        {
        }
    }
}
