using System;
using System.Runtime.Remoting.Contexts;
using Vintagestory.API.Client;
using Cairo;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Context = Cairo.Context;

namespace TwitchEmotes
{
    public class TwitchIconComponent : RichTextComponentBase
    {
        private string EmoteKey;
        private CairoFont font;
        private Mod _mod;
        private TwitchEmotes _twitchEmotes;
        public TwitchIconComponent(ICoreClientAPI api, Mod mod, string emoteKey, CairoFont font, TwitchEmotes twitchEmotes)
            : base(api)
        {
            this._mod = mod;
            this.EmoteKey = emoteKey;
            this.font = font;
            this._twitchEmotes = twitchEmotes;
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            // api.Gui.Icons.DrawIcon(ctx, this.EmoteKey, this.BoundsPerLine[0].X, this.BoundsPerLine[0].Y,
            //     GuiElement.scaled(this.font.UnscaledFontsize), GuiElement.scaled(this.font.UnscaledFontsize),
            //     this.font.Color);

            string? filepath = _twitchEmotes.GetEmoteFilepath(EmoteKey);
            if (filepath == null)
            {
                _mod.Logger.Error("failed getting filepath of {0}", EmoteKey);
                return;
            }

            var textureSurface = new ImageSurface(filepath);

            var x = BoundsPerLine[0].X;
            var y = BoundsPerLine[0].Y;
            var width = GuiElement.scaled(this.font.UnscaledFontsize);
            var height = GuiElement.scaled(this.font.UnscaledFontsize);
            
            Matrix matrix1 = ctx.Matrix;
            ctx.Save();
            float imageWidth = textureSurface.Width;
            float imageHeight = textureSurface.Height;
            double num3 = Math.Min(width / imageWidth, height / imageHeight);
            matrix1.Translate((double) x + (double) Math.Max(0.0f, (float) (((double) width - (double) imageWidth * (double) num3) / 2.0)), (double) y + (double) Math.Max(0.0f, (float) (((double) height - (double) imageHeight * (double) num3) / 2.0)));
            matrix1.Scale((double) num3, (double) num3);
            ctx.Matrix = matrix1;

            var pattern = new SurfacePattern(textureSurface);
            ctx.SetSource(pattern);
            ctx.NewPath();
            ctx.MoveTo(0, 0);
            ctx.LineTo(textureSurface.Width, 0);
            ctx.LineTo(textureSurface.Width, textureSurface.Height);
            ctx.LineTo(0, textureSurface.Height);
            ctx.ClosePath();
            // ctx.MoveTo(365.0, 94.0);
            ctx.Tolerance = 0.1;
            ctx.Antialias = Antialias.Default;
            ctx.FillRule = FillRule.Winding;
            ctx.FillPreserve();
            pattern?.Dispose();
            textureSurface.Dispose();

            ctx.Restore();
        }

        public override bool CalcBounds(
            TextFlowPath[] flowPath,
            double currentLineHeight,
            double lineX,
            double lineY)
        {
            this.BoundsPerLine = new LineRectangled[1]
            {
                new LineRectangled(lineX, lineY, GuiElement.scaled(this.font.UnscaledFontsize), GuiElement.scaled(this.font.UnscaledFontsize))
            };
            return false;
        }

        public override void Dispose() => base.Dispose();
    }
}