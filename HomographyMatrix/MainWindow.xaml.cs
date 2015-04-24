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

        CV.Mat mat;

        /// <summary> 楕円の名前とpolygonのインデックスの対応 </summary>
        private List<Ellipse> ellipseDictionary;
        /// <summary> ドラッグ用．ドラッグ中か否か </summary>
        private bool isDrag = false;
        /// <summary> ドラッグ用．楕円の座標とマウスポインタの座標のオフセット </summary>
        private Point dragOffset;

        public MainWindow()
        {
            InitializeComponent();

            ellipseDictionary = new List<Ellipse>() { ellipseLT, ellipseRT, ellipseRB, ellipseLB };
            DataContext = vm = new ViewModel(canvas);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            double radius = ellipseLT.Width / 2;
            Canvas.SetLeft(ellipseLT, 0);
            Canvas.SetTop(ellipseLT, 0);
            Canvas.SetLeft(ellipseRT, canvas.ActualWidth - radius * 2);
            Canvas.SetTop(ellipseRT, 0);
            Canvas.SetLeft(ellipseRB, canvas.ActualWidth - radius * 2);
            Canvas.SetTop(ellipseRB, canvas.ActualHeight - radius * 2);
            Canvas.SetLeft(ellipseLB, 0);
            Canvas.SetTop(ellipseLB, canvas.ActualHeight - radius * 2);

            polygon.Points = new PointCollection(4);
            polygon.Points.Add(new Point(Canvas.GetLeft(ellipseLT) + radius, Canvas.GetTop(ellipseLT) + radius));
            polygon.Points.Add(new Point(Canvas.GetLeft(ellipseRT) + radius, Canvas.GetTop(ellipseRT) + radius));
            polygon.Points.Add(new Point(Canvas.GetLeft(ellipseRB) + radius, Canvas.GetTop(ellipseRB) + radius));
            polygon.Points.Add(new Point(Canvas.GetLeft(ellipseLB) + radius, Canvas.GetTop(ellipseLB) + radius));
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
                double leftOffset = imgRaw.PointToScreen(new Point()).X - canvas.PointToScreen(new Point()).X;
                double topOffset = imgRaw.PointToScreen(new Point()).Y - canvas.PointToScreen(new Point()).Y;

                double[] points = new double[8];
                for (int i = 0; i < 4; i++)
                {
                    Image2BitmapCoordinate(imgRaw,
                    new System.Windows.Point(Canvas.GetLeft(ellipseDictionary[i]) - leftOffset, Canvas.GetTop(ellipseDictionary[i]) - topOffset))
                        .Unpack(out points[i * 2], out points[i * 2 + 1]);
                }
                CalculateHomography(points);
                Transform();
                isDrag = false;
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
                    = new Point(Canvas.GetLeft(el) + 3, Canvas.GetTop(el) + 3);
            }
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            vm.MousePos = Image2BitmapCoordinate(imgRaw, e.GetPosition(imgRaw));
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

        private void OpenImage()
        {
            mat = new CV.Mat(txtBoxInputImagePath.Text, LoadMode.Color);
            imgRaw.Source = WriteableBitmapConverter.ToWriteableBitmap(mat);
        }

        /// <summary>
        /// Image上の座標をBitmap上の座標に変換する
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private System.Windows.Point Image2BitmapCoordinate(Image img, System.Windows.Point p)
        {
            return new System.Windows.Point((int)(p.X * img.Source.Height / img.ActualHeight), (int)(p.Y * img.Source.Height / img.ActualHeight));
        }

        private double[] CalculateHomography(double[] points)
        {
            const int POINT_COUNT = 8;
            double w = imgRaw.Source.Width, h = imgRaw.Source.Height;
            System.Diagnostics.Debug.Assert(points.Length == POINT_COUNT);

            CvMat srcPoints = new CvMat(4, 2, MatrixType.F64C1, points);
            CvMat dstPoints = new CvMat(4, 2, MatrixType.F64C1,
                new double[POINT_COUNT] { 0, 0, w, 0, w, h, 0, h });
            CvMat homography = new CvMat(3, 3, MatrixType.F64C1, vm.HomographyMatrix);
            Cv.FindHomography(srcPoints, dstPoints, homography);
            vm.RaisePropertyChanged("HomographyMatrix");
            return vm.HomographyMatrix;
        }

        private void Transform()
        {
            CV.Mat mat_ = new CV.Mat(mat.Rows, mat.Cols, mat.Type());
            Cv.WarpPerspective(mat.ToCvMat(), mat_.ToCvMat(), new CvMat(3, 3, MatrixType.F64C1, vm.HomographyMatrix));
            imgTransformed.Source = WriteableBitmapConverter.ToWriteableBitmap(mat_);
        }
    }
}

public static class Extentions
{
    public static void Unpack(this System.Windows.Point pt, out double a, out double b)
    {
        a = pt.X; b = pt.Y;
    }
}