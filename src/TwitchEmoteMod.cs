using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;

namespace TwitchEmotes
{
    public class TwitchEmoteMod : ModSystem
    {
        // ReSharper disable InconsistentNaming
        private const string CONFIGNAME = "twitchemotes.json";
        // ReSharper restore InconsistentNaming

        private static Mod _mod;
        private static TwitchEmotes _emotes;
        private static ModConfig? _config;

        private static ModConfig Config => _config ?? throw new NullReferenceException("config is not initialized yet");

        public override void StartClientSide(ICoreClientAPI api)
        {
            _mod = base.Mod;
            LoadConfig(api);
            _emotes = new TwitchEmotes(_mod, api, Config.channels);

            base.Mod.Logger.Debug("registering command");
            VtmlUtil.TagConverters.Add(
                "twitch_icon",
                (coreApi, token, stack, link) =>
                {
                    token.Attributes.TryGetValue("name", out string emoteKey);
                    var filepath = _emotes.GetEmoteFilepath(emoteKey);
                    if (filepath == null)
                    {
                        _mod.Logger.Error("failed getting filepath of {0}", emoteKey);
                        throw new Exception($"failed getting filepath of {emoteKey}");
                    }

                    TwitchIconComponent iconComponent = new(coreApi, _mod, stack.Peek(), emoteKey, filepath);
                    return (RichTextComponentBase) iconComponent;
                }
            );

            api.RegisterCommand(
                "emotes",
                "lists all registered emotes",
                "test",
                (id, args) =>
                {
                    var count = _emotes.emotesByChannel.Values.Sum(list => list.Length);
                    var modifiedCount = _emotes.emotes.Keys.Count - count;
                    api.ShowChatMessage($"emotes: {count} ({modifiedCount})");
                    
                    foreach (var channelKey in Config.channels)
                    {
                        if (!_emotes.emotesByChannel.TryGetValue(channelKey, out var emotes)) continue;
                        var pageSize = 20;
                        api.ShowChatMessage($"{channelKey} ({emotes.Length}): ");
                        api.ShowChatMessage(string.Join(" ", emotes));
                        // foreach (var emote in emotes)
                        // {
                        //     if (!_emotes.emotes.ContainsKey(emote))
                        //     {
                        //         _mod.Logger.Error($"missing {emote}");
                        //     }
                        // }
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
                    var emoteKeys = _emotes.emotes.Keys.Where(k => k.StartsWith(emoteKey)).ToList();
                    Mod.Logger.Debug("all: {0}", string.Join(" ", _emotes.emotes.Keys));
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
                            var filepath = _emotes.GetEmoteFilepath(word);
                            if (filepath != null)
                            {
                                _mod.Logger.Debug("found twitch icon: '{0}'", word);
                                return $"<twitch_icon name=\"{word}\"/>";
                            }

                            foreach (var code in _emotes.emotes.Keys)
                            {
                                if (Regex.IsMatch(word, "^"+code+"$"))
                                {
                                    filepath = _emotes.GetEmoteFilepath(code);
                                    if (filepath != null)
                                    {
                                        _mod.Logger.Debug("found twitch icon: '{0}'", code);
                                        return $"<twitch_icon name=\"{code}\"/>";
                                    }
                                }
                            }

                            return word;
                        }
                    )
                    .Join(delimiter: " ");

                return true;
            }
        }
    }
}