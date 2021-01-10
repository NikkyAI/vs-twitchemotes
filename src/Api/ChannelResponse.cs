using System;
using System.Collections.Generic;

namespace TwitchEmotes.Api
{
    public class ChannelResponse
    {
        public string channel_name { get; set; }
        public string channel_id { get; set; }
        public string broadcaster_type { get; set; }
        public List<Emote> emotes { get; set; }
        // public Dictionary<string, BitsEmote> bits_badges { get; set; }
        public string base_set_id { get; set; }
        public string display_name { get; set; }
        public DateTime generated_at { get; set; }
    }
}