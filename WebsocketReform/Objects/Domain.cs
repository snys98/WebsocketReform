using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WebsocketReform.Objects
{
    //区域
    public class Domain
    {
        public static Domain DefaultDomain = new Domain("0");
        public Domain(string id)
        {
            Id = id;
            ClassDict = new Dictionary<string, Class> { { "0", new Class("0") { MaxNum = this.MaxNum } } };
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

        public Class DefaultClass => this.ClassDict["0"];
        public bool IsFull => CurNum >= MaxNum;
        public string DraftInfo {
            get {
                string result = ClassDict.Select(item => item.Value).Aggregate("", (current, cls) => current +
                                                                                                     $"{cls.Id},{cls.Name},{cls.Description},{(cls.HasPwd ? "Y" : "N")},{cls.UserDict.Count},{cls.MaxNum}|");
                result.Remove(result.LastIndexOf("|", StringComparison.Ordinal));
                return result;
            }
        }

        public bool CreateClass(string classId, string className, string classDesc, string classPwd, int classMaxNum)
        {
            if (classMaxNum > this.MaxNum)
            {
                return false;
            }
            var cls = new Class(classId)
            {
                Description = classDesc,
                Password = classPwd,
                MaxNum = classMaxNum,
                Name = className
            };
            this.ClassDict.Add(classId, cls);
            return true;
        }

        public void DeleteClass(Class cls)
        {
            cls.Dismiss();
        }

        public void BroadCast(string messageToSend)
        {
            foreach (KeyValuePair<string, Class> keyValuePair in ClassDict)
            {
                keyValuePair.Value.BroadCast(messageToSend);
            }
        }

        public void Dismiss()
        {
            var usersToMove = ClassDict.SelectMany(classPair => classPair.Value.UserDict).Select(userPair => userPair.Value);
            foreach (var user in usersToMove)
            {
                user.Class = DefaultDomain.DefaultClass;
                DefaultDomain.DefaultClass.UserDict.Add(user.Id,user);
            }
        }
    }
}
