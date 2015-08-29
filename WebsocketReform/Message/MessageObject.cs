using System.Collections.Generic;

namespace WebsocketReform.Message
{
    public enum MessageType
    {
        ControlMessage = 0, ContentMessage = 1,
    }

    public abstract class MessageBase
    {
        public string Sender { get; set; } = string.Empty;
    }

    public enum ContentType
    {
        Text = 0,
    }
    public class ContentMessage : MessageBase
    {
        public string Receiver { get; set; } = string.Empty;
        public ContentType ContentType { get; set; } = ContentType.Text;
    }

    public enum ControlType
    {
        Login = 1,
        UploadGraphic = 2,
        ChangeClass = 3,
        ChangeDomain = 4,
        ChangeUserInfo = 5,
        CreateClass = 6,
    }
    public class ControlMessage : MessageBase
    {
        public ControlType ControlType { get; set; }
        public Dictionary<string, dynamic> Params { get; set; }
    }
}
