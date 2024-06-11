namespace DrawingLetters
{
    public class DrawPoint
    {
        public DrawPoint(float x, float y, int distance = -1)
        {
            X = x;
            Y = y;
            Distance = distance;
        }

        public float X { get; set; }
        public float Y { get; set; }
        public int Distance { get; set; }

        public override string ToString() => $"(Distance:{Distance})\n";
    }
}
