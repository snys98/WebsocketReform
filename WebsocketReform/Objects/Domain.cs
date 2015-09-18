using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WebsocketReform.Objects
{
    //区域
    public class Domain
    {
        public static Domain DefaultDomain = new Domain("");
        public Domain(string id)
        {
            Id = id;
            ClassDict = new Dictionary<string, Class> { { "", new Class("",this) { MaxNum = this.MaxNum } } };
        }

        public string Id { get; }

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public int MaxNum { get; set; } = 1000;

        //当前教室占用人口
        public int CurNum
        {
            get
            {
                return this.ClassDict.Sum(item => item.Value.CurNum);
            }
        }

        public Dictionary<string,Class> ClassDict { get; }

        public Class DefaultClass => this.ClassDict[""];
        public bool IsFull => CurNum >= MaxNum;
        public string DraftInfo {
            get
            {
                string result = ClassDict
                    .SelectMany(item => item.Value.UserDict).Select(item => item.Value)
                    .Aggregate("",
                        (current, user) =>
                            current +
                            $"{user.Id},{user.Name},{user.NickName},{user.State},{user.ReceiveState},{user.Sign},{user.Class.Id}|");
                result = ClassDict.Select(cls => cls.Value)
                    .Aggregate(result,
                        (current, cls) =>
                            current +
                            $"{cls.Id},{cls.Name},{cls.Description},{(cls.HasPwd ? "Y" : "N")},{cls.CurNum},{cls.MaxNum}|");
                return result.TrimEnd('|');
            }
        }

        public bool CreateClass(string createrId, string classId, string className, string classDesc, string classPwd, int classMaxNum)
        {
            User creater = ChatRoom.UserDict[createrId];
            if (classMaxNum > this.MaxNum)
            {
                return false;
            }
            var cls = new Class(classId,this)
            {
                Description = classDesc,
                Password = classPwd,
                MaxNum = classMaxNum,
                Name = className,
                Owner = creater
            };
            this.ClassDict.Add(classId, cls);
            creater.OwnedClasses.Add(cls);
            return true;
        }

        public void DeleteClass(Class cls)
        {
            cls.Dismiss();
            this.ClassDict.Remove(cls.Id);
        }

        public void BroadCast(string messageToSend,Class excludeClass = null,User excludeUser = null)
        {
            foreach (KeyValuePair<string, Class> keyValuePair in ClassDict)
            {
                if (keyValuePair.Value!=excludeClass)
                {
                    keyValuePair.Value.BroadCast(messageToSend, excludeUser);
                }
            }
        }
    }
}
