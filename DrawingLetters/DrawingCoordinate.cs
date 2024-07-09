namespace DrawingLetters {
    public struct DrawingCoordinate {
        public DrawingCoordinate(bool isMoving, double x, double y) {
            IsMoving = isMoving;
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
        public bool IsMoving { get; }
        public override string ToString() => $"({IsMoving}, {X}, {Y})\n";
    }
}