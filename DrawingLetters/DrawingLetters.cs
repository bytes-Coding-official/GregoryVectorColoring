using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DrawingLetters {
    public partial class DrawingLetters : Form {
        private Dictionary<DrawPoint, List<DrawPoint>> allNeighbors;
        private DrawingCoordinate[] originalCoords; //hier die 1/4 rate nehmen für die gelben Punkte.
        private DrawingCoordinate[] scaledCoords;
        private List<DrawPoint> drawnPoints;
        private int distance; //hier die LedDistanz nutzen
        private int distanceLED;
        private float dotRadius = 2.5f;
        private double maxX, maxY, minX, minY;
        private float height;
        private List<PointF> linePoints = new();

        public DrawingLetters() {
            InitializeComponent();
            Resize += CanvasResize;
            MouseMove += CanvasMouseMove;
            goButton.MouseClick += GoButtonMouseClick;
        }

        private void ReadFileButtonClick(object sender, EventArgs e) {
            using var openFileDialog = new OpenFileDialog {
                Title = "Open CSV File",
                Filter = "csv files (*.csv)|*.csv",
                CheckFileExists = true,
                CheckPathExists = true,
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK) return;
            MessageBox.Show(openFileDialog.FileName);
            LoadDataFromFile(openFileDialog.FileName);
        }

        private void LoadDataFromFile(string csvPath) {
            var idx = 0;
            originalCoords = [];
            var coordsAsText = new StringBuilder();
            var result = MessageBox.Show($"Möchten Sie wirklich die Datei {csvPath} laden?", "Datei laden", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes) {
                var provider = new NumberFormatInfo {
                    NumberDecimalSeparator = ".",
                    NumberGroupSeparator = ","
                };

                using var stream = new StreamReader(csvPath);

                string line;

                if (distance > 0) {
                    distance = 0;
                    inputDistance.Text = "0";
                }

                while ((line = stream.ReadLine()) != null) {
                    if (idx > originalCoords.Length - 1) {
                        Array.Resize(ref originalCoords, originalCoords.Length + 1);
                    }

                    var parts = line.Split(",");

                    if (parts.Length != 3) continue;
                    var isMoving = parts[0].Trim().Equals("M");
                    var x = Convert.ToDouble(parts[1].Trim(), provider);
                    var y = Convert.ToDouble(parts[2].Trim(), provider);

                    originalCoords[idx++] = new DrawingCoordinate(isMoving, x, y);
                    coordsAsText.Append(new DrawingCoordinate(isMoving, x, y));
                }

                scaledCoords = new DrawingCoordinate[originalCoords.Length];

                maxX = originalCoords.Max(coord => coord.X);
                maxY = originalCoords.Max(coord => coord.Y);
                minX = originalCoords.Min(coord => coord.X);
                minY = originalCoords.Min(coord => coord.Y);

                ScaleDrawingCoordinates();
                visualizeCoordinates.Text = coordsAsText.ToString();
                canvas.Invalidate();
            }
            else {
                MessageBox.Show("Ladevorgang abgebrochen.", "Abgebrochen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ScaleDrawingCoordinates() {
            //Berechnung des Skalierungsfaktors basierend auf dem Canvas-Größe
            var scaleFactor = GetScaleFactor();

            for (var i = 0; i < originalCoords.Length; i++) {
                var scaledX = ScaleCoordinate(originalCoords[i].X, scaleFactor);
                var scaledY = ScaleCoordinate(originalCoords[i].Y, scaleFactor);

                scaledCoords[i] = new DrawingCoordinate(originalCoords[i].IsMoving, scaledX, scaledY);
            }
        }

        private void LetterDrawingPaint(object sender, PaintEventArgs e) {
            if (scaledCoords == null || scaledCoords.Length == 0) {
                return;
            }

            linePoints.Clear();
            var blackPen = new Pen(Color.Black, 5);
            PointF? startPoint = null;

            foreach (var coordinate in scaledCoords) {
                var mirroredY = mirroringYCoordinate(coordinate.Y);
                var currentPoint = new PointF((float) coordinate.X, (float) mirroredY);

                if (coordinate.IsMoving) {
                    startPoint = currentPoint;
                }
                else if (startPoint.HasValue) {
                    e.Graphics.DrawLine(blackPen, startPoint.Value, currentPoint);
                    linePoints = GetPointsBetween(startPoint.Value, currentPoint);
                    startPoint = currentPoint;
                }
            }

            DrawPoints(e.Graphics);
        }

        private float Distance(PointF p1, PointF p2) {
            // Euklidische Distanz
            return (float) Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }


        private List<PointF> GetPointsBetween(PointF start, PointF end) {
            var distance = Distance(start, end);
            // Die Anzahl der Schritte bestimmen
            var steps = (int) Math.Ceiling(distance);

            // Die Differenz für jeden Schritt berechnen
            var deltaX = (end.X - start.X) / steps;
            var deltaY = (end.Y - start.Y) / steps;

            for (var i = 0; i <= steps; i++) {
                var x = start.X + (deltaX * i);
                var y = start.Y + (deltaY * i);
                linePoints.Add(new PointF(x, y));
            }

            return linePoints;
        }


        private void DrawPoints(Graphics g) {
            if (distance < 10) {
                return;
            }

            distanceLED = distance;

            if (distanceLED >= 100) {
                distanceLED /= 10;
            }

            distance /= 10;

            height = distance * (float) Math.Sqrt(3) / 2f;

            drawnPoints = [];

            for (float y = 0; y < maxY; y += height) {
                var offsetX = y / height % 2 * (distance / 2f);

                for (var x = 0; x < maxX; x += distance) {
                    var point = new PointF(x + offsetX, y);
                    var isPointInShape = GregoryCasting(point);

                    if (isPointInShape) {
                        drawnPoints.Add(new DrawPoint(point.X, point.Y));
                    }
                }
            }

            allNeighbors = new();

            FillNeighborMap();
            pointPosition.Clear();
            ChangeDistanceOfDrawPoint();
            DrawCenterLine(g);
            distance *= 10;
        }

        private void ChangeDistanceOfDrawPoint() {
            ChangeDistanceFromMinusOneToZero();
            var counterDistance = 0;

            do {
                foreach (var kvp in allNeighbors) {
                    var keyPoint = kvp.Key;
                    var valuePoints = kvp.Value;

                    GetDistancesAndChangeThem(keyPoint, valuePoints, counterDistance);
                }

                counterDistance++;
            } while (ContainsDistanceMinusOne());
        }

        private int GetHighestDistance() {
            var highestKeyDistance = allNeighbors.Keys.Max(point => point.Distance);
            return highestKeyDistance;
        }

        private void DrawCenterLine(Graphics graphic) {
            var maxDistance = GetHighestDistance();
            foreach (var keyPoint in allNeighbors.Select(kvp => kvp.Key)) {
                DrawSinglePoint(graphic, keyPoint, dotRadius);
            }

            var counterDistanceUp = 0;
            Stack<DrawPoint> centerLinePoints = new();

            while (counterDistanceUp <= maxDistance) {
                foreach (var actualPoint in from kvp in allNeighbors
                         let actualPoint = kvp.Key
                         let neighbors = kvp.Value
                         where actualPoint.Distance == counterDistanceUp &&
                               CheckIfHigherDistanceNotExist(actualPoint, neighbors)
                         select actualPoint) {
                    centerLinePoints.Push(actualPoint);
                }

                counterDistanceUp++;
            }

            HashSet<DrawPoint> ledPoints = [];
            foreach (var linePoint in centerLinePoints) {
                var nichtUeberlappend = new HashSet<DrawPoint>(ledPoints).All(donePoint => linePoint.X + distanceLED < donePoint.X - distanceLED || linePoint.X - distanceLED > donePoint.X + distanceLED || linePoint.Y + distanceLED < donePoint.Y - distanceLED || linePoint.Y - distanceLED > donePoint.Y + distanceLED);
                if (nichtUeberlappend) ledPoints.Add(linePoint);
            }

            //& Färbe und zeichne die punkte ein
            foreach (var point in centerLinePoints) {
                DrawCenterPoint(graphic, point);
            }

            //fertige LED-Points
            var centerPointList = ledPoints.ToList();

            var finalPoints = new HashSet<DrawPoint>(centerPointList); // Assuming you want to start with all yellowPoints.
            var pointsToRemove = new HashSet<DrawPoint>();

            foreach (var yellowPoint in finalPoints.Where(yellowPoint => linePoints.Any(originalCoord => Math.Abs(originalCoord.X - yellowPoint.X) < distanceLED/4.0 &&
                                                                                                         Math.Abs(originalCoord.Y - yellowPoint.Y) < distanceLED/4.0))) {
                pointsToRemove.Add(yellowPoint);
            }

            // Remove the identified linePoints from finalPoints
            foreach (var pointToRemove in pointsToRemove) {
                finalPoints.Remove(pointToRemove);
            }

            DrawLEDPoints(finalPoints.ToList(), graphic);
        }

        private void DrawLEDPoints(List<DrawPoint> centerPointList, Graphics graphic) {
            var halfDistance = distanceLED / 2;
            double dx;
            double dy;

            var newPoints = new List<DrawPoint>();

            for (var i = 0; i < centerPointList.Count; i++) {
                for (var j = i + 1; j < centerPointList.Count; j++) {
                    dx = centerPointList[j].X - centerPointList[i].X;
                    dy = centerPointList[j].Y - centerPointList[i].Y;
                    var pointDistance = Math.Sqrt(dx * dx + dy * dy);

                    if (Math.Abs(halfDistance - pointDistance) <= distanceLED + 1) {
                        centerPointList.Remove(centerPointList[j]);
                    }
                }

                newPoints.Add(centerPointList[i]);
            }

            foreach (var p in newPoints) {
                DrawCenterPoint(graphic, p, true);
            }
        }

        private bool CheckIfHigherDistanceNotExist(DrawPoint point, List<DrawPoint> allNeighbors) {
            return allNeighbors.TrueForAll(neighborPoint => neighborPoint.Distance <= point.Distance);
        }

        private bool ContainsDistanceMinusOne() {
            var keysWithMinusOne = allNeighbors.Keys.Any(key => key.Distance == -1);

            var valuesWithMinusOne = allNeighbors.Values.Any(list => list.Any(value => value.Distance == -1));

            return keysWithMinusOne || valuesWithMinusOne;
        }

        private void ChangeDistanceFromMinusOneToZero() {
            foreach (var neighbor in allNeighbors.Select(kvp => kvp.Key)) {
                if (allNeighbors[neighbor].Count() < 6) {
                    neighbor.Distance = 0;
                }
            }
        }

        private void GetDistancesAndChangeThem(DrawPoint keypoint, List<DrawPoint> neighbors, int countDistance) {
            if (keypoint.Distance != countDistance) return;
            foreach (var actualNeighbor in neighbors.Where(actualNeighbor => actualNeighbor.Distance == -1)) {
                actualNeighbor.Distance = countDistance + 1;
            }
        }

        private void FillNeighborMap() {
            allNeighbors.Clear();

            foreach (var middlePoint in drawnPoints) {
                foreach (var neighbor in drawnPoints) {
                    AddNeighborIfClose(middlePoint, neighbor, distance, 0); // rechter Punkt
                    AddNeighborIfClose(middlePoint, neighbor, -distance, 0); // linker Punkt
                    AddNeighborIfClose(middlePoint, neighbor, distance / 2, height); // oberer rechter Punkt
                    AddNeighborIfClose(middlePoint, neighbor, -distance / 2, height); // oberer linker Punkt
                    AddNeighborIfClose(middlePoint, neighbor, distance / 2, -height); // unterer rechter Punkt
                    AddNeighborIfClose(middlePoint, neighbor, -distance / 2, -height); // unterer linker Punkt
                }
            }
        }

        private void AddNeighborIfClose(DrawPoint firstPoint, DrawPoint secondPoint, float offsetX, float offsetY) {
            if (!(Math.Abs(secondPoint.X - (firstPoint.X + offsetX)) < 1) || !(Math.Abs(secondPoint.Y - (firstPoint.Y + offsetY)) < 1)) return;
            if (!allNeighbors.TryGetValue(firstPoint, out var neighbor)) {
                neighbor = [];
                allNeighbors.Add(firstPoint, neighbor);
            }

            neighbor.Add(secondPoint);
        }

        private void DrawSinglePoint(Graphics g, DrawPoint point, float radius) {
            var drawColor = new SolidBrush(Color.Black);

            var scaleFactor = GetScaleFactor();
            point.Y = ChangeYPointCoordinate(point.Y);
            point.X *= scaleFactor;

            g.FillEllipse(drawColor, (float) point.X, (float) point.Y, radius * 2, radius * 2);
        }


        private void DrawCenterPoint(Graphics g, DrawPoint point, bool isRed = false) {
            var drawColor = new SolidBrush(isRed ? Color.Red : Color.Yellow);

            if (isRed) {
                distance *= 10;
                var drawDistance = distance >= 100 ? distance / 10 : distance;
                g.FillEllipse(drawColor, (float) point.X, (float) point.Y, drawDistance, drawDistance);
                distance /= 10;
            }

            g.FillEllipse(drawColor, (float) point.X, (float) point.Y, 7.5f, 7.5f);
        }

        private void CanvasResize(object sender, EventArgs e) {
            if (scaledCoords == null || scaledCoords.Length == 0) {
                return;
            }

            ScaleDrawingCoordinates();
            canvas.Invalidate();
        }

        private double ScaleCoordinate(double coordinate, double scaleFactor) {
            return coordinate * scaleFactor;
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e) {
            var isInForm = GregoryCasting(e.Location);

            vectorChecking.BackColor = isInForm ? Color.ForestGreen : Color.Red;
        }

        private bool GregoryCasting(Point mousePosition) {
            if (scaledCoords == null || scaledCoords.Length == 0) {
                return false;
            }

            var mouseX = mousePosition.X;
            var mouseY = mirroringYCoordinate(mousePosition.Y);
            double dx;
            double dy;
            double ys;
            double dxOver = 0;
            double dxUnder = 0;
            var yOver = Double.MaxValue;
            var yUnder = Double.MinValue;

            for (var i = 0; i < scaledCoords.Length - 1; i++) {
                var startPoint = scaledCoords[i];
                var endPoint = scaledCoords[i + 1];

                dx = endPoint.X - startPoint.X;

                if (!IsPointWithinXRangeAndNotMoving(mousePosition, startPoint, endPoint, dx)) continue;
                dy = endPoint.Y - startPoint.Y;
                ys = (mouseX - startPoint.X) / dx * dy + startPoint.Y;

                if (ys >= mouseY && ys < yOver) {
                    yOver = ys;
                    dxOver = dx;
                }
                else if (ys <= mouseY && ys > yUnder) {
                    yUnder = ys;
                    dxUnder = dx;
                }
            }

            return dxOver > 0 && dxUnder < 0;
        }

        private bool GregoryCasting(PointF drawPoint) {
            if (originalCoords == null || originalCoords.Length == 0) {
                return false;
            }

            double dx;
            double dy;
            double ys;
            double dxOver = 0;
            double dxUnder = 0;
            var yOver = Double.MaxValue;
            var yUnder = Double.MinValue;

            for (var i = 0; i < originalCoords.Length - 1; i++) {
                var startPoint = originalCoords[i];
                var endPoint = originalCoords[i + 1];

                dx = endPoint.X - startPoint.X;

                if (!IsPointWithinXRangeAndNotMoving(drawPoint, startPoint, endPoint, dx)) continue;
                dy = endPoint.Y - startPoint.Y;
                ys = (drawPoint.X - startPoint.X) / dx * dy + startPoint.Y;

                if (ys >= drawPoint.Y && ys < yOver) {
                    yOver = ys;
                    dxOver = dx;
                }
                else if (ys <= drawPoint.Y && ys > yUnder) {
                    yUnder = ys;
                    dxUnder = dx;
                }
            }

            return dxOver > 0 && dxUnder < 0;
        }

        private bool IsPointWithinXRangeAndNotMoving(PointF drawPoint, DrawingCoordinate startPoint, DrawingCoordinate endPoint, double dx) {
            return (!endPoint.IsMoving) && AreXCoordinatesInTargetPointXCoordinate(startPoint.X, endPoint.X, drawPoint.X) && dx != 0;
        }

        private bool IsPointWithinXRangeAndNotMoving(Point drawPoint, DrawingCoordinate startPoint, DrawingCoordinate endPoint, double dx) {
            return (!endPoint.IsMoving) && AreXCoordinatesInTargetPointXCoordinate(startPoint.X, endPoint.X, drawPoint.X) && dx != 0;
        }

        private bool AreXCoordinatesInTargetPointXCoordinate(double startPointX, double endpointX, double targetPointX) {
            return (startPointX < targetPointX && endpointX > targetPointX) ^ (startPointX > targetPointX && endpointX < targetPointX);
        }

        private double mirroringYCoordinate(double y) {
            return canvas.Height - y;
        }

        private void PointDistanceKeyUp(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                ChangeDistance();
            }
        }

        private void GoButtonMouseClick(object sender, MouseEventArgs e) {
            using var g = canvas.CreateGraphics();

            ChangeDistance();
        }

        private void ChangeDistance() {
            if (Regex.IsMatch(inputDistance.Text, @"\D")) {
                return;
            }

            var distances = inputDistance.Text;

            distance = int.Parse(distances);
            canvas.Invalidate();
        }

        private double GetScaleFactor() {
            return Math.Min(canvas.Width / (maxX - minX), canvas.Height / (maxY - minY));
        }

        private double ChangeYPointCoordinate(double y) {
            var scaleFactor = GetScaleFactor();
            y *= scaleFactor;
            y = (float) mirroringYCoordinate((float) y);
            return y;
        }
    }
}