using Cairo;

namespace TwitchEmotes
{
    public class ImageData
    {
        public readonly byte[] Data;
        public readonly Format Format;
        public readonly int Width;
        public readonly int Height;

        public ImageData(byte[] data, Format format, int width, int height)
        {
            Data = data;
            Format = format;
            Width = width;
            Height = height;
        }
    }
}