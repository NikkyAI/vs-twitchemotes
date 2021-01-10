using System;
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

namespace TwitchEmotes
{
    public struct EmoteInfo
    {
        public readonly string ChannelName;
        public readonly string Url;
        public readonly string Filepath;
        public readonly int Id;
        public readonly string Code;
        public readonly string Variant;
        public readonly bool IsRegex;

        public EmoteInfo(string channelName, string url, string filepath, int id, string code, string variant)
        {
            ChannelName = channelName;
            Url = url;
            Filepath = filepath;
            Id = id;
            Code = code;
            Variant = variant;
            IsRegex = Regex.IsMatch(code, "^[a-zA-Z0-9]*$");
        }
    }

    public class TwitchEmotes
    {
        static readonly HttpClient _client = new HttpClient();

        private readonly Mod _mod;
        private readonly ICoreClientAPI _api;

        public ConcurrentDictionary<string, EmoteInfo> emotes = new();

        public ConcurrentDictionary<string, string[]> emotesByChannel = new();

        public TwitchEmotes(Mod mod, ICoreClientAPI api, string[] channels)
        {
            _mod = mod;
            _api = api;

            _mod.Logger.Notification($"loading emotes for {channels.Length} channels");
            foreach (var channel in channels)
            {
                _mod.Logger.Notification($"loading emotes for channel: {channel}");
                try
                {
                    var channel_id = ChannelIdFromName(channel).Result;
                    if (channel_id != null)
                    {
                        _mod.Logger.Notification($"loading emotes for channel: {channel_id}");
                        GetEmotesForChannel((int) channel_id, channel).Wait();
                    }
                }
                catch (Exception e)
                {
                    _mod.Logger.Error("error: {0}", e);
                }
            }
        }

        public string? GetEmoteFilepath(string emotekey)
        {
            return GetEmoteFilepathAsync(emotekey).Result;
        }

        public async Task<string?> GetEmoteFilepathAsync(string emotekey)
        {
            // TODO: download as required ...
            
            try
            {
                if (!emotes.TryGetValue(emotekey, out var emote))
                {
                    var found = false;
                    foreach (var pair in emotes)
                    {
                        var code = pair.Key;
                        var emoteCandidate = pair.Value;
                        if(!emoteCandidate.IsRegex) continue;
                        if (Regex.IsMatch(emotekey, "^"+code+"$"))
                        {
                            emote = pair.Value;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return null;
                    }
                }

                if (!File.Exists(emote.Filepath))
                {
                    await DownloadEmote(emotekey, emote.Url, emote.Filepath, emote.ChannelName, emote.Code);

                    if (!File.Exists(emote.Filepath))
                    {
                        _mod.Logger.Error("failed to download {0} from {1}", emotekey, emote.Url);
                        return null;
                    }
                }

                return emote.Filepath;
            }
            catch (Exception e)
            {
                _mod.Logger.Error("failed to retrieve {0} exception: {1}", emotekey, e);
                return null;
            }
        }

        private async Task DownloadEmote(string key, string url, string filepath, string channel_name, string code)
        {
            try
            {
                _mod.Logger.VerboseDebug($"filepath: {filepath}", filepath);
                if (File.Exists(filepath))
                {
                    _mod.Logger.VerboseDebug("file for {0} exists already", code);
                }
                else
                {
                    _mod.Logger.Debug("downloading file for {0} {1}", channel_name, key);
                    var bytes = await _client.GetByteArrayAsync(url);

                    Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                    File.WriteAllBytes(filepath, bytes);
                }
            }
            catch (Exception e)
            {
                _mod.Logger.Error("failed to download {0} from {1}, {2}", code, url, e);
                throw;
            }
        }

        private void AddEmote(string channel_name, int emote_id, string code, string variant)
        {
            emotes[code + variant] = new EmoteInfo(
                filepath: GenerateFilepathForEmote(channel_name, emote_id, code, variant),
                channelName: channel_name,
                url: $"https://static-cdn.jtvnw.net/emoticons/v2/{emote_id + variant}/default/dark/3.0",
                id: emote_id,
                code: code,
                variant: variant
            );
        }

        private async Task GetEmotesForChannel(int id, string channelKey)
        {
            _mod.Logger.Notification($"downloading emotes for {id}");
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
                try
                {
                    _mod.Logger.Notification($"loading {parsed.channel_name} {emote.code} from id {id}");
                    AddEmote(parsed.channel_name, emote.id, emote.code, "");
                    AddEmote(parsed.channel_name, emote.id, emote.code, "_BW");
                    AddEmote(parsed.channel_name, emote.id, emote.code, "_HF");
                    AddEmote(parsed.channel_name, emote.id, emote.code, "_SG");
                    AddEmote(parsed.channel_name, emote.id, emote.code, "_SQ");
                    AddEmote(parsed.channel_name, emote.id, emote.code, "_TK");
                    sucessfulEmotes.Add(emote.code);
                }
                catch (Exception e)
                {
                    _mod.Logger.Error("encountered error loading {0} {1} exception: {2}", channelKey, emote.code, e);
                    continue;
                }
            }

            emotesByChannel[channelKey] = sucessfulEmotes.ToArray();
        }

        private async Task<int?> ChannelIdFromName(string channel_name)
        {
            if (channel_name.ToLower() == "twitch") return 0;
            var handler = new HttpClientHandler() {AllowAutoRedirect = false};

            // Create an HttpClient object
            HttpClient client = new HttpClient(handler);
            try
            {
                HttpRequestMessage httpRequest = new(HttpMethod.Post, "https://www.twitchemotes.com/search/channel");

                // httpRequest.Content = new StringContent(xml.Document.ToString(), Encoding.UTF8, "application/vnd.citrix.sessionstate+xml");
                httpRequest.Headers.Referrer = new Uri("https://www.twitchemotes.com/");

                httpRequest.Content = new FormUrlEncodedContent(
                    nameValueCollection: new KeyValuePair<string, string>[] {new("query", channel_name)}
                );
                var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

                _mod.Logger.Notification("response status: {0}", response.StatusCode);
                var location = response.Headers.Location;
                _mod.Logger.Notification("location for {0}: {1}", channel_name, location);

                if (location == null)
                {
                    _mod.Logger.Error("failed getting channel id for {0}", channel_name);
                    return null;
                }

                string idString = location.ToString().Substring("/channel/".Length + 1);
                _mod.Logger.Notification("trying to parse {0}", idString);

                return int.Parse(idString);
            }
            catch (Exception e)
            {
                _mod.Logger.Error("failed getting channel id for {0} exception: \n{1}", channel_name, e);
                throw;
            }
            finally
            {
                client.Dispose();
            }
        }

        private string GenerateFilepathForEmote(string channel_name, int emote_id, string code, string variant)
        {
            var cleanedName = code
                .Replace("\\&gt", ">")
                .Replace("\\&lt", "<")
                .Replace("\\", "");
            if (code.Contains("[") && code.Contains("]"))
            {
                cleanedName = emote_id.ToString();
            }

            var filename = cleanedName + variant;
            var invalids = Path.GetInvalidFileNameChars();
            filename = String.Join("_", filename.Split(invalids, StringSplitOptions.RemoveEmptyEntries))
                .TrimEnd('.');
            return Path.Combine(_api.DataBasePath, "Cache", "twitchemotes", channel_name,
                filename + ".png");
        }
    }
}