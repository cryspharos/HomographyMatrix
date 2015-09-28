using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using CV = OpenCvSharp.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;

namespace HomographyMatrix
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("USER32.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern void SetCursorPos(int X, int Y);

        private ViewModel vm;

        /// <summary> 楕円の名前とpolygonのインデックスの対応 </summary>
        private List<Ellipse> ellipseDictionary;
        /// <summary> ドラッグ用．ドラッグ中か否か </summary>
        private bool isDrag = false;
        /// <summary> ドラッグ用．楕円の座標とマウスポインタの座標のオフセット </summary>
        private Point dragOffset;

        private double origGridWidth { get; set; }
        private double origEllipseWidth { get; set; }
        private double origStrokeThickness { get; set; }
        private double origTextBlockFontSize { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            ellipseDictionary = new List<Ellipse>() { ellipseLT, ellipseRT, ellipseRB, ellipseLB };
            DataContext = vm = new ViewModel(canvas);
            origGridWidth = srcGrid.Width;
            origEllipseWidth = ellipseLT.Width;
            origStrokeThickness = polygon.StrokeThickness;
            origTextBlockFontSize = txbLT.FontSize;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ResetEllipses(origGridWidth);
            ResetRuler(origGridWidth);
        }

        private void txtBoxInputImagePath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            GetFilePath();
        }

        private void btnInputImagePath_Click(object sender, RoutedEventArgs e)
        {
            GetFilePath();
        }

        private void txtBoxInputImagePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            OpenImage();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "*.txt | *.txt";
            if(sfd.ShowDialog() == true)
            {
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(sfd.FileName))
                {
                    for (int i = 0; i < vm.HomographyMatrix.Length; i++)
                    {
                        sw.WriteLine(vm.HomographyMatrix[i]);
                    }
                }
            }
        }

        private void ellipse_MouseEnter(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Cross;
        }

        private void ellipse_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void Ellipse_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UIElement el = sender as UIElement;
            if (el != null)
            {
                isDrag = true;
                dragOffset = e.GetPosition(el);
                el.CaptureMouse();
            }
        }

        private void Ellipse_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDrag)
            {
                UIElement el = sender as UIElement;
                el.ReleaseMouseCapture();
                isDrag = false;
                if (imgRaw.Source == null) return;
                double[] srcPoints = GetSourcePoints();
                CalculateHomography(srcPoints);
                Transform(srcPoints);
            }
        }

        private void Ellipse_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrag)
            {
                Point pt = Mouse.GetPosition(canvas);
                Ellipse el = sender as Ellipse;
                if (pt.X - dragOffset.X < 0) pt.X = 0;
                if (pt.X - dragOffset.X > canvas.ActualWidth) pt.X = canvas.ActualWidth;
                if (pt.Y - dragOffset.Y < 0) pt.Y = 0;
                if (pt.Y - dragOffset.Y > canvas.ActualHeight) pt.Y = canvas.ActualHeight;
                Point cursorPos = canvas.PointToScreen(pt);
                SetCursorPos((int)cursorPos.X, (int)cursorPos.Y);
                Canvas.SetLeft(el, pt.X - dragOffset.X);
                Canvas.SetTop(el, pt.Y - dragOffset.Y);
                polygon.Points[ellipseDictionary.IndexOf(el)]
                    = new Point(Canvas.GetLeft(el) + ellipseLT.Width / 2, Canvas.GetTop(el) + ellipseLT.Width / 2);
            }
        }

        private void imgRaw_MouseMove(object sender, MouseEventArgs e)
        {
            vm.SrcMousePos = Image2BitmapCoordinate(imgRaw, e.GetPosition(imgRaw));
            if (imgTransformed.Source == null) return;
            CV.Mat srcMousePos = new CV.Mat(3, 1, CV.MatType.CV_64F,
                new double[] { vm.SrcMousePos.X, vm.SrcMousePos.Y, 1 });
            CV.Mat homographyMatrix = new CV.Mat(3, 3, CV.MatType.CV_64F, vm.HomographyMatrix);//homographyはvm.HomographyMatrixの参照
            CV.Mat dstMousePos = new CV.Mat(3, 1, CV.MatType.CV_64F);
            dstMousePos = homographyMatrix * srcMousePos;
            vm.DstMousePos = new Point(dstMousePos.At<double>(0, 0) / dstMousePos.At<double>(0, 2),
                dstMousePos.At<double>(0, 1) / dstMousePos.At<double>(0, 2));
        }

        private void imgTransformed_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = Image2BitmapCoordinate(imgTransformed, e.GetPosition(imgTransformed));
            p.X = p.X / imgTransformed.Source.Width * 2 - 0.5;  // グリッド内[0,1]
            p.Y = p.Y / imgTransformed.Source.Height * 2 - 0.5; // グリッド内[0,1]
            p.X = p.X * (vm.DstMaxX - vm.DstMinX) + vm.DstMinX; // グリッド内[DstMinX,DstMaxX]
            p.Y = p.Y * (vm.DstMaxY - vm.DstMinY) + vm.DstMinY; // グリッド内[DstMinY,DstMaxY]
            vm.DstMousePos = p;
            if (imgTransformed.Source == null) return;
            CV.Mat dstMousePos = new CV.Mat(3, 1, CV.MatType.CV_64F,
                new double[] { vm.DstMousePos.X, vm.DstMousePos.Y, 1 });
            CV.Mat homographyMatrixInv = new CV.Mat(3, 3, CV.MatType.CV_64F, vm.HomographyMatrix).Inv();//homographyはvm.HomographyMatrixの参照
            CV.Mat srcMousePos = new CV.Mat(3, 1, CV.MatType.CV_64F);
            srcMousePos = homographyMatrixInv * dstMousePos;
            vm.SrcMousePos = new Point(srcMousePos.At<double>(0, 0) / srcMousePos.At<double>(0, 2),
                srcMousePos.At<double>(0, 1) / srcMousePos.At<double>(0, 2));
        }

        private void txtBoxDst_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            textBox.SelectAll();
        }

        private void txtBoxDst_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (imgRaw.Source == null) return;
            double[] srcPoints = GetSourcePoints();
            CalculateHomography(srcPoints);
        }

        private void GetFilePath()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "*.jpg, *.png, *.gif | *.jpg;*.png;*.gif";
            if (ofd.ShowDialog() == true)
            {
                txtBoxInputImagePath.Text = ofd.FileName;
            }
        }

        private void ResetEllipses(double gridWidth)
        {
            foreach (var e in ellipseDictionary)
            {
                e.Width = e.Height = origEllipseWidth * gridWidth / origGridWidth;
                e.StrokeThickness = origStrokeThickness * gridWidth / origGridWidth;
            }
            double radius = ellipseLT.Width / 2;
            double aw14 = gridWidth / 4 - radius;
            double aw34 = gridWidth * 3 / 4 - radius;
            double ah14 = gridWidth / 4 - radius;
            double ah34 = gridWidth * 3 / 4 - radius;
            Canvas.SetLeft(ellipseLT, aw14);
            Canvas.SetTop(ellipseLT, ah14);
            Canvas.SetLeft(ellipseRT, aw34);
            Canvas.SetTop(ellipseRT, ah14);
            Canvas.SetLeft(ellipseRB, aw34);
            Canvas.SetTop(ellipseRB, ah34);
            Canvas.SetLeft(ellipseLB, aw14);
            Canvas.SetTop(ellipseLB, ah34);

            polygon.Points = new PointCollection(4);
            polygon.StrokeThickness = origStrokeThickness * gridWidth / origGridWidth;
            polygon.Points.Add(new Point(Canvas.GetLeft(ellipseLT) + radius, Canvas.GetTop(ellipseLT) + radius));
            polygon.Points.Add(new Point(Canvas.GetLeft(ellipseRT) + radius, Canvas.GetTop(ellipseRT) + radius));
            polygon.Points.Add(new Point(Canvas.GetLeft(ellipseRB) + radius, Canvas.GetTop(ellipseRB) + radius));
            polygon.Points.Add(new Point(Canvas.GetLeft(ellipseLB) + radius, Canvas.GetTop(ellipseLB) + radius));
        }

        private void ResetRuler(double gridWidth)
        {
            foreach (var o in new[] {
                new { ruler = ruler0, x1 = 0.25, y1 = 0.00, x2 = 0.25, y2 = 1.00 },
                new { ruler = ruler1, x1 = 0.75, y1 = 0.00, x2 = 0.75, y2 = 1.00 },
                new { ruler = ruler2, x1 = 0.00, y1 = 0.25, x2 = 1.00, y2 = 0.25 },
                new { ruler = ruler3, x1 = 0.00, y1 = 0.75, x2 = 1.00, y2 = 0.75 }
            })
            {
                o.ruler.StrokeThickness = origStrokeThickness * gridWidth / origGridWidth;
                o.ruler.X1 = o.x1 * gridWidth;
                o.ruler.Y1 = o.y1 * gridWidth;
                o.ruler.X2 = o.x2 * gridWidth;
                o.ruler.Y2 = o.y2 * gridWidth;
            }
            txbLT.FontSize = origTextBlockFontSize * gridWidth / origGridWidth;
            txbRB.FontSize = origTextBlockFontSize * gridWidth / origGridWidth;
        }

        private void OpenImage()
        {
            var mat = new CV.Mat(txtBoxInputImagePath.Text, LoadMode.Color);
            imgRaw.Source = WriteableBitmapConverter.ToWriteableBitmap(mat);
            double w = Math.Max(mat.Width, mat.Height);
            srcGrid.Width = srcGrid.Height = dstGrid.Width = dstGrid.Height = w;
            ResetEllipses(w);
            ResetRuler(w);
        }

        /// <summary>
        /// Image上の座標をBitmap上の座標に変換する
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private System.Windows.Point Image2BitmapCoordinate(Image img, System.Windows.Point p)
        {
            return new System.Windows.Point(p.X * img.Source.Width / img.ActualWidth, p.Y * img.Source.Height / img.ActualHeight);
        }

        private double[] GetSourcePoints()
        {
            double leftOffset = (srcGrid.Width - imgRaw.Source.Width) / 2;
            double topOffset = (srcGrid.Height - imgRaw.Source.Height) / 2;

            double[] srcPoints = new double[8];
            for (int i = 0; i < 4; i++)// 4 points
            {
                Image2BitmapCoordinate(imgRaw,
                new System.Windows.Point(Canvas.GetLeft(ellipseDictionary[i]) - leftOffset + ellipseLT.Width / 2, Canvas.GetTop(ellipseDictionary[i]) - topOffset + ellipseLT.Width / 2))
                    .Unpack(out srcPoints[i * 2], out srcPoints[i * 2 + 1]);
            }
            return srcPoints;
        }

        private void CalculateHomography(double[] srcPoints)
        {
            const int POINT_COUNT = 8;
            System.Diagnostics.Debug.Assert(srcPoints.Length == POINT_COUNT);

            CvMat srcPointsMat = new CvMat(4, 2, MatrixType.F64C1, srcPoints);// 4 points 2 dimensions (x, y)
            CvMat dstPointsMat = new CvMat(4, 2, MatrixType.F64C1,            // 4 points 2 dimensions (x, y)
                new double[POINT_COUNT] { vm.DstMinX, vm.DstMinY, vm.DstMaxX, vm.DstMinY, vm.DstMaxX, vm.DstMaxY, vm.DstMinX, vm.DstMaxY });
            CvMat homographyMatrix = new CvMat(3, 3, MatrixType.F64C1, vm.HomographyMatrix);//homographyはvm.HomographyMatrixの参照
            Cv.FindHomography(srcPointsMat, dstPointsMat, homographyMatrix);
            vm.RaisePropertyChanged("HomographyMatrix");
        }

        private void Transform(double[] srcPoints)
        {
            const int POINT_COUNT = 8;
            System.Diagnostics.Debug.Assert(srcPoints.Length == POINT_COUNT);
            double leftOffset = (srcGrid.Width - imgRaw.Source.Width) / 2;
            double topOffset = (srcGrid.Height - imgRaw.Source.Height) / 2;

            CvMat srcPointsMat = new CvMat(4, 2, MatrixType.F64C1, srcPoints);
            CvMat dstPointsMat = new CvMat(4, 2, MatrixType.F64C1,
                new double[POINT_COUNT] {
                    dstGrid.Width * 1 / 4, dstGrid.Height * 1 / 4, dstGrid.Width * 3 / 4, dstGrid.Height * 1 / 4,
                    dstGrid.Width * 3 / 4, dstGrid.Height * 3 / 4, dstGrid.Width * 1 / 4, dstGrid.Height * 3 / 4 });
            CvMat viewerHomographyMatrix = new CvMat(3, 3, MatrixType.F64C1, new double[9]);
            Cv.FindHomography(srcPointsMat, dstPointsMat, viewerHomographyMatrix);

            CV.Mat src = WriteableBitmapConverter.ToMat((WriteableBitmap)imgRaw.Source);
            CV.Mat dst = new CV.Mat((int)srcGrid.Height, (int)srcGrid.Width, src.Type());
            Cv.WarpPerspective(src.ToCvMat(), dst.ToCvMat(), viewerHomographyMatrix);
            imgTransformed.Source = WriteableBitmapConverter.ToWriteableBitmap(dst);

            srcPointsMat.Dispose();
            dstPointsMat.Dispose();
            src.Dispose();
            dst.Dispose();
        }
    }

    public class PointConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var point = (Point)value;
            return string.Format("{0:f2},{1:f2}", point.X, point.Y);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public static class Extentions
    {
        public static void Unpack(this System.Windows.Point pt, out double a, out double b)
        {
            a = pt.X; b = pt.Y;
        }
    }
}

