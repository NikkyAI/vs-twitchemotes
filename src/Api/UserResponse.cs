using System.Collections.Generic;

namespace TwitchEmotes.Api
{
    public class UserResponse
    {
        public int _total { get; set; }
        public List<User> users { get; set; }
    }

    public class User
    {
        public string _id { get; set; }
        public string display_name { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string logo { get; set; }
    }
}