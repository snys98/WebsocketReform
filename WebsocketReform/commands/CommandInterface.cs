//using System.Collections.Generic;
//using WebsocketReform.Objects;

//namespace WebsocketReform.commands
//{
//    public class CommandInterface
//    {
//        public static void GetDomainInfo()
//        { 
        
//        }
//        //因为涉及到区域的默认教室(未注册到教室列表),所以需要domainID来保证教室唯一性
//        public static void GetUsers(User user, string message, out object target, out string messageToUser, out string messageToOthers)
//        {
//            string[] _params = GetParams(message);
//            string domainID = _params[0];
//            string classID = _params[1];
//            Domain domain = DomainList.FindDomainByID(domainID);
//            Class cls = null;
//            List<User> users;
//            string result = string.Format("C;{0};;",user.Id);
//            foreach (var item in domain.ClassDict)
//            {
//                if (item.Id == classID)
//                {
//                    cls = item;
//                }
//            }
//            if (cls != null)
//            {
//                users = cls.UserDict;
//                result += "GetUsersSuccess";
//                foreach (var item in users)
//                {
//                    result += string.Format("|{0}{1}{2}{3}{4}{5}",item.Id,item.Name,item.NickName,item.Description,item.State,item.Sign);
//                }
//                target = null;
//                messageToOthers = "";
//                messageToUser = result;
//            }
//            else
//            {
//                result += "GetUsersFailed|";
//                target = null;
//                messageToUser = result;
//                messageToOthers = "";
//            }
//        }
//    }
//}
