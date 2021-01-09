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
        private static Mod _mod;
        private static TwitchEmotes _emotes;

        public override void StartClientSide(ICoreClientAPI api)
        {
            _mod = base.Mod;
            _emotes = new TwitchEmotes(_mod, api);
            base.Mod.Logger.Debug("registering command");
            VtmlUtil.TagConverters.Add(
                "twitch_icon",
                (coreApi, token, stack, link) =>
                {
                    token.Attributes.TryGetValue("name", out string emoteKey);
                    TwitchIconComponent iconComponent = new TwitchIconComponent(coreApi, base.Mod, emoteKey, stack.Peek(), _emotes);
                    return (RichTextComponentBase) iconComponent;
                }
            );

            api.RegisterCommand(
                "emotes",
                "lists all registered emotes",
                "test",
                (id, args) =>
                {
                    var pageSize = 25;
                    var count = _emotes.emotes.Keys.Count;
                    var modifiedCount = _emotes.modifiedEmotes.Keys.Count;
                    api.ShowChatMessage($"emotes: {count} ({modifiedCount})");
                    for (var i = 0; i < count / pageSize; i++)
                    {
                        var list = _emotes.emotes.Keys.Skip(i * pageSize).Take(pageSize);
                        api.ShowChatMessage(string.Join(" ", list));
                    }
                }
            );

            var harmony = new Harmony("twitchemote.fix");
            harmony.PatchAll();
            _mod.Logger.Debug("patches applied");
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
                message = message
                    .Split(" ".ToCharArray())
                    .Select(part =>
                        {
                            _mod.Logger.Debug("testing: '{0}'", part);
                            if (_emotes.emotes.ContainsKey(part) || _emotes.modifiedEmotes.ContainsKey(part))
                            {
                                return $"<twitch_icon name=\"{part}\"/>";
                            }
                            else
                            {
                                return part;
                            }
                        }
                    )
                    .Join(delimiter: " ");

                return true;
            }
        }
    }
}