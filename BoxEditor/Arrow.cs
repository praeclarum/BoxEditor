namespace BoxEditor
{
    public class PortRef
    {
        public readonly Box Box;
        public readonly Port Port;
        public PortRef(Box box, Port port)
        {
            Box = box;
            Port = port;
        }
    }

    public class Arrow
    {
        public readonly object Value;

        public readonly PortRef From;
        public readonly PortRef To;

        public Arrow(object value, PortRef @from, PortRef @to)
        {
            Value = value;
            From = @from;
            To = @to;
        }
    }
}
