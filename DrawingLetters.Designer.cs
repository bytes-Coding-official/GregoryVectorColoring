namespace DrawingLetters
{
    partial class DrawingLetters
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            visualizeCoordinates = new RichTextBox();
            readFileButton = new Button();
            canvas = new Panel();
            inputDistance = new TextBox();
            goButton = new Button();
            pointPosition = new RichTextBox();
            vectorChecking = new Panel();
            SuspendLayout();
            // 
            // visualizeCoordinates
            // 
            visualizeCoordinates.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            visualizeCoordinates.Location = new Point(859, 5);
            visualizeCoordinates.Name = "visualizeCoordinates";
            visualizeCoordinates.Size = new Size(163, 341);
            visualizeCoordinates.TabIndex = 0;
            visualizeCoordinates.Text = "";
            // 
            // readFileButton
            // 
            readFileButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            readFileButton.Location = new Point(936, 501);
            readFileButton.Name = "readFileButton";
            readFileButton.Size = new Size(90, 23);
            readFileButton.TabIndex = 1;
            readFileButton.Text = "Read CSV file";
            readFileButton.UseVisualStyleBackColor = true;
            readFileButton.Click += ReadFileButtonClick;
            // 
            // canvas
            // 
            canvas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            canvas.Location = new Point(3, 5);
            canvas.Name = "canvas";
            canvas.Size = new Size(850, 529);
            canvas.TabIndex = 2;
            canvas.Paint += LetterDrawingPaint;
            canvas.MouseMove += CanvasMouseMove;
            canvas.Resize += CanvasResize;
            // 
            // inputDistance
            // 
            inputDistance.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            inputDistance.Location = new Point(936, 472);
            inputDistance.Name = "inputDistance";
            inputDistance.Size = new Size(90, 23);
            inputDistance.TabIndex = 5;
            inputDistance.Text = "0";
            inputDistance.KeyUp += PointDistanceKeyUp;
            // 
            // goButton
            // 
            goButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            goButton.Location = new Point(885, 472);
            goButton.Name = "goButton";
            goButton.Size = new Size(45, 23);
            goButton.TabIndex = 6;
            goButton.Text = "Go";
            goButton.UseVisualStyleBackColor = true;
            goButton.MouseClick += GoButtonMouseClick;
            // 
            // pointPosition
            // 
            pointPosition.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            pointPosition.Location = new Point(859, 352);
            pointPosition.Name = "pointPosition";
            pointPosition.Size = new Size(163, 96);
            pointPosition.TabIndex = 7;
            pointPosition.Text = "";
            // 
            // vectorChecking
            // 
            vectorChecking.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            vectorChecking.Location = new Point(885, 449);
            vectorChecking.Name = "vectorChecking";
            vectorChecking.Size = new Size(137, 17);
            vectorChecking.TabIndex = 8;
            // 
            // DrawingLetters
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.GradientActiveCaption;
            ClientSize = new Size(1034, 536);
            Controls.Add(vectorChecking);
            Controls.Add(pointPosition);
            Controls.Add(goButton);
            Controls.Add(inputDistance);
            Controls.Add(canvas);
            Controls.Add(readFileButton);
            Controls.Add(visualizeCoordinates);
            MinimumSize = new Size(250, 250);
            Name = "DrawingLetters";
            Text = "Drawing Letters";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private RichTextBox visualizeCoordinates;
        private Button readFileButton;
        private Panel canvas;
        private TextBox inputDistance;
        private Button goButton;
        private RichTextBox pointPosition;
        private Panel vectorChecking;
    }
}
