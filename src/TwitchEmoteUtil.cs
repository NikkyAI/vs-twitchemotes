using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TwitchEmotes.Api;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Path = System.IO.Path;

namespace TwitchEmotes
{
    public class TwitchEmoteUtil
    {
        static readonly HttpClient _client = new HttpClient();

        private readonly Mod _mod;
        private readonly ICoreClientAPI _api;

        public ConcurrentDictionary<string, EmoteInfo> emotes = new();

        public ConcurrentDictionary<string, string[]> emotesByChannel = new();
        public List<string> channels = new ();
        private const string CLIENT_ID = "abe7gtyxbr7wfcdftwyi9i5kej3jnq";

        public TwitchEmoteUtil(Mod mod, ICoreClientAPI api, string[] channels)
        {
            _mod = mod;
            _api = api;

            _mod.Logger.Notification($"loading emotes for {channels.Length} channels");
            var channelResponse = ChannelIdFromName(channels).Result;
            GetEmotesForChannel(0, "Twitch").Wait();
            foreach (var user in channelResponse.users)
            {
                _mod.Logger.Notification($"loading emotes for channel: {user.display_name}");
                GetEmotesForChannel(int.Parse(user._id), user.display_name).Wait();
            }
        }

        public string? GetEmotKeyFromWord(string word)
        {
            if (!emotes.TryGetValue(word, out var emote))
            {
                foreach (var pair in emotes)
                {
                    var emoteCandidate = pair.Value;
                    if (!emoteCandidate.IsRegex) continue;
                    if (Regex.IsMatch(word, "^" + emoteCandidate.Code + "$"))
                    {
                        return emoteCandidate.Key;
                    }
                }

                return null;
            }

            return emote.Key;
        }

        public async Task<bool> LoadEmote(EmoteInfo emote)
        {
            string key = emote.Key;
            string url = emote.Url;
            string filepath = emote.Filepath;
            _mod.Logger.VerboseDebug($"filepath: {filepath}", filepath);
            if (File.Exists(filepath))
            {
                _mod.Logger.VerboseDebug("file for {0} exists already", key);
                return true;
            }
            else
            {
                return await DownloadEmote(emote);
            }
        }

        private async Task<bool> DownloadEmote(EmoteInfo emote)
        {
            string key = emote.Key;
            string url = emote.Url;
            string filepath = emote.Filepath;
            string channelName = emote.ChannelName;
            try
            {
                _mod.Logger.Debug("downloading file for {0} {1}", channelName, key);
                var bytes = await _client.GetByteArrayAsync(url);

                Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                using var stream = new FileStream(filepath, FileMode.Create);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                return true;
            }
            catch (HttpRequestException e)
            {
                _mod.Logger.Error("failed to download {0} from {1}, {2}", key, url, e);
                return false;
            }
        }

        private async Task GetEmotesForChannel(int id, string channelKey)
        {
            _mod.Logger.Notification($"downloading emotes for {channelKey} id: {id}");
            var response = await _client.GetAsync($"https://api.twitchemotes.com/api/v4/channels/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _mod.Logger.Error("no channel found for {0} {1}", channelKey, id);
                return;
            }

            var result = await response.Content.ReadAsStringAsync();
            if (result == null)
            {
                _mod.Logger.Error("error parsing response from {0}", id);
                return;
            }

            // _mod.Logger.Notification("parsing {0}", result);
            var parsed = JsonConvert.DeserializeObject<ChannelResponse>(result);

            var sucessfulEmotes = new List<string>();
            foreach (var emote in parsed.emotes)
            {
                _mod.Logger.Notification($"loading '{emote.code}' from channel {parsed.display_name}");
                var key = AddEmote(parsed.channel_name, emote.id, emote.code, "");
                AddEmote(parsed.channel_name, emote.id, emote.code, "_BW");
                AddEmote(parsed.channel_name, emote.id, emote.code, "_HF");
                AddEmote(parsed.channel_name, emote.id, emote.code, "_SG");
                AddEmote(parsed.channel_name, emote.id, emote.code, "_SQ");
                AddEmote(parsed.channel_name, emote.id, emote.code, "_TK");

                sucessfulEmotes.Add(key);
            }

            emotesByChannel[channelKey] = sucessfulEmotes.ToArray();
            channels.Add(channelKey);
        }

        private string AddEmote(string channel_name, int emote_id, string code, string variant)
        {
            EmoteInfo emote = new(
                emoteUtil: this,
                api: _api,
                mod: _mod,
                channelName: channel_name,
                id: emote_id,
                code: code,
                variant: variant,
                key: code + variant
            );

            emotes[emote.Key] = emote;

            return emote.Key;
        }

        private async Task<UserResponse> ChannelIdFromName(string[] channelNames)
        {
            var response = await _client.GetStringAsync(
                $"https://api.twitch.tv/kraken/users?api_version=5&client_id={CLIENT_ID}&login={string.Join(",", channelNames)}"
            );

            return JsonConvert.DeserializeObject<UserResponse>(response);
        }
    }
}