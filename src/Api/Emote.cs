using System;
using Newtonsoft.Json;

namespace TwitchEmotes.Api
{
    public struct Emote    {
        public string code;
        public int id;
        public int emoticon_set;
        
        public Emote(string code, int id, int emoticon_set) {
           this.code = code;
           this.id = id;
           this.emoticon_set = emoticon_set;
        }
        
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}