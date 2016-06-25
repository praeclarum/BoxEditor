using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace BoxEditor
{
    public class Diagram
    {
        public readonly ImmutableArray<Box> Boxes;
        public readonly ImmutableArray<Arrow> Arrows;

        public static Diagram Empty =
            new Diagram(ImmutableArray<Box>.Empty, ImmutableArray<Arrow>.Empty);

        public Diagram(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows)
        {
            Boxes = boxes;
            Arrows = arrows;
        }

        public static Diagram Create(
            IEnumerable<object> boxValues,
            IEnumerable<object> arrowValues,
            Func<object, Box> getBox,
            Func<Func<object, string, PortRef>, object, Arrow> getArrow)
        {
            var boxes = boxValues.Select(getBox).ToImmutableArray();

            var portIndex = new Dictionary<Tuple<object, string>, PortRef>();
            foreach (var b in boxes)
            {
                foreach (var p in b.Ports)
                {
                    portIndex[Tuple.Create(b.Value, p.Id)] = new PortRef (b, p);
                }
            }
            Func<object, string, PortRef> portF = (o, n) => portIndex[Tuple.Create(o, n)];

            var arrows = arrowValues.Select (o => getArrow(portF, o)).ToImmutableArray();

            return new Diagram(boxes, arrows);
        }
    }
}
