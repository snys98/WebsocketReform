using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using log4net;
using WebsocketReform.SocketObjects;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace WebsocketReform.Objects
{
    public class ChatRoom
    {
        #region 单例模式

        private static ChatRoom _instance = null;
        private static readonly object SyncRoot = new object();

        public static ChatRoom Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncRoot)
                    {
                        if (null == _instance)
                        {
                            _instance = new ChatRoom();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        public Dictionary<string, Domain> DomainDict { get; }

        public static readonly Dictionary<string, User> UserDict = new Dictionary<string, User>();
        public Domain DefaultDomain => this.DomainDict[""];

        private readonly ILog _logger = LogManager.GetLogger(typeof (ChatRoom));

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
                    //避免数组越界
                    var paramList = partList[3].IndexOf('|') != -1 ? partList[3].Split('|')[1].Split(',') : new string[]{};
                    switch (command)
                    {
                        case "CreateDomain": //创建区域
                        {
                            try
                            {
                                var domainId = paramList[0];
                                var domainName = paramList[1];
                                var domainDesc = paramList[2];
                                var maxNum = paramList[3];
                                if (CreateDomain(domainId, domainName, domainDesc, int.Parse(maxNum)))
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;CreateDomainSuccess");
                                    _logger.Info($"User [{thisUser.Id}] create domain [{domainId}]");
                                }
                                else
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;CreateDomainFail|已有同名区域");
                                    _logger.Warn($"User [{thisUser.Id}] attempt to create domain [domainId] fail: 已有同名区域");
                                }
                            }
                            catch (NullReferenceException ex)
                            {
                                    thisUser.PushMessage($"C;{thisUser.Id};;CreateDomainFail|参数数目少于通信协议?");
                                    _logger.Error($"User [{thisUser.Id}] attempt to create domain [{paramList?[0]}] fail: 参数数目少于通信协议?", ex);
                            }
                            catch (Exception ex)
                            {
                                    thisUser.PushMessage($"C;{thisUser.Id};;CreateDomainFail|未知错误");
                                    _logger.Error($"User [{thisUser.Id}] attempt to create domain [{paramList?[0]}] fail: 未知错误", ex);
                            }
                            return;
                        }
                        case "DeleteDomain": //删除区域,破坏了结构，必须在内部先发送消息再解散区域
                        {
                            try
                            {
                                var domainId = paramList?[0];

                                var domain = DomainDict[domainId];
                                var usersToNotify =
                                    domain.ClassDict.SelectMany(classPair => classPair.Value.UserDict)
                                        .Select(userPair => userPair.Value);
                                DeleteDomain(domain);
                                _logger.Info($"User [{thisUser.Id}] delete domain [{paramList?[0]}]");
                                thisUser.PushMessage($"C;{thisUser.Id};;DeleteDomainSuccess");
                                foreach (var user in usersToNotify)
                                {
                                    user.PushMessage($"C;{user.Id};;DomainDeleted");
                                }
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                _logger.Error($"User [{thisUser.Id}] attempt to delete domain [{paramList[0]}] fail: 参数数目少于通信协议?", ex);
                            }
                            catch (ArgumentException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DeleteDomainFail|区域不存在");
                                _logger.Warn($"User [{thisUser.Id}] attempt to delete domain [{paramList[0]}] fail: 区域不存在");
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DeleteDomainFail|未知错误");
                                Console.WriteLine(ex.Message);
                                _logger.Error($"User [{thisUser.Id}] attempt to delete domain [{paramList[0]}] fail: 未知错误", ex);
                            }
                            return;
                        }
                        case "CreateClass": //创建教室
                        {
                            try
                            {
                                var domainId = paramList[0];
                                var classId = paramList[1];
                                var className = paramList[2];
                                var classDesc = paramList[3];
                                var classPwd = paramList[4];
                                var classMaxNum = paramList[5];
                                if (!DomainDict.ContainsKey(domainId))
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;CreateClassFail|指定区域不存在");
                                    _logger.Warn($"User [{thisUser.Id}] attempt to create class [{classId}] in domain [{domainId}] fail: 指定区域不存在");
                                    return;
                                }
                                var targetDomain = DomainDict[domainId];
                                if (targetDomain.CreateClass(thisUser.Id,
                                    classId,
                                    className,
                                    classDesc,
                                    classPwd,
                                    int.Parse(classMaxNum)))
                                {
                                    thisUser.PushMessage(
                                        $"C;{thisUser.Id};;CreateClassSuccess|{domainId},{classId},{className},{classDesc},{classPwd},{classMaxNum}");
                                    targetDomain.BroadCast(
                                        $"C;{thisUser.Id};;NewClass|{domainId},{classId},{className},{classDesc},{(string.IsNullOrEmpty(classPwd) ? "N" : "Y")},{classMaxNum}",
                                        excludeUser: thisUser);
                                    _logger.Info($"User [{thisUser.Id}] create class [{paramList[0]}]");
                                    return;
                                }
                                thisUser.PushMessage($"C;{thisUser.Id};;CreateClassFail|超过区域总人数限制");
                                _logger.Warn($"User [{thisUser.Id}] attempt to create class [{paramList[1]}] in domain [{domainId}] fail: 超过区域总人数限制");
                                return;
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;CreateClassFail|参数数目少于通信协议?");
                                _logger.Error($"User [{thisUser.Id}] attempt to create class [{paramList[1]}] fail: 参数数目少于通信协议?", ex);
                            }
                            catch (ArgumentException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;CreateClassFail|已有同名教室");
                                _logger.Warn($"User [{thisUser.Id}] attempt to create class [{paramList[1]}] fail: 已有同名教室");
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"{thisUser.Id};;CreateClassFail|未知错误");
                                if (paramList.Length >= 5)
                                    _logger.Error($"User [{thisUser.Id}] attempt to create class [{paramList[1]}] fail: 未知错误", ex);
                            }
                            return;
                        }
                        case "DismissClass": //删除教室，破坏了结构，必须在内部先发送消息再解散教室
                        {
                            try
                            {
                                var classId = paramList[0];
                                var cls = thisDomain.ClassDict[classId];
                                if (cls.Owner != thisUser)
                                {
                                    return;
                                }
                                thisDomain.DeleteClass(cls);
                                thisDomain.BroadCast($"C;{thisUser.Id};;DissmissClass|{cls.Id}");
                                _logger.Info($"User [{thisUser.Id}] dismiss class [{classId}]");
                                //thisUser.PushMessage($"C;{thisUser.Id};;DissmissClassSuccess");//是否通知解散成功
                                return;
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DissmissClassFail|参数数目少于通信协议?");
                                _logger.Error($"User [{thisUser.Id}] attempt to dismiss domain [{paramList[0]}] fail: 参数数目少于通信协议?", ex);
                            }
                            catch (ArgumentException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DissmissClassFail|教室不存在");
                                _logger.Warn($"User [{thisUser.Id}] attempt to dismiss class [{paramList[0]}] fail: 教室不存在");
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DissmissClassFail|未知错误");
                                _logger.Error($"User [{thisUser.Id}] attempt to dismiss class [{paramList[0]}] fail: 未知错误", ex);
                            }
                            return;
                        }
                        case "ChangeDomain": //改变区域
                        {
                            try
                            {
                                var domainId = paramList[0];
                                var targetDomain = DomainDict[domainId];
                                if (thisUser.Domain == targetDomain)
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|你已经在该区域");
                                    _logger.Warn($"User [{thisUser.Id}] attempt to change domain [{paramList[0]}] fail: 已经在该区域");
                                    return;
                                }
                                if (thisUser.ChangeDomain(targetDomain))
                                {
                                    thisDomain.BroadCast(
                                        $"C;{thisUser.Id};;LeftDomain|{thisDomain.Id},{thisDomain.CurNum}");
                                    thisUser.PushMessage(
                                        $"C;{thisUser.Id};;ChangeDomainSuccess|{targetDomain.Id}|{targetDomain.DraftInfo}");
                                    targetDomain.BroadCast(
                                        $"C;{thisUser.Id};;EnterDomain|{targetDomain.Id},{targetDomain.CurNum}|{thisUser.Id},{thisUser.Name},{thisUser.NickName},{thisUser.State},{thisUser.ReceiveState},{thisUser.Sign}",
                                        excludeUser: thisUser);
                                    _logger.Info($"User [{thisUser.Id}] change domian to [{domainId}]");
                                }
                                else
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeDomainFail|超出区域最大允许人数");
                                    _logger.Warn($"User [{thisUser.Id}] attempt to change domain [{paramList[0]}] fail: 超出区域最大允许人数");
                                }
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeDomainFail|参数数目少于通信协议?");
                                    _logger.Error($"User [{thisUser.Id}] attempt to change domain [{paramList[0]}] fail: 参数数目少于通信协议?", ex);
                            }
                            catch (KeyNotFoundException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeDomainFail|区域不存在");
                                _logger.Warn($"User [{thisUser.Id}] attempt to change domain [{paramList[0]}] fail: 区域不存在");
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeDomainFail|未知错误");
                                _logger.Error($"User [{thisUser.Id}] attempt to change domain [{paramList[0]}] fail: 未知错误", ex);
                            }
                            return;
                        }
                        case "ChangeClass": //改变教室
                        {
                            try
                            {
                                var classId = paramList[0];
                                var password = paramList[1];
                                var targetClass = thisDomain.ClassDict[classId];
                                if (thisUser.Class == targetClass)
                                {
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|你已经在该教室");
                                        _logger.Error($"User [{thisUser.Id}] attempt to change class [{paramList[0]}] fail: 用户已经在该教室");
                                        return;
                                }
                                switch (thisUser.ChangeClass(targetClass, password))
                                {
                                    case 0:
                                        if (thisUser.OwnedClasses.Contains(thisClass))
                                        {
                                            thisDomain.DeleteClass(thisClass);
                                            thisDomain.BroadCast($"C;{thisUser.Id};;DissmissClass|{thisClass.Id}");
                                        }
                                        else
                                        {
                                            thisDomain.BroadCast(
                                                $"C;{thisUser.Id};;LeftClass|{thisClass.Id},{thisClass.CurNum}",
                                                excludeUser: thisUser);
                                        }
                                        thisUser.PushMessage(
                                            $"C;{thisUser.Id};;ChangeClassSuccess|{targetClass.Id}|{thisUser.Class.DraftInfo}");
                                        thisDomain.BroadCast(
                                            $"C;{thisUser.Id};;EnterClass|{targetClass.Id},{targetClass.CurNum}",
                                            excludeUser: thisUser);
                                        _logger.Info($"User [{thisUser.Id}] change class to [{classId}]");
                                        return;
                                    case 1:
                                        thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|教室已满");
                                        _logger.Warn($"User [{thisUser.Id}] change class [{classId}] fail: 教室已满");
                                        return;
                                    case 2:
                                        thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|密码错误");
                                        _logger.Warn($"User [{thisUser.Id}] change class [{classId}] fail: 密码错误");
                                        return;
                                    default:
                                        thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|未知错误");
                                        _logger.Warn($"User [{thisUser.Id}] change class [{classId}] fail: 未知错误");
                                        return;
                                }
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|参数数目少于通信协议?");
                                    _logger.Error($"User [{thisUser.Id}] attempt to change class [{paramList[0]}] fail: 参数数目少于通信协议?",ex);
                            }
                            catch (KeyNotFoundException)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|教室不存在");
                                    _logger.Warn($"User [{thisUser.Id}] attempt to change class [{paramList[0]}] fail: 教室不存在");
                            }
                            catch (Exception ex)
                            {
                                    thisUser.PushMessage($"C;{thisUser.Id};;ChangeClassFail|未知错误");
                                    _logger.Error($"User [{thisUser.Id}] attempt to change class [{paramList[0]}] fail: 未知错误",ex);
                                }
                            return;
                        }
                        case "ModiState": //改变状态
                            try
                            {
                                string state = paramList[0];
                                thisUser.ModifyState(state);
                                thisUser.PushMessage($"C;{thisUser.Id};;ModiStateSuccess|{thisUser.State};");
                                thisDomain.BroadCast($"C;{thisUser.Id};;ModiState|{thisUser.State};");
                                _logger.Info($"User [{thisUser.Id}] modify state");
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ModiStateFail|参数数目少于通信协议?");
                                _logger.Error($"User [{thisUser.Id}] attempt to modify state [{paramList[0]}] fail: 参数数目少于通信协议?", ex);
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ModiStateFail|未知错误");
                                _logger.Error($"User [{thisUser.Id}] attempt to modify state [{paramList[0]}] fail: 未知错误", ex);
                            }
                            return;
                        case "ModiSign": //改变签名
                            try
                            {
                                string sign = paramList[0];
                                thisUser.ModifySign(sign);
                                thisUser.PushMessage($"C;{thisUser.Id};;ModiSignSuccess|{thisUser.Sign};");
                                thisDomain.BroadCast($"C;{thisUser.Id};;ModiSign|{thisUser.Sign};");
                                _logger.Info($"User [{thisUser.Id}] modify sign");
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ModiSignFail|参数数目少于通信协议?");
                                _logger.Error($"User [{thisUser.Id}] attempt to modify state fail: 参数数目少于通信协议?", ex);
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ModiSignFail|未知错误");
                                _logger.Error($"User [{thisUser.Id}] attempt to modify state fail: 未知错误", ex);
                            }
                            return;
                        case "InitGraph": //获取所有图形
                        {
                            try
                            {
                                var messageToUser = $"C;{thisUser.Id};InitGraphSuccess";
                                messageToUser += thisUser.AppendAllGraphic();
                                thisUser.PushMessage(messageToUser);
                                _logger.Info($"User [{thisUser.Id}] init graphic");
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;InitGraphFail|参数数目少于通信协议?");
                                _logger.Error($"User [{thisUser.Id}] attempt to init graphic fail: 参数数目少于通信协议?", ex);
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;InitGraphFail|未知错误");
                                _logger.Error($"User [{thisUser.Id}] attempt to init graphic fail: 未知错误", ex);
                            }
                            return;
                        }
                        case "GetGraph": //获取指定图形
                        {
                            try
                            {
                                int sid = int.Parse(paramList[0]) - 1;
                                var messageToUser = $"C;{thisUser.Id};GetGraphSuccess";
                                messageToUser += thisUser.AppendTargetGraphic(sid);
                                thisUser.PushMessage(messageToUser);
                                _logger.Info($"User [{thisUser.Id}] get graphic [{sid}]");
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;GetGraphFail|参数数目少于通信协议?");
                                _logger.Error($"User [{thisUser.Id}] attempt to get graphic fail: 参数数目少于通信协议?", ex);
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;GetGraphFail|未知错误");
                                _logger.Error($"User [{thisUser.Id}] attempt to get graphic fail: 未知错误", ex);
                            }
                            return;
                        }
                        case "ClearGraph": //清除用户所在教室图形
                            try
                            {
                                thisClass.ClearGraphic();
                                thisClass.BroadCast(message);
                                _logger.Info($"User [{thisUser.Id}] clear graphic");
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ClearGraphFail|参数数目少于通信协议?");
                                _logger.Error($"User [{thisUser.Id}] attempt to clear graphic fail: 参数数目少于通信协议?", ex);
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;ClearGraph|未知错误");
                                _logger.Error($"User [{thisUser.Id}] attempt to clear graphic fail: 未知错误", ex);
                            }
                            return;
                        case "DelLastGraph":
                            try
                            {
                                thisClass.DelLastGraph();
                                thisClass.BroadCast($"C;fromUserID;;DelLastGraph|{thisClass.CurrentGID}");
                                _logger.Info($"User [{thisUser.Id}] delete last graphic");
                            }
                            catch (IndexOutOfRangeException ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DelLastGraph|参数数目少于通信协议?");
                                _logger.Error($"User [{thisUser.Id}] attempt to delete last graphic fail: 参数数目少于通信协议?", ex);
                            }
                            catch (Exception ex)
                            {
                                thisUser.PushMessage($"C;{thisUser.Id};;DelLastGraph|未知错误");
                                _logger.Error($"User [{thisUser.Id}] attempt to delete last graphic fail: 未知错误", ex);
                            }
                            return;
                        default:
                            if (thisClass != null)
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
                    string content = partList[3];
                    if (targetId == "ALL")
                    {
                        if (thisClass.Id != "")
                        {
                            thisClass.BroadCast($"T;{thisUser.Id};ALL;{content}" /*,thisUser*/); //排除用户自己
                        }
                        else
                        {
                            thisDomain.BroadCast($"T;{thisUser.Id};ALL;{content}" /*, excludeUser:thisUser*/); //排除用户自己
                        }
                        return;
                    }
                    try
                    {
                        var targetUser = UserDict[targetId];
                        //thisUser.PushMessage($"T;{thisUser.Id};{targetId};{content}");
                        targetUser.PushMessage($"T;{thisUser.Id};{targetId};{content}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("T-Message", ex);
                        thisUser.PushMessage($"T;{thisUser.Id};{targetId};SendMessageFail");
                        return;
                    }
                }

                case "G": //上传图形消息到用户所在教室
                {
                    try
                    {
                        var content = message.Substring(message.LastIndexOf(';') + 1);
                        thisClass.StoreGraphic(content);
                        thisClass.BroadCast($"G;{thisUser.Id};;{content}|{thisClass.CurrentGID}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("G-Message", ex);
                        thisUser.PushMessage($"G;{thisUser.Id};{targetId};SendMessageFail");
                    }
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
            if (DomainDict.ContainsKey(domainId))
            {
                return false;
            }
            DomainDict.Add(domainId, newDomain);
            return true;
        }

        public void OnDisconnected(SocketConnection sender, EventArgs e)
        {
            User leavingUser = (from userPair in UserDict where userPair.Value.Socket == sender select userPair.Value).FirstOrDefault();
            try
            {
                if (leavingUser == null)
                {
                    _logger.Warn("null user disconnect");
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
                if (leavingUser.OwnedClasses != null)
                {
                    foreach (Class cls in leavingUser.OwnedClasses)
                    {
                        cls.Domain.DeleteClass(cls);
                        cls.Domain.BroadCast($"C;{leavingUser.Id};;DissmissClass|{cls.Id}");
                    }
                }
                _logger.Info($"User [{leavingUser.Id}] disconnected");
            }
            catch (Exception ex)
            {
                _logger.Warn("OnDisconnected多个用户同时下线与该用户同时下线: " + leavingUser?.Id, ex);
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
                User originalUser = UserDict[thisUser.Id];
                originalUser?.PushMessage($"C;{originalUser.Id};;LoginFail|账号在别处登陆");
                thisUser.Class.UserDict.Remove(originalUser.Id);
                originalUser?.Socket.Socket.Close();
                UserDict[thisUser.Id] = thisUser;
                thisUser.Class.UserDict.Add(thisUser.Id, thisUser);
                thisUser.PushMessage($"C;{thisUser.Id};;LoginSuccess|{this.DraftInfo}");
                _logger.Info($"User [{thisUser.Id}] forced connected");
            }
            catch (KeyNotFoundException)
            {
                UserDict.Add(thisUser.Id, thisUser);
                thisUser.Class.UserDict.Add(thisUser.Id, thisUser);
                thisUser.PushMessage($"C;{thisUser.Id};;LoginSuccess|{this.DraftInfo}");
                _logger.Info($"User [{thisUser.Id}] connected");
            }
        }

        public string DraftInfo
        {
            get
            {
                string result = this.DomainDict.Select(item => item.Value)
                    .Aggregate("", (current, domain) => current + $"{domain.Id},{domain.CurNum},{domain.MaxNum}|");
                return result.Remove(result.LastIndexOf('|'));
            }
        }
    }
}
