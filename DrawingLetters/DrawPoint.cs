namespace DrawingLetters
{
    public class DrawPoint
    {
        public DrawPoint(double x, double y, int distance = -1)
        {
            X = x;
            Y = y;
            Distance = distance;
        }

        public double X { get; set; }
        public double Y { get; set; }
        public int Distance { get; set; }

        public override string ToString() => $"(Distance:{Distance})\n";
    }
}
