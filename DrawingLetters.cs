using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DrawingLetters {
    public partial class DrawingLetters : Form {
        private Dictionary<DrawPoint, List<DrawPoint>> allNeighbors;
        private DrawingCoordinate[] originalCoords;
        private DrawingCoordinate[] scaledCoords;
        private List<DrawPoint> drawnPoints;
        private int distance; //radius / distance f�r die berechnungder werte
        private int distanceLED;
        private float dotRadius = 2.5f;
        private double maxX, maxY, minX, minY;
        private float height;

        public DrawingLetters() {
            InitializeComponent();
            Resize += CanvasResize;
            MouseMove += CanvasMouseMove;
            goButton.MouseClick += GoButtonMouseClick;
        }

        private void ReadFileButtonClick(object sender, EventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
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
            originalCoords = new DrawingCoordinate[idx];
            StringBuilder coordsAsText = new StringBuilder();
            DialogResult result = MessageBox.Show($"M�chten Sie wirklich die Datei {csvPath} laden?", "Datei laden", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes) {
                NumberFormatInfo provider = new NumberFormatInfo {
                    NumberDecimalSeparator = ".",
                    NumberGroupSeparator = ","
                };

                using StreamReader stream = new StreamReader(csvPath);

                string line;

                if (distance > 0) {
                    distance = 0;
                    inputDistance.Text = "0";
                }

                while ((line = stream.ReadLine()) != null) {
                    if (idx > originalCoords.Length - 1) {
                        Array.Resize(ref originalCoords, originalCoords.Length + 1);
                    }

                    string[] parts = line.Split(",");

                    if (parts.Length != 3) continue;
                    bool isMoving = parts[0].Trim().Equals("M");
                    double x = Convert.ToDouble(parts[1].Trim(), provider);
                    double y = Convert.ToDouble(parts[2].Trim(), provider);

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
            //Berechnung des Skalierungsfaktors basierend auf dem Canvas-Gr��e
            double scaleFactor = GetScaleFactor();

            for (int i = 0; i < originalCoords.Length; i++) {
                double scaledX = ScaleCoordinate(originalCoords[i].X, scaleFactor);
                double scaledY = ScaleCoordinate(originalCoords[i].Y, scaleFactor);

                scaledCoords[i] = new DrawingCoordinate(originalCoords[i].IsMoving, scaledX, scaledY);
            }
        }

        private void LetterDrawingPaint(object sender, PaintEventArgs e) {
            if (scaledCoords == null || scaledCoords.Length == 0) {
                return;
            }

            Pen blackPen = new Pen(Color.Black, 5);
            PointF? startPoint = null;

            foreach (DrawingCoordinate coordinate in scaledCoords) {
                double mirroredY = mirroringYCoordinate(coordinate.Y);
                PointF currentPoint = new PointF((float) coordinate.X, (float) mirroredY);

                if (coordinate.IsMoving) {
                    startPoint = currentPoint;
                }
                else if (startPoint.HasValue) {
                    e.Graphics.DrawLine(blackPen, startPoint.Value, currentPoint);
                    startPoint = currentPoint;
                }
            }

            DrawPoints(e.Graphics);
        }

        private void DrawPoints(Graphics g) {
            if (distance < 10) {
                return;
            }

            distanceLED = distance;

            distance /= 10;

            height = distance * (float) Math.Sqrt(3) / 2f;

            drawnPoints = new List<DrawPoint>();

            for (float y = 0; y < maxY; y += height) {
                float offsetX = y / height % 2 * (distance / 2f);

                for (int x = 0; x < maxX; x += distance) {
                    PointF point = new PointF(x + offsetX, y);
                    bool isPointInShape = GregoryCasting(point);

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
            int counterDistance = 0;

            do {
                foreach (var kvp in allNeighbors) {
                    DrawPoint keyPoint = kvp.Key;
                    List<DrawPoint> valuePoints = kvp.Value;

                    GetDistancesAndChangeThem(keyPoint, valuePoints, counterDistance);
                }

                counterDistance++;
            } while (ContainsDistanceMinusOne());
        }

        private int GetHighestDistance() {
            int highestKeyDistance = allNeighbors.Keys.Max(point => point.Distance);
            int highestValueDistance = allNeighbors.Values
                .SelectMany(list => list) // Flatten die Listen von DrawPoints zu einer Sequenz von DrawPoints
                .Max(point => point.Distance);

            //return Math.Max(highestKeyDistance, highestValueDistance);
            return highestKeyDistance;
        }

        private void DrawCenterLine(Graphics graphic) {
        
            var maxDistance = GetHighestDistance();
            pointPosition.Text += maxDistance + "\n";

            foreach (var keyPoint in allNeighbors.Select(kvp => kvp.Key)) {
                //DrawNumber(graphic, keyPoint, dotRadius, keyPoint.Distance);
                DrawSinglePoint(graphic, keyPoint, dotRadius);
            }

            var counterDistanceUp = 0;
            Stack<DrawPoint> centerLinePoints = new();
            while (counterDistanceUp <= maxDistance) {
                foreach (var actualPoint in from kvp in allNeighbors let actualPoint = kvp.Key let neighbors = kvp.Value where actualPoint.Distance == counterDistanceUp && CheckIfHigherDistanceNotExist(actualPoint, neighbors) select actualPoint) {
                    centerLinePoints.Push(actualPoint);
                }

                counterDistanceUp++;
            }
            HashSet<DrawPoint> ledPoints = [];
            foreach (var linePoint in from linePoint in centerLinePoints let nichtUeberlappend = new HashSet<DrawPoint>(ledPoints).All(donePoint => linePoint.X + distanceLED < donePoint.X - distanceLED || linePoint.X - distanceLED > donePoint.X + distanceLED || linePoint.Y + distanceLED < donePoint.Y - distanceLED || linePoint.Y - distanceLED > donePoint.Y + distanceLED) where nichtUeberlappend select linePoint) {
                ledPoints.Add(linePoint);
                Console.WriteLine("Added: " + linePoint);
            }
            //& F�rbe und zeichne die punkte ein
            foreach (DrawPoint point in centerLinePoints) {
                DrawCenterPoint(graphic, point, dotRadius);
            }
            List<DrawPoint> centerPointList = ledPoints.ToList();
            DrawLEDPoints(centerPointList, graphic);
        }

        private void DrawLEDPoints(List<DrawPoint> centerPointList, Graphics graphic) {
            int halfDistance = distanceLED / 2;
            int counter = 0;
            double dx;
            double dy;

            List<DrawPoint> newPoints = new List<DrawPoint>();

            //DrawCenterPoint();

            for (int i = 0; i < centerPointList.Count; i++) {
                for (int j = i + 1; j < centerPointList.Count; j++) {
                    dx = centerPointList[j].X - centerPointList[i].X;
                    dy = centerPointList[j].Y - centerPointList[i].Y;
                    double pointDistance = Math.Sqrt(dx * dx + dy * dy);

                    if (Math.Abs(halfDistance - pointDistance) <= distance + 1) {
                        centerPointList.Remove(centerPointList[j]);
                    }
                }

                newPoints.Add(centerPointList[i]);
            }

            foreach (DrawPoint p in newPoints) {
                DrawCenterPoint(graphic, p, dotRadius, true);
            }
        }

        private bool CheckIfHigherDistanceNotExist(DrawPoint point, List<DrawPoint> allNeighbors) {
            return allNeighbors.All(neighborPoint => neighborPoint.Distance <= point.Distance);
        }

        private bool ContainsDistanceMinusOne() {
            bool keysWithMinusOne = allNeighbors.Keys.Any(key => key.Distance == -1);

            bool valuesWithMinusOne = allNeighbors.Values.Any(list => list.Any(value => value.Distance == -1));

            return keysWithMinusOne || valuesWithMinusOne;
        }

        private void ChangeDistanceFromMinusOneToZero() {
            foreach (var neighbor in allNeighbors) {
                DrawPoint keyPoint = neighbor.Key;
                List<DrawPoint> valuePoints = neighbor.Value;

                if (valuePoints.Count() < 6) {
                    keyPoint.Distance = 0;
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

            foreach (DrawPoint middlePoint in drawnPoints) {
                foreach (DrawPoint neighbor in drawnPoints) {
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
                neighbor = new List<DrawPoint>();
                allNeighbors.Add(firstPoint, neighbor);
            }

            neighbor.Add(secondPoint);
        }

        private void DrawSinglePoint(Graphics g, DrawPoint point, float radius) {
            int maxDistance = GetHighestDistance();
            SolidBrush drawColor = new SolidBrush(Color.Black);

            double scaleFactor = GetScaleFactor();
            point.Y = ChangeYPointCoordinate(point.Y);
            point.X *= scaleFactor;

            g.FillEllipse(drawColor, (float) point.X, (float) point.Y, radius * 2, radius * 2);
        }

        private void DrawNumber(Graphics g, DrawPoint point, float radius, int distance) {
            SolidBrush drawColor = new SolidBrush(Color.Red);

            Font font = new Font("Calibri", 8);
            double scaleFactor = GetScaleFactor();
            point.Y = ChangeYPointCoordinate(point.Y);
            point.X *= scaleFactor;

            g.DrawString(distance.ToString(), font, drawColor, (float) point.X, (float) point.Y);
        }

        private void DrawCenterPoint(Graphics g, DrawPoint point, float radius, bool isRed = false) {
            SolidBrush drawColor = new SolidBrush(Color.Yellow);

            if (isRed) {
                drawColor = new SolidBrush(Color.Red);
            }

            double scaleFactor = GetScaleFactor();

            g.FillEllipse(drawColor, (float) point.X, (float) point.Y, radius * 2, radius * 2);
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
            bool isInForm = GregoryCasting(e.Location);

            vectorChecking.BackColor = isInForm ? Color.ForestGreen : Color.Red;
        }

        private bool GregoryCasting(Point mousePosition) {
            if (scaledCoords == null || scaledCoords.Length == 0) {
                return false;
            }

            double mouseX = mousePosition.X;
            double mouseY = mirroringYCoordinate(mousePosition.Y);
            double dx;
            double dy;
            double ys;
            double dxOver = 0;
            double dxUnder = 0;
            double yOver = Double.MaxValue;
            double yUnder = Double.MinValue;

            for (int i = 0; i < scaledCoords.Length - 1; i++) {
                DrawingCoordinate startPoint = scaledCoords[i];
                DrawingCoordinate endPoint = scaledCoords[i + 1];

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
            double yOver = Double.MaxValue;
            double yUnder = Double.MinValue;

            for (int i = 0; i < originalCoords.Length - 1; i++) {
                DrawingCoordinate startPoint = originalCoords[i];
                DrawingCoordinate endPoint = originalCoords[i + 1];

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
            using Graphics g = canvas.CreateGraphics();

            ChangeDistance();
        }

        private void ChangeDistance() {
            if (Regex.IsMatch(inputDistance.Text, @"\D")) {
                return;
            }

            string distances = inputDistance.Text;

            distance = int.Parse(distances);
            canvas.Invalidate();
        }

        private double GetScaleFactor() {
            return Math.Min(canvas.Width / (maxX - minX), canvas.Height / (maxY - minY));
        }

        private double ChangeYPointCoordinate(double y) {
            double scaleFactor = GetScaleFactor();
            y *= scaleFactor;
            y = (float) mirroringYCoordinate((float) y);

            return y;
        }
    }
}