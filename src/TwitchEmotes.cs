using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TwitchEmotes.Api;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace TwitchEmotes
{
    public class TwitchEmotes
    {
        static readonly HttpClient client = new HttpClient();
        
        private readonly Mod _mod;
        private readonly ICoreClientAPI _api;

        public System.Collections.Concurrent.ConcurrentDictionary<string, string> emotes =
            new ConcurrentDictionary<string, string>();
        public System.Collections.Concurrent.ConcurrentDictionary<string, string> modifiedEmotes =
            new ConcurrentDictionary<string, string>();


        public TwitchEmotes(Mod mod, ICoreClientAPI api)
        {
            _mod = mod;
            _api = api;

            Task.Run(async () =>
                {
                    GetEmotesForChannel(0);
                    GetEmotesForChannel(96743665);
                    GetEmotesForChannel(583196385);
                }
            );
        }

        public string? GetEmoteFilepath(string emotekey)
        {
            if (emotes.TryGetValue(emotekey, out var emote))
            {
                return emote;
            }
            else if (modifiedEmotes.TryGetValue(emotekey, out var modifiedEmote))
            {
                return modifiedEmote;
            }

            return null;
        }

        private async void DownloadEmote(string channel_name, int emote_id, string key, string variant, string url)
        {
            try {
                var filepath = Path.Combine(_api.DataBasePath, "Cache", "twitchemotes", channel_name, $"{emote_id+variant}.png");
                if (File.Exists(filepath))
                {
                    _mod.Logger.Debug("file for {0} exists already", key);
                    if (variant == "")
                    {
                        emotes[key] = filepath;
                    }
                    else
                    {
                        modifiedEmotes[key] = filepath;
                    }
                    return;
                }
                
                var bytes = await client.GetByteArrayAsync(url);

                Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                File.WriteAllBytes(filepath, bytes);

                if (variant == "")
                {
                    emotes[key] = filepath;
                }
                else
                {
                    modifiedEmotes[key] = filepath;
                }
            } catch(Exception e) {
                _mod.Logger.Error("failed to download {0} from {1}, {2}", key, url, e);
            }
        }

        private async void GetEmotesForChannel(int id)
        {
            _mod.Logger.Notification($"downloading emotes for {id}");
            var response = await client.GetAsync($"https://api.twitchemotes.com/api/v4/channels/{id}");
            var result = await response.Content.ReadAsStringAsync();
            if (result == null)
            {
                _mod.Logger.Error("error parsing response from {0}", id);
                return;
            }
            // _mod.Logger.Notification("parsing {0}", result);
            var parsed = JsonConvert.DeserializeObject<ChannelResponse>(result);
            
            foreach (var emote in parsed.emotes)
            {
                _mod.Logger.Notification($"storing {emote.code} from id {id}");
                // DownloadEmote
                DownloadEmote(parsed.channel_name, emote.id, emote.code, "",$"https://static-cdn.jtvnw.net/emoticons/v2/{emote.id}/default/dark/1.0");
                DownloadEmote(parsed.channel_name, emote.id, emote.code+"_BW" ,"_BW", $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.id}_BW/default/dark/1.0");
                DownloadEmote(parsed.channel_name, emote.id, emote.code+"_HF" ,"_HF", $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.id}_HF/default/dark/1.0");
                DownloadEmote(parsed.channel_name, emote.id, emote.code+"_SG" ,"_SG", $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.id}_SG/default/dark/1.0");
                DownloadEmote(parsed.channel_name, emote.id, emote.code+"_SQ" ,"_SQ", $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.id}_SQ/default/dark/1.0");
                DownloadEmote(parsed.channel_name, emote.id, emote.code+"_TK" ,"_TK", $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.id}_TK/default/dark/1.0");
            }
        }
    }
}