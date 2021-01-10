using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace TwitchEmotes
{
    public class EmoteInfo
    {
        public readonly string ChannelName;
        public readonly string Url;
        public readonly string Filepath;
        public readonly int Id;
        public readonly string Code;
        public readonly string Variant;
        public readonly string Key;
        public readonly bool IsRegex;
        public readonly Task<bool> DownloadTask;

        public ImageData? ImageData;

        public EmoteInfo(TwitchEmoteUtil emoteUtil, ICoreClientAPI api, Mod mod, string channelName, int id, string code,
            string variant, string key)
        {
            ChannelName = channelName;
            Url = $"https://static-cdn.jtvnw.net/emoticons/v2/{id + variant}/default/dark/3.0";
            Id = id;
            Code = code;
            Variant = variant;
            IsRegex = !Regex.IsMatch(code, "^[a-zA-Z0-9]*$");
            Key = IsRegex ? $"emote_{id}" + Variant : key;
            Filepath = GenerateFilepathForEmote(api.DataBasePath, channelName, Key);
            // Key = IsRegex ? key.Replace("\\", "") : key;
            ImageData = null;

            DownloadTask = Task.Run(async () =>
            {
                try
                {
                    return await emoteUtil.LoadEmote(this);
                }
                catch (Exception e)
                {
                    mod.Logger.Error("unhandled exception loading {0} exception: {1}", Key, e);
                    return false;
                }
            });
            // DownloadTask.Wait();
        }

        private static string GenerateFilepathForEmote(string dataBasePath, string channelName, string key)
        {
            var filename = key;
            var invalids = Path.GetInvalidFileNameChars();
            filename = String.Join("_", filename.Split(invalids, StringSplitOptions.RemoveEmptyEntries))
                .TrimEnd('.');
            return Path.Combine(dataBasePath, "Cache", "twitchemotes", channelName,
                filename + ".png");
        }
    }
}