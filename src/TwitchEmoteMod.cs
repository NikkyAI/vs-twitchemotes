using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace TwitchEmotes
{
    public class TwitchEmoteMod : ModSystem
    {
        // ReSharper disable InconsistentNaming
        private const string CONFIGNAME = "twitchemotes.json";
        // ReSharper restore InconsistentNaming

        private static Mod _mod;
        private static ModConfig? _config;
        private static TwitchEmoteUtil _emoteUtil;

        private static ModConfig Config => _config ?? throw new NullReferenceException("config is not initialized yet");

        public override void StartClientSide(ICoreClientAPI api)
        {
            _mod = base.Mod;
            LoadConfig(api);
            _emoteUtil = new TwitchEmoteUtil(_mod, api, Config.channels);

            base.Mod.Logger.Debug("registering command");
            VtmlUtil.TagConverters.Add(
                "twitch_icon",
                (coreApi, token, stack, link) =>
                {
                    if (!token.Attributes.TryGetValue("emotekey", out string emoteKey))
                    {
                        _mod.Logger.Error("missing 'emoteKey' in twitch_icon tag, attributes: {0}", string.Join(" ", token.Attributes.Keys));
                        return new IconComponent(coreApi, "none", stack.Peek());
                    }
                    if (!token.Attributes.TryGetValue("raw", out string? raw))
                    {
                        raw = null;
                    }

                    if (!_emoteUtil.emotes.TryGetValue(emoteKey, out var emote))
                    {
                        _mod.Logger.Error("missing '{0}' in emotes", emoteKey);
                        return new IconComponent(coreApi, "none", stack.Peek());
                    }

                    var success = emote.DownloadTask.Result;
                    if (success != true)
                    {
                        _mod.Logger.Error("emote '{0}' marked as failing", emoteKey);
                        return new IconComponent(coreApi, "none", stack.Peek());
                    }
                    
                    TwitchIconComponent iconComponent = new(coreApi, _mod, stack.Peek(), emoteKey, raw, _emoteUtil);
                    return (RichTextComponentBase) iconComponent;
                }
            );

            api.RegisterCommand(
                "emotes",
                "lists all registered emotes",
                "test",
                (id, args) =>
                {
                    var count = _emoteUtil.emotesByChannel.Values.Sum(list => list.Length);
                    var modifiedCount = _emoteUtil.emotes.Keys.Count - count;
                    api.ShowChatMessage($"emotes: {count} ({modifiedCount})");
                    
                    foreach (var channelKey in _emoteUtil.channels)
                    {
                        if (!_emoteUtil.emotesByChannel.TryGetValue(channelKey, out var emotes)) continue;
                        api.ShowChatMessage($"{channelKey} ({emotes.Length}): ");
                        _mod.Logger.Error("emotes: {0}", string.Join(" ", emotes));
                        api.ShowChatMessage(string.Join(" ", emotes));
                    }
                }
            );
            api.RegisterCommand(
                "emotevariants",
                "lists all variants of a emote",
                "emotevariants <emote>",
                (id, args) =>
                {
                    var emoteKey = args.PopWord();
                    if (emoteKey == null)
                    {
                        Mod.Logger.Error("argument must be a emote");
                        return;
                    }
                    Mod.Logger.Debug("executing .emotevariants {0}", emoteKey);
                    var emoteKeys = _emoteUtil.emotes.Keys.Where(k => k.StartsWith(emoteKey)).ToList();
                    Mod.Logger.Debug("all: {0}", string.Join(" ", _emoteUtil.emotes.Keys));
                    Mod.Logger.Debug("found {0} emotes", emoteKeys.Count);
                    emoteKeys.Sort();
                    
                    api.ShowChatMessage(string.Join(" ", emoteKeys));
                }
            );

            var harmony = new Harmony("twitchemotes.fix");
            harmony.PatchAll();
            _mod.Logger.Debug("patches applied");
        }

        private void LoadConfig(ICoreClientAPI Api)
        {
            try
            {
                _config = Api.LoadModConfig<ModConfig>(CONFIGNAME);
            }
            catch (Exception e)
            {
                Mod.Logger.Error("Failed to load mod config! {0}", e);
                return;
            }

            if (_config == null)
            {
                Mod.Logger.Notification($"non-existant modconfig at 'ModConfig/{CONFIGNAME}', creating default...");
                _config = new ModConfig();
            }

            Api.StoreModConfig(_config, CONFIGNAME);
        }

        [HarmonyPatch(typeof(HudDialogChat), "OnNewServerToClientChatLine")]
        class OnNewServerToClientChatLinePatch
        {
            static bool Prefix(
                ref int groupId,
                ref string message,
                ref EnumChatType chattype,
                ref string data
            )
            {
                _mod.Logger.Debug("OnNewServerToClientChatLine type: {0} message: {1}", chattype, message);
                if (message.StartsWith(".")) return true;
                message = message
                    .Split(" ".ToCharArray())
                    .Select(word =>
                        {
                            // TODO word to emoteKey
                            var emoteKey = _emoteUtil.GetEmotKeyFromWord(word);
                            if (emoteKey == null) return word;
                            //TODO somehow do not block here
                            _mod.Logger.Debug("found twitch icon: '{0}'", emoteKey);
                            return $"<twitch_icon emotekey=\"{emoteKey}\" raw=\"{word}\"/>";
                        }
                    )
                    .Join(delimiter: " ");

                return true;
            }
        }
    }
}