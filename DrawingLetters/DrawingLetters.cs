using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DrawingLetters
{
    public partial class DrawingLetters : Form
    {
        private Dictionary<DrawPoint, List<DrawPoint>> allNeighbors;
        private DrawingCoordinate[] originalCoords;
        private DrawingCoordinate[] scaledCoords;
        private List<DrawPoint> drawnPoints;
        private double scaleFactor;
        private int distance;
        private float dotRadius = 2.5f;
        private double maxX, maxY, minX, minY;
        private float height;

        public DrawingLetters()
        {
            InitializeComponent();
            Resize += CanvasResize;
            MouseMove += new MouseEventHandler(CanvasMouseMove);
            goButton.MouseClick += new MouseEventHandler(GoButtonMouseClick);
        }

        private void ReadFileButtonClick(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Open CSV File",
                Filter = "csv files (*.csv)|*.csv",
                CheckFileExists = true,
                CheckPathExists = true,
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                MessageBox.Show(openFileDialog.FileName);
                LoadDataFromFile(openFileDialog.FileName);
            }
        }

        private void LoadDataFromFile(string csvPath)
        {
            int idx = 0;
            originalCoords = new DrawingCoordinate[idx];
            StringBuilder coordsAsText = new StringBuilder();
            DialogResult result = MessageBox.Show($"Möchten Sie wirklich die Datei {csvPath} laden?", "Datei laden", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                NumberFormatInfo provider = new NumberFormatInfo
                {
                    NumberDecimalSeparator = ".",
                    NumberGroupSeparator = ","
                };

                using StreamReader stream = new StreamReader(csvPath);

                string line;

                while ((line = stream.ReadLine()) != null)
                {
                    if (idx > originalCoords.Length - 1)
                    {
                        Array.Resize(ref originalCoords, originalCoords.Length + 1);
                    }

                    string[] parts = line.Split(",");

                    if (parts.Length == 3)
                    {
                        bool isMoving = parts[0].Trim().Equals("M");
                        double x = Convert.ToDouble(parts[1].Trim(), provider);
                        double y = Convert.ToDouble(parts[2].Trim(), provider);

                        originalCoords[idx++] = new DrawingCoordinate(isMoving, x, y);
                        coordsAsText.Append(new DrawingCoordinate(isMoving, x, y));
                    }
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
            else
            {
                MessageBox.Show("Ladevorgang abgebrochen.", "Abgebrochen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ScaleDrawingCoordinates()
        {
            //allNeighbors.Clear();
            //Berechnung des Skalierungsfaktors basierend auf dem Canvas-Größe
            scaleFactor = Math.Min(canvas.Width / (maxX - minX), canvas.Height / (maxY - minY));

            for (int i = 0; i < originalCoords.Length; i++)
            {
                double scaledX = ScaleCoordinate(originalCoords[i].X, scaleFactor);
                double scaledY = ScaleCoordinate(originalCoords[i].Y, scaleFactor);

                scaledCoords[i] = new DrawingCoordinate(originalCoords[i].IsMoving, scaledX, scaledY);
            }
        }

        private void LetterDrawingPaint(object sender, PaintEventArgs e)
        {
            if (scaledCoords == null || scaledCoords.Length == 0)
            {
                return;
            }

            Pen blackPen = new Pen(Color.Black, 5);
            PointF? startPoint = null;

            foreach (DrawingCoordinate coordinate in scaledCoords)
            {
                double mirroredY = mirroringYCoordinate(coordinate.Y);
                PointF currentPoint = new PointF((float)coordinate.X, (float)mirroredY);

                if (coordinate.IsMoving)
                {
                    startPoint = currentPoint;
                }
                else if (startPoint.HasValue)
                {
                    e.Graphics.DrawLine(blackPen, startPoint.Value, currentPoint);
                    startPoint = currentPoint;
                }
            }
            DrawPoints(e.Graphics);
        }

        private void DrawPoints(Graphics graphic)
        {
            if (distance < 11)
            {
                return;
            }

            graphic.SmoothingMode = SmoothingMode.AntiAlias;

            height = distance * (float)Math.Sqrt(3) / 2f;

            drawnPoints = new List<DrawPoint>();

            for (float y = 0; y < canvas.Height; y += height)
            {
                float offsetX = y / height % 2 * (distance / 2f);

                for (int x = 0; x < canvas.Width; x += distance)
                {
                    PointF point = new PointF(x + offsetX, y);
                    bool isPointInShape = GregoryCasting(point);

                    if (isPointInShape)
                    {
                        point.X -= dotRadius;
                        point.Y = (float)mirroringYCoordinate(point.Y) - dotRadius;
                        drawnPoints.Add(new DrawPoint(point.X, point.Y));
                    }
                }
            }

            allNeighbors = new();

            FillNeighborMap();
            pointPosition.Clear();
            ChangeDistanceOfDrawPoint();
            DrawCenterLine(graphic);
            drawnPoints.Clear();
        }

        private void ChangeDistanceOfDrawPoint()
        {
            ChangeDistanceFromMinusOneToZero();
            int counterDistance = 0;

            do
            {
                foreach (var kvp in allNeighbors)
                {
                    DrawPoint keyPoint = kvp.Key;
                    List<DrawPoint> valuePoints = kvp.Value;

                    GetDistancesAndChangeThem(keyPoint, valuePoints, counterDistance);
                }

                counterDistance++;
            } while (ContainsDistanceMinusOne());
        }

        private int GetHighestDistance()
        {
            int highestKeyDistance = allNeighbors.Keys.Max(point => point.Distance);
            int highestValueDistance = allNeighbors.Values
                .SelectMany(list => list) // Flatten die Listen von DrawPoints zu einer Sequenz von DrawPoints
                .Max(point => point.Distance);

            //return Math.Max(highestKeyDistance, highestValueDistance);
            return highestKeyDistance;
        }

        private void DrawCenterLine(Graphics graphic)
        {
            StringBuilder sb = new StringBuilder();
            int maxDistance = GetHighestDistance();

            /*
            foreach (var kvp in allNeighbors)
            {
                DrawPoint keyPoint = kvp.Key;

                DrawSinglePoint(graphic, keyPoint, dotRadius, keyPoint.Distance);
                sb.Append(keyPoint.Distance + "\n");
            }
            
            foreach (var kvp in allNeighbors)
            {
                DrawPoint point = kvp.Key;

                if (point.Distance == 0)
                {
                    DrawCenterPoint(graphic, point, dotRadius, point.Distance);
                }
            }
            
            foreach (var kvp in allNeighbors)
            {
                DrawPoint keyPoint = kvp.Key;

                DrawNumber(graphic, keyPoint, dotRadius, keyPoint.Distance);
            }

           Stack<DrawPoint> middleLine = new Stack<DrawPoint>();

            do
            {
                foreach(var drawPoint in allNeighbors)
                {
                    DrawPoint point = drawPoint.Key;
                    List<DrawPoint> points = drawPoint.Value;

                    if (maxDistance == point.Distance)
                    {
                        CheckNeighbors(point, points, middleLine, maxDistance);
                    }
                }
            } while (maxDistance-- > 2);

            while (middleLine.Count > 0)
            {
                DrawPoint point = middleLine.Pop();
                DrawCenterPoint(graphic, point, dotRadius);
            }
            */

            foreach (var kvp in allNeighbors)
            {
                DrawPoint keyPoint = kvp.Key;

                DrawNumber(graphic, keyPoint, dotRadius, keyPoint.Distance);
            }

            pointPosition.Text = sb.ToString();
            pointPosition.Text += maxDistance + "\n";
        }

        private void CheckNeighbors(DrawPoint point, List<DrawPoint> neighbors, Stack<DrawPoint> middleLine, int maxDistance)
        {
            int nextHighesDistance = maxDistance - 1;
            List<DrawPoint> getRight = new List<DrawPoint>();

            foreach (DrawPoint neighbor in neighbors)
            {
                if (neighbor.Distance == nextHighesDistance)
                {
                    getRight.Add(neighbor);
                }
            }

            if (getRight.Count <= 2)
            {
                middleLine.Push(point);
                foreach (DrawPoint actualPoint in neighbors)
                {
                    middleLine.Push(actualPoint);
                }
            }
        }

        private bool ContainsDistanceMinusOne()
        {
            bool keysWithMinusOne = allNeighbors.Keys.Any(key => key.Distance == -1);

            bool valuesWithMinusOne = allNeighbors.Values.Any(list => list.Any(value => value.Distance == -1));

            return keysWithMinusOne || valuesWithMinusOne;
        }

        private void ChangeDistanceFromMinusOneToZero()
        {
            foreach (var neighbor in allNeighbors)
            {
                DrawPoint keyPoint = neighbor.Key;
                List<DrawPoint> valuePoints = neighbor.Value;

                if (keyPoint.Distance == -1 && valuePoints.Count() < 6)
                {
                    keyPoint.Distance = 0;
                }
            }
        }

        private void GetDistancesAndChangeThem(DrawPoint keypoint, List<DrawPoint> neighbors, int countDistance)
        {
            if (keypoint.Distance == countDistance)
            {
                foreach (DrawPoint actualNeighbor in neighbors)
                {
                    if (actualNeighbor.Distance == -1)
                    {
                        actualNeighbor.Distance = countDistance + 1;
                    }
                }
            }
        }

        private void FillNeighborMap()
        {
            allNeighbors.Clear();

            foreach (DrawPoint middlePoint in drawnPoints)
            {
                foreach (DrawPoint neighbor in drawnPoints)
                {
                    AddNeighborIfClose(middlePoint, neighbor, distance, 0); // rechter Punkt
                    AddNeighborIfClose(middlePoint, neighbor, -distance, 0); // linker Punkt
                    AddNeighborIfClose(middlePoint, neighbor, distance / 2, height); // oberer rechter Punkt
                    AddNeighborIfClose(middlePoint, neighbor, -distance / 2, height); // oberer linker Punkt
                    AddNeighborIfClose(middlePoint, neighbor, distance / 2, -height); // unterer rechter Punkt
                    AddNeighborIfClose(middlePoint, neighbor, -distance / 2, -height); // unterer linker Punkt
                }
            }
        }

        private void AddNeighborIfClose(DrawPoint firstPoint, DrawPoint secondPoint, float offsetX, float offsetY)
        {
            if (Math.Abs(secondPoint.X - (firstPoint.X + offsetX)) < 1 && Math.Abs(secondPoint.Y - (firstPoint.Y + offsetY)) < 1)
            {
                if (!allNeighbors.TryGetValue(firstPoint, out var neighbor))
                {
                    neighbor = new List<DrawPoint>();
                    allNeighbors.Add(firstPoint, neighbor);
                }
                neighbor.Add(secondPoint);
            }
        }

        private void DrawSinglePoint(Graphics g, DrawPoint point, float radius, int distance)
        {
            int maxDistance = GetHighestDistance();
            double ratio = (double)distance / maxDistance;
            int colorIntensity = (int)(ratio * 255.0);
            SolidBrush drawColor = new SolidBrush(Color.FromArgb(255 - colorIntensity, 255 - colorIntensity, 255 - colorIntensity));

            g.FillEllipse(drawColor, point.X, point.Y, radius * 2, radius * 2);
        }

        private void DrawNumber(Graphics g, DrawPoint point, float radius, int distance)
        {
            SolidBrush drawColor = new SolidBrush(Color.Red);

            Font font = new Font("Calibri", 8);

            g.DrawString(distance.ToString(), font, drawColor, point.X, point.Y);
        }

        private void DrawCenterPoint(Graphics g, DrawPoint point, float radius)
        {
            SolidBrush drawColor = new SolidBrush(Color.Yellow);

            g.FillEllipse(drawColor, point.X, point.Y, radius * 2, radius * 2);
        }

        private void CanvasResize(object sender, EventArgs e)
        {
            if (scaledCoords == null || scaledCoords.Length == 0)
            {
                return;
            }

            ScaleDrawingCoordinates();
            canvas.Invalidate();
        }

        private double ScaleCoordinate(double coordinate, double scaleFactor)
        {
            return coordinate * scaleFactor;
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            bool isInForm = GregoryCasting(e.Location);

            if (isInForm)
            {
                vectorChecking.BackColor = Color.ForestGreen;
            }
            else
            {
                vectorChecking.BackColor = Color.Red;
            }
        }

        private bool GregoryCasting(Point mousePosition)
        {
            if (scaledCoords == null || scaledCoords.Length == 0)
            {
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

            for (int i = 0; i < scaledCoords.Length - 1; i++)
            {
                DrawingCoordinate startPoint = scaledCoords[i];
                DrawingCoordinate endPoint = scaledCoords[i + 1];

                dx = endPoint.X - startPoint.X;

                if (IsPointWithinXRangeAndNotMoving(mousePosition, startPoint, endPoint, dx))
                {
                    dy = endPoint.Y - startPoint.Y;
                    ys = (mouseX - startPoint.X) / dx * dy + startPoint.Y;

                    if (ys >= mouseY && ys < yOver)
                    {
                        yOver = ys;
                        dxOver = dx;
                    }
                    else if (ys <= mouseY && ys > yUnder)
                    {
                        yUnder = ys;
                        dxUnder = dx;
                    }
                }
            }
            return dxOver > 0 && dxUnder < 0;
        }

        private bool GregoryCasting(PointF drawPoint)
        {
            if (scaledCoords == null || scaledCoords.Length == 0)
            {
                return false;
            }

            double dx;
            double dy;
            double ys;
            double dxOver = 0;
            double dxUnder = 0;
            double yOver = Double.MaxValue;
            double yUnder = Double.MinValue;

            for (int i = 0; i < scaledCoords.Length - 1; i++)
            {
                DrawingCoordinate startPoint = scaledCoords[i];
                DrawingCoordinate endPoint = scaledCoords[i + 1];

                dx = endPoint.X - startPoint.X;

                if (IsPointWithinXRangeAndNotMoving(drawPoint, startPoint, endPoint, dx))
                {
                    dy = endPoint.Y - startPoint.Y;
                    ys = (drawPoint.X - startPoint.X) / dx * dy + startPoint.Y;

                    if (ys >= drawPoint.Y && ys < yOver)
                    {
                        yOver = ys;
                        dxOver = dx;
                    }
                    else if (ys <= drawPoint.Y && ys > yUnder)
                    {
                        yUnder = ys;
                        dxUnder = dx;
                    }
                }
            }
            return dxOver > 0 && dxUnder < 0;
        }

        private bool IsPointWithinXRangeAndNotMoving(PointF drawPoint, DrawingCoordinate startPoint, DrawingCoordinate endPoint, double dx)
        {
            return (!endPoint.IsMoving) && AreXCoordinatesInTargetPointXCoordinate(startPoint.X, endPoint.X, drawPoint.X) && dx != 0;
        }

        private bool IsPointWithinXRangeAndNotMoving(Point drawPoint, DrawingCoordinate startPoint, DrawingCoordinate endPoint, double dx)
        {
            return (!endPoint.IsMoving) && AreXCoordinatesInTargetPointXCoordinate(startPoint.X, endPoint.X, drawPoint.X) && dx != 0;
        }

        private bool AreXCoordinatesInTargetPointXCoordinate(double startPointX, double endpointX, double targetPointX)
        {
            return (startPointX < targetPointX && endpointX > targetPointX) || (startPointX > targetPointX && endpointX < targetPointX);
        }

        private double mirroringYCoordinate(double y)
        {
            return canvas.Height - y;
        }

        private void PointDistanceKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ChangeDistance();
            }
        }

        private void GoButtonMouseClick(object sender, MouseEventArgs e)
        {
            using Graphics g = canvas.CreateGraphics();

            ChangeDistance();
        }

        private void ChangeDistance()
        {
            if (Regex.IsMatch(inputDistance.Text, @"\D"))
            {
                return;
            }

            string distances = inputDistance.Text;

            distance = int.Parse(distances);
            canvas.Invalidate();
        }
    }
}