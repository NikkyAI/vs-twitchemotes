using System;
using Vintagestory.API.Client;
using Cairo;
using Vintagestory.API.Common;
using Context = Cairo.Context;

namespace TwitchEmotes
{
    public class TwitchIconComponent : RichTextComponentBase
    {
        private readonly CairoFont _font;
        private readonly Mod _mod;
        private readonly string _tooltipText;
        private readonly TextDrawUtil textUtil = new();

        private readonly TwitchEmoteUtil _emotUtilUtil;
        private readonly EmoteInfo _emote;

        private LoadedTexture hoverText;
        private LoadedTexture zoomed;
        private TextLine[] lines;

        public TwitchIconComponent(ICoreClientAPI api, Mod mod, CairoFont font, string emoteKey, string raw,
            TwitchEmoteUtil? emoteUtilUtil)
            : base(api)
        {
            _mod = mod;
            _font = font;
            hoverText = new LoadedTexture(api);
            zoomed = new LoadedTexture(api);

            _emotUtilUtil = emoteUtilUtil;
            if (!_emotUtilUtil.emotes.TryGetValue(emoteKey, out var emote))
            {
                _mod.Logger.Error("cannot get emote for key {0}", _tooltipText);
                return;
            }

            _emote = emote;
            raw = raw.Replace("&gt;", ">").Replace("&lt;", "<");
            var regex = _emote.Code.Replace("\\&gt\\;", ">").Replace("\\&lt\\;", "<");
            _tooltipText = !_emote.IsRegex ? _emote.Code : (raw + " regex: " + regex);
        }

        public override void ComposeElements(Context ctxStatic, ImageSurface surface)
        {
            _mod.Logger.Debug("rendering emote {0}", _tooltipText);


            ImageSurface textureSurface;
            if (_emote.ImageData != null)
            {
                var imageData = _emote.ImageData;
                textureSurface = (ImageSurface) ImageSurface.CreateForImage(imageData.Data, imageData.Format,
                    imageData.Width, imageData.Height);
            }
            else
            {
                // ensure that the download finished
                _emote.DownloadTask.Wait();
                textureSurface = new ImageSurface(_emote.Filepath);

                // TODO store image data
                _emote.ImageData = new ImageData(
                    data: textureSurface.Data,
                    format: textureSurface.Format,
                    width: textureSurface.Width,
                    height: textureSurface.Height
                );
            }

            using var pattern = new SurfacePattern(textureSurface);

            var x = BoundsPerLine[0].X;
            var y = BoundsPerLine[0].Y;
            var width = GuiElement.scaled(this._font.UnscaledFontsize);
            var height = GuiElement.scaled(this._font.UnscaledFontsize);

            Matrix matrix1 = ctxStatic.Matrix;
            ctxStatic.Save();
            float imageWidth = textureSurface.Width;
            float imageHeight = textureSurface.Height;
            double scale = Math.Min(width / imageWidth, height / imageHeight);
            matrix1.Translate(
                x + Math.Max(0.0f, (float) ((width - imageWidth * scale) / 2.0)),
                y + Math.Max(0.0f, (float) ((height - imageHeight * scale) / 2.0))
            );
            matrix1.Scale(scale, scale);
            ctxStatic.Matrix = matrix1;

            ctxStatic.SetSource(pattern);
            ctxStatic.NewPath();
            ctxStatic.MoveTo(0, 0);
            ctxStatic.LineTo(imageWidth, 0);
            ctxStatic.LineTo(imageWidth, imageHeight);
            ctxStatic.LineTo(0, imageHeight);
            ctxStatic.ClosePath();
            ctxStatic.Tolerance = 0.1;
            ctxStatic.Antialias = Antialias.Default;
            ctxStatic.FillRule = FillRule.Winding;
            ctxStatic.FillPreserve();
            ctxStatic.Restore();

            var scaleZoomed = scale * 3;
            using ImageSurface zoomedSurface = new(
                Format.Argb32,
                (int) (imageWidth * scaleZoomed),
                (int) (imageHeight * scaleZoomed)
            );
            using Context zoomedCtx = new(zoomedSurface);
            Matrix matrix2 = zoomedCtx.Matrix;
            matrix2.Scale(scaleZoomed, scaleZoomed);
            zoomedCtx.Matrix = matrix2;
            zoomedCtx.SetSource(pattern);
            zoomedCtx.NewPath();
            zoomedCtx.MoveTo(0, 0);
            zoomedCtx.LineTo(imageWidth, 0);
            zoomedCtx.LineTo(imageWidth, imageHeight);
            zoomedCtx.LineTo(0, imageHeight);
            zoomedCtx.ClosePath();
            zoomedCtx.Tolerance = 0.1;
            zoomedCtx.Antialias = Antialias.Default;
            zoomedCtx.FillRule = FillRule.Winding;
            zoomedCtx.FillPreserve();
            zoomedCtx.Restore();
            api.Gui.LoadOrUpdateCairoTexture(zoomedSurface, false, ref this.zoomed);


            var leftMostX = 999999.0;
            var topMostY = 999999.0;
            double rightMostX = 0.0;
            double bottomMostY = 0.0;
            for (int index = 0; index < this.lines.Length; ++index)
            {
                TextLine line = this.lines[index];
                leftMostX = Math.Min(leftMostX, line.Bounds.X);
                topMostY = Math.Min(topMostY, line.Bounds.Y);
                rightMostX = Math.Max(rightMostX, line.Bounds.X + line.Bounds.Width);
                bottomMostY = Math.Max(bottomMostY, line.Bounds.Y + line.Bounds.Height);
            }

            using ImageSurface hoverSurface = new(Format.Argb32, (int) (rightMostX - leftMostX),
                (int) (bottomMostY - topMostY));
            using Context hoverCtx = new(hoverSurface);
            hoverCtx.SetSourceRGBA(0.0, 0.0, 0.0, 0.75);
            hoverCtx.Paint();
            hoverCtx.Save();
            Matrix matrix = hoverCtx.Matrix;
            matrix.Translate((int) -leftMostX, (int) -topMostY);
            hoverCtx.Matrix = matrix;

            hoverCtx.LineWidth = 1.0;
            // hoverCtx.SetSourceRGBA(this._font.Color);
            hoverCtx.SetSourceRGBA(1.0, 1.0, 1.0, 1.0);
            _font.Color = new[] { 1.0, 1.0, 1.0, 1.0 };
            this.textUtil.DrawMultilineText(hoverCtx, this._font, this.lines);
            hoverCtx.Restore();
            this.api.Gui.LoadOrUpdateCairoTexture(hoverSurface, false, ref this.hoverText);
            textureSurface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime, double renderX, double renderY)
        {
            base.RenderInteractiveElements(deltaTime, renderX, renderY);
            bool flag = false;
            foreach (var rectangled in this.BoundsPerLine)
            {
                if (rectangled.PointInside(this.api.Input.MouseX - renderX,
                    this.api.Input.MouseY - renderY))
                {
                    this.api.Render.Render2DTexturePremultipliedAlpha(this.zoomed.TextureId,
                        this.api.Input.MouseX + 20, this.api.Input.MouseY - this.zoomed.Height, this.zoomed.Width,
                        this.zoomed.Height, 50f);
                    this.api.Render.Render2DTexturePremultipliedAlpha(this.hoverText.TextureId,
                        this.api.Input.MouseX + 20, this.api.Input.MouseY, this.hoverText.Width, this.hoverText.Height,
                        50f);
                }
            }
        }

        public override bool CalcBounds(
            TextFlowPath[] flowPath,
            double currentLineHeight,
            double lineX,
            double lineY
        )
        {
            lines = textUtil.Lineize(this._font, _tooltipText, flowPath, lineX + this.PaddingLeft, lineY);
            BoundsPerLine = new LineRectangled[1]
            {
                new(lineX, lineY,
                    GuiElement.scaled(this._font.UnscaledFontsize),
                    GuiElement.scaled(this._font.UnscaledFontsize)
                )
            };
            return false;
        }

        // public override void OnMouseDown(MouseEvent args)
        // {
        //     foreach (Rectangled rectangled in this.BoundsPerLine)
        //     {
        //         if (rectangled.PointInside((double) args.X, (double) args.Y))
        //         {
        //             args.Handled = true;
        //             this.Trigger();
        //         }
        //     }
        // }

        public override void Dispose()
        {
            base.Dispose();
            zoomed?.Dispose();
            hoverText?.Dispose();
        }
    }
}