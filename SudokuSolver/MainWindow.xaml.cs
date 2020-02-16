using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Microsoft.Win32;
using SudokuSolver.Helpers;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace SudokuSolver
{
    public partial class MainWindow : Window
    {
        private string fileName = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            var op = new OpenFileDialog
            {
                Title = "Select an image of a Sudoku Puzzle",
                Filter = "All supported graphics|*.jpg;*.jpeg;*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|Portable Network Graphic (*.png)|*.png"
            };

            if (op.ShowDialog() != true)
            {
                return;
            }

            fileName = op.FileName;

            imgPhotoPreview.Source = new BitmapImage(new Uri(fileName));
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private void btnSolve_Click(object sender, RoutedEventArgs e)
        {
            if (imgPhotoPreview.Source == null) { return; }

            Mat sudoku = CvInvoke.Imread(fileName, ImreadModes.Grayscale);
            Mat original = sudoku.Clone();

            Mat outerBox = new Mat(sudoku.Size, DepthType.Cv8U, 1);

            CvInvoke.GaussianBlur(sudoku, sudoku, new Size(11, 11), 0);
            CvInvoke.AdaptiveThreshold(sudoku, outerBox, 255, AdaptiveThresholdType.MeanC, ThresholdType.Binary, 5, 2);
            CvInvoke.BitwiseNot(outerBox, outerBox);

            Mat kernel = CvInvoke.GetStructuringElement(ElementShape.Cross, new Size(3, 3), new Point(1, 1));
            CvInvoke.Dilate(outerBox, outerBox, kernel, Point.Empty, 1, BorderType.Default, new MCvScalar());

            int max = -1;

            Point maxPt = new Point();

            for (int y = 0; y < outerBox.Height; y++)
            {
                var row = outerBox.GetRawData(y);

                for (int x = 0; x < outerBox.Width; x++)
                {
                    if (row[x] >= 128)
                    {
                        int area = CvInvoke.FloodFill(outerBox, null, new Point(x, y), new MCvScalar(64), out Rectangle _, new MCvScalar(), new MCvScalar());

                        if (area > max)
                        {
                            maxPt = new Point(x, y);
                            max = area;
                        }
                    }
                }
            }

            CvInvoke.FloodFill(outerBox, null, maxPt, new Rgb(255, 255, 255).MCvScalar, out Rectangle _, new MCvScalar(), new MCvScalar());

            for (int y = 0; y < outerBox.Height; y++)
            {
                var row = outerBox.GetRawData(y);

                for (int x = 0; x < outerBox.Width; x++)
                {
                    if (row[x] == 64 && x != maxPt.X && y != maxPt.Y)
                    {
                        CvInvoke.FloodFill(outerBox, null, new Point(x, y), new MCvScalar(0), out Rectangle _, new MCvScalar(), new MCvScalar());
                    }
                }
            }

            CvInvoke.Erode(outerBox, outerBox, kernel, Point.Empty, 1, BorderType.Default, new MCvScalar());

            var lines = new VectorOfPointF();

            CvInvoke.HoughLines(outerBox, lines, 1, Math.PI / 180, 200);

            lines = MergeRelatedLines(lines, sudoku);

            for (int i = 0; i < lines.Size; i++)
            {
                DrawLine(lines[i], outerBox, new MCvScalar(128));
            }

            PointF topEdge = new PointF(1000, 1000); double topYIntercept = 100000, topXIntercept = 0;
            PointF bottomEdge = new PointF(-1000, -1000); double bottomYIntercept = 0, bottomXIntercept = 0;
            PointF leftEdge = new PointF(1000, 1000); double leftXIntercept = 100000, leftYIntercept = 0;
            PointF rightEdge = new PointF(-1000, -1000); double rightXIntercept = 0, rightYIntercept = 0;

            for (int i = 0; i < lines.Size; i++)
            {
                PointF current = lines[i];

                float p = current.X;
                float theta = current.Y;

                if (p == 0 && theta == -100) { continue; }

                double xIntercept, yIntercept;

                xIntercept = p / Math.Cos(theta);
                yIntercept = p / (Math.Cos(theta) * Math.Sin(theta));

                if (theta > Math.PI * 80 / 180 && theta < Math.PI * 100 / 180)
                {
                    if (p < topEdge.X) { topEdge = current; }

                    if (p > bottomEdge.X) { bottomEdge = current; }
                }
                else if (theta < Math.PI * 10 / 180 || theta > Math.PI * 170 / 180)
                {
                    if (xIntercept > rightXIntercept)
                    {
                        rightEdge = current;
                        rightXIntercept = xIntercept;
                    }
                    else if (xIntercept <= leftXIntercept)
                    {
                        leftEdge = current;
                        leftXIntercept = xIntercept;
                    }
                }
            }

            DrawLine(topEdge, sudoku, new MCvScalar(0, 0, 0));
            DrawLine(bottomEdge, sudoku, new MCvScalar(0, 0, 0));
            DrawLine(leftEdge, sudoku, new MCvScalar(0, 0, 0));
            DrawLine(rightEdge, sudoku, new MCvScalar(0, 0, 0));

            Point left1, left2, right1, right2, bottom1, bottom2, top1, top2;
            left1 = left2 = right1 = right2 = bottom1 = bottom2 = top1 = top2 = new Point();

            int height = outerBox.Height;
            int width = outerBox.Width;

            if (leftEdge.Y != 0)
            {
                left1.X = 0;
                left1.Y = (int)(leftEdge.X / Math.Sin(leftEdge.Y));

                left2.X = width;
                left2.Y = (int)(-left2.X / Math.Tan(leftEdge.Y) + left1.Y);
            }
            else
            {
                left1.Y = 0;
                left1.X = (int)(leftEdge.X / Math.Cos(leftEdge.Y));

                left2.Y = height;
                left2.X = (int)(left1.X - height * Math.Tan(leftEdge.Y) + left1.Y);
            }

            if (rightEdge.Y != 0)
            {
                right1.X = 0;
                right1.Y = (int)(rightEdge.X / Math.Sin(rightEdge.Y));

                right2.X = width;
                right2.Y = (int)(-right2.X / Math.Tan(rightEdge.Y) + right1.Y);
            }
            else
            {
                right1.Y = 0;
                right1.X = (int)(rightEdge.X / Math.Cos(rightEdge.Y));

                right2.Y = height;
                right2.X = (int)(right1.X - height * Math.Tan(rightEdge.Y));

            }

            bottom1.X = 0;
            bottom1.Y = (int)(bottomEdge.X / Math.Sin(bottomEdge.Y));

            bottom2.X = width;
            bottom2.Y = (int)(-bottom2.X / Math.Tan(bottomEdge.Y) + bottom1.Y);

            top1.X = 0;
            top1.Y = (int)(topEdge.X / Math.Sin(topEdge.Y));

            top2.X = width;
            top2.Y = (int)(-top2.X / Math.Tan(topEdge.Y) + top1.Y);

            int leftA = left2.Y - left1.Y;
            int leftB = left1.X - left2.X;

            int leftC = leftA * left1.X + leftB * left1.Y;

            int rightA = right2.Y - right1.Y;
            int rightB = right1.X - right2.X;

            int rightC = rightA * right1.X + rightB * right1.Y;

            int topA = top2.Y - top1.Y;
            int topB = top1.X - top2.X;

            int topC = topA * top1.X + topB * top1.Y;

            int bottomA = bottom2.Y - bottom1.Y;
            int bottomB = bottom1.X - bottom2.X;

            int bottomC = bottomA * bottom1.X + bottomB * bottom1.Y;

            int detTopLeft = leftA * topB - leftB * topA;

            Point ptTopLeft = new Point((topB * leftC - leftB * topC) / detTopLeft, (leftA * topC - topA * leftC) / detTopLeft);

            double detTopRight = rightA * topB - rightB * topA;

            Point ptTopRight = new Point((int)((topB * rightC - rightB * topC) / detTopRight), (int)((rightA * topC - topA * rightC) / detTopRight));

            double detBottomRight = rightA * bottomB - rightB * bottomA;
            Point ptBottomRight = new Point((int)((bottomB * rightC - rightB * bottomC) / detBottomRight), (int)((rightA * bottomC - bottomA * rightC) / detBottomRight));
            double detBottomLeft = leftA * bottomB - leftB * bottomA;
            Point ptBottomLeft = new Point((int)((bottomB * leftC - leftB * bottomC) / detBottomLeft), (int)((leftA * bottomC - bottomA * leftC) / detBottomLeft));

            CvInvoke.Line(sudoku, ptTopRight, ptTopRight, new MCvScalar(255, 0, 0), 10);
            CvInvoke.Line(sudoku, ptTopLeft, ptTopLeft, new MCvScalar(255, 0, 0), 10);
            CvInvoke.Line(sudoku, ptBottomRight, ptBottomRight, new MCvScalar(255, 0, 0), 10);
            CvInvoke.Line(sudoku, ptBottomLeft, ptBottomLeft, new MCvScalar(255, 0, 0), 10);

            int maxLength = (ptBottomLeft.X - ptBottomRight.X) * (ptBottomLeft.X - ptBottomRight.X) + (ptBottomLeft.Y - ptBottomRight.Y) * (ptBottomLeft.Y - ptBottomRight.Y);
            int temp = (ptTopRight.X - ptBottomRight.X) * (ptTopRight.X - ptBottomRight.X) + (ptTopRight.Y - ptBottomRight.Y) * (ptTopRight.Y - ptBottomRight.Y);

            if (temp > maxLength) maxLength = temp;

            temp = (ptTopRight.X - ptTopLeft.X) * (ptTopRight.X - ptTopLeft.X) + (ptTopRight.Y - ptTopLeft.Y) * (ptTopRight.Y - ptTopLeft.Y);

            if (temp > maxLength) maxLength = temp;

            temp = (ptBottomLeft.X - ptTopLeft.X) * (ptBottomLeft.X - ptTopLeft.X) + (ptBottomLeft.Y - ptTopLeft.Y) * (ptBottomLeft.Y - ptTopLeft.Y);

            if (temp > maxLength) maxLength = temp;

            maxLength = (int)Math.Sqrt(maxLength);

            PointF[] srcValues = new PointF[4];
            PointF[] dstValues = new PointF[4];

            srcValues[0] = ptTopLeft;
            srcValues[1] = ptTopRight;
            srcValues[2] = ptBottomRight;
            srcValues[3] = ptBottomLeft;

            dstValues[0] = new PointF(0, 0);
            dstValues[1] = new PointF(maxLength - 1, 0);
            dstValues[2] = new PointF(maxLength - 1, maxLength - 1);
            dstValues[3] = new PointF(0, maxLength - 1);

            VectorOfPointF src = new VectorOfPointF(srcValues);
            VectorOfPointF dst = new VectorOfPointF(dstValues);

            Mat undistorted = new Mat(new Size(maxLength, maxLength), DepthType.Cv8U, 1);
            CvInvoke.WarpPerspective(original, undistorted, CvInvoke.GetPerspectiveTransform(src, dst), new Size(maxLength, maxLength));

            Mat undistortedThreshed = undistorted.Clone();
            CvInvoke.AdaptiveThreshold(undistorted, undistortedThreshed, 255, AdaptiveThresholdType.GaussianC, ThresholdType.BinaryInv, 101, 1);

            int dist = (int)Math.Ceiling((double)maxLength / 9);
            Mat currentCell = new Mat(dist, dist, DepthType.Cv8U, 1);

            var dr = new DigitRecogniser();

            var result = new int?[9, 9];

            for (int j = 0; j < 9; j++)
            {
                for (int i = 4; i < 9; i++)
                {
                    var test = new byte[dist * dist];

                    for (int y = 0; y < dist && j * dist + y < undistortedThreshed.Cols; y++)
                    {
                        for (int x = 0; x < dist && i * dist + x < undistortedThreshed.Rows; x++)
                        {
                            test[(y * dist) + x] = undistortedThreshed.GetRawData(j * dist + y, i * dist + x)[0];
                        }
                    }

                    var image = new Image<Gray, byte>(dist, dist)
                    {
                        Bytes = test
                    };
                    currentCell = image.Mat;

                    Moments m = CvInvoke.Moments(currentCell, true);
                    int area = (int)m.M00;
                    if (area > currentCell.Rows * currentCell.Cols / 5)
                    {
                        result[j, i] = dr.Classify(currentCell);
                    }
                }
            }

            DisplayResult(currentCell);
        }

        private void DrawLine(PointF line, Mat image, MCvScalar rgb)
        {
            if (line.Y != 0)
            {
                double m = -1 / Math.Tan(line.Y);

                double c = line.X / Math.Sin(line.Y);

                CvInvoke.Line(image, new Point(0, (int)c), new Point(image.Width, (int)(m * image.Width + c)), new MCvScalar(128));
            }
            else
            {
                CvInvoke.Line(image, new Point((int)line.X, 0), new Point((int)line.X, image.Height), new MCvScalar(128));
            }
        }

        private VectorOfPointF MergeRelatedLines(VectorOfPointF lines, Mat image)
        {
            var values = lines.ToArray();

            for (int i = 0; i < lines.Size; i++)
            {
                var current = lines[i];

                if (current.X == 0 && current.Y == -100) { continue; }

                float p1 = current.X;
                float theta1 = current.Y;

                PointF pt1Current = new PointF();
                PointF pt2Current = new PointF();

                if (theta1 > Math.PI * 45 / 180 && theta1 < Math.PI * 135 / 180)
                {
                    pt1Current.X = 0;

                    pt1Current.Y = (float)(p1 / Math.Sin(theta1));

                    pt2Current.X = image.Width;
                    pt2Current.Y = (float)(pt2Current.X / Math.Tan(theta1) + p1 / Math.Sin(theta1));
                }
                else
                {
                    pt1Current.Y = 0;

                    pt1Current.X = (float)(p1 / Math.Cos(theta1));

                    pt2Current.Y = image.Height;
                    pt2Current.X = (float)(-pt2Current.Y / Math.Tan(theta1) + p1 / Math.Cos(theta1));
                }

                for (int j = 0; j < lines.Size; j++)
                {
                    var pos = lines[j];

                    if (current == pos) { continue; }

                    if (Math.Abs(pos.X - current.X) < 20 && Math.Abs(pos.Y - current.Y) < Math.PI * 10 / 180)
                    {
                        float p = pos.X;
                        float theta = pos.Y;

                        PointF pt1, pt2;
                        pt1 = pt2 = new PointF();

                        if (pos.Y > Math.PI * 45 / 180 && pos.Y < Math.PI * 135 / 180)
                        {
                            pt1.X = 0;
                            pt1.Y = (float)(p / Math.Sin(theta));
                            pt2.X = image.Width;
                            pt2.Y = (float)(-pt2.X / Math.Tan(theta) + p / Math.Sin(theta));
                        }
                        else
                        {
                            pt1.Y = 0;
                            pt1.X = (float)(p / Math.Cos(theta));
                            pt2.Y = image.Height;
                            pt2.X = (float)(-pt2.Y / Math.Tan(theta) + p / Math.Cos(theta));
                        }

                        if (((double)(pt1.X - pt1Current.X) * (pt1.X - pt1Current.X) + (pt1.Y - pt1Current.Y) * (pt1.Y - pt1Current.Y) < 64 * 64) &&
                            ((double)(pt2.X - pt2Current.X) * (pt2.X - pt2Current.X) + (pt2.Y - pt2Current.Y) * (pt2.Y - pt2Current.Y) < 64 * 64))
                        {
                            values[i].X = (current.X + pos.X) / 2;
                            values[i].Y = (current.Y + pos.Y) / 2;

                            values[j].X = 0;
                            values[j].Y = -100;
                        }
                    }
                }
            }

            return new VectorOfPointF(values);
        }

        private void DisplayResult(Mat sudoku)
        {
            using Bitmap bmp = sudoku.ToBitmap();

            IntPtr hBitmap = bmp.GetHbitmap();

            try
            {
                imgPhotoResult.Source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
    }
}
