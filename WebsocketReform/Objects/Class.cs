using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WebsocketReform.Objects
{
    public struct Graphic
    {
        public int Sid { get; set; }
        public string Content { get; set; }
    }

    public class Class
    {
        private List<Graphic> classGraph = new List<Graphic>(); //教室图形消息

        public int CurrentGID = 0;

        public Class(string id, Domain belonging)
        {
            Id = id;
            this.Domain = belonging;
        }

        public string Id { get;}

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public string Password { get; set; } = "";

        public int CurNum => this.UserDict.Count;
        public int MaxNum { get; set; } = 100;
        public User Owner { get; set; }

        public Dictionary<string,User> UserDict { get; set; } = new Dictionary<string, User>();

        public Domain Domain { get; set; }

        public List<Graphic> ClassGraph
        {
            get
            {
                return classGraph;
            }
            set
            {
                classGraph = value;
                classGraph = classGraph.OrderBy(p => p.Sid).ToList<Graphic>();
            }
        }
        public bool HasPwd
        {
            get
            {
                if (Password == "")
                {
                    return false;
                }
                return true;
            }
        }

        public bool IsFull => CurNum >= MaxNum;
        public string DraftInfo {
            get
            {
                //result.Remove(result.LastIndexOf("|"));
                return UserDict.Aggregate("", (current, item) => current + $"{item.Value.Id},").TrimEnd(',');
            }
        }

        public string GetGraphic(int sid)
        {
            return classGraph[sid].Content;
        }

        public override string ToString()
        {
            return this.Id+":{"+this.UserDict+"}";
        }

        internal void ClearGraphic()
        {
            this.ClassGraph.Clear();
            this.CurrentGID = 0;
        }

        public void BroadCast(string messageToSend,User exclude=null)
        {
            foreach (KeyValuePair<string, User> keyValuePair in UserDict)
            {
                if (keyValuePair.Value!= exclude)
                {
                    keyValuePair.Value.PushMessage(messageToSend);
                }
            }
        }

        public void Dismiss()
        {
            this.Owner.OwnedClasses.Remove(this);
            var usersToNotify = UserDict.Select(userPair => userPair.Value).ToList();
            foreach (var user in usersToNotify)
            {
                user.Class = this.Domain.DefaultClass;
                this.Domain.DefaultClass.UserDict.Add(user.Id, user);
            }
        }

        public void StoreGraphic(string content)
        {
            this.ClassGraph.Add(new Graphic() { Sid = ++CurrentGID, Content = content });
        }

        public void DelLastGraph()
        {
            try
            {
                this.ClassGraph.RemoveAt(--this.CurrentGID);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return;
            }
        }
    }
}
