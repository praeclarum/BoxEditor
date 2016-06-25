using NGraphics;

namespace BoxEditor
{
    public class Port
    {
        public readonly string Id;

        public readonly Rect Frame;

        public Port(string id, Rect frame)
        {
            Id = id;
            Frame = frame;
        }
    }
}
