using System;
using System.Linq;
using WebsocketReform.SocketObjects;

namespace WebsocketReform.Objects
{
    public enum Identity
    {
        Student = 0,
        Teacher = 1,
    }

    //public enum UserState
    //{
    //    Online = 0,
    //    Hide = 1,
    //    Away = 2,
    //    Busy = 3,
    //}

    public enum ReceiveState
    {
        AllMessage=0,
        ClassMessageOnly=1,
        NoMessage = 100,
    }

    //用户类型
    public class User
    {
        public User(SocketConnection socket)
        {
            Socket = socket;
        }

        public User(string id, SocketConnection socket)
        {
            Id = id;
            Socket = socket;
        }

        public Identity Identity { get; set; } = Identity.Student;
        public bool HasClass { get; set; } = false;
        public bool IsValid => this.Id == null;
        public string Id { get; }

        public string Name { get; set; } = "";

        public string NickName { get; set; } = "";

        public string Description { get; set; } = "S";

        public string State { get; set; } = "O";

        public ReceiveState ReceiveState { get; set; } = 0;

        public string Sign { get; set; } = "";

        public SocketConnection Socket { get; }

        public Class Class { get; set; }

        public Domain Domain => this.Class.Domain;

        public override string ToString()
        {
            return this.Id;
        }

        public bool ChangeDomain(Domain targetDomain)
        {
            if (targetDomain.IsFull)
            {
                return false;
            }
            this.Class = targetDomain.DefaultClass;
            return true;
        }

        internal int ChangeClass(Class cls, string password)
        {
            if (cls.IsFull)
            {
                return 1;
            }
            if (cls.HasPwd && cls.Password != password)
            {
                return 2;
            }
            this.Class = cls;
            cls.UserDict.Add(this.Id,this);
            return 0;
        }

        internal void ModifyState(string state)
        {
            this.State = state;
        }

        internal void ModifySign(string sign)
        {
            this.Sign = sign;
        }


        public void PushMessage(string messageToSend)
        {
            this.Socket.Send(messageToSend);
        }

        public string AppendAllGraphic()
        {
            return this.Class.ClassGraph.Aggregate("",(current, t) => current + ("|" + t.Content)).Remove(0);
        }

        public string AppendTargetGraphic(int sid)
        {
            if (this.Class.ClassGraph.Count >= sid)
            {
                return this.Class.GetGraphic(sid);
            }
            else
            {
                throw new IndexOutOfRangeException("申请获取的图像不存在");
            }
        }
    }
}
