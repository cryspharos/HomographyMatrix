using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomographyMatrix
{
    class ViewModel : INotifyPropertyChanged
    {
        private System.Windows.IInputElement displayArea;

        public ViewModel() { }
        public ViewModel(System.Windows.IInputElement displayArea_)
        {
            displayArea = displayArea_;
        }
        private System.Windows.Point mousePos;
        public System.Windows.Point MousePos
        {
            get { return mousePos; }
            set
            {
                if (mousePos != value)
                {
                    mousePos = value;
                    RaisePropertyChanged("MousePos");
                }
            }
        }
        private double[] homographyMatrix = new double[9];
        public double[] HomographyMatrix
        {
            get { return homographyMatrix; }
            set
            {
                if (!homographyMatrix.SequenceEqual(value))
                {
                    homographyMatrix = value;
                    RaisePropertyChanged("HomographyMatrix");
                }
            }
        }

        private double dstMinX = 0;
        public double DstMinX
        {
            get { return dstMinX; }
            set
            {
                if (dstMinX != value)
                {
                    dstMinX = value;
                    RaisePropertyChanged("DstMinX");
                }
            }
        }
        private double dstMinY = 0;
        public double DstMinY
        {
            get { return dstMinY; }
            set
            {
                if (dstMinY != value)
                {
                    dstMinY = value;
                    RaisePropertyChanged("DstMinY");
                }
            }
        }
        private double dstMaxX = 10;
        public double DstMaxX
        {
            get { return dstMaxX; }
            set
            {
                if (dstMaxX != value)
                {
                    dstMaxX = value;
                    RaisePropertyChanged("DstMaxX");
                }
            }
        }
        private double dstMaxY = 10;
        public double DstMaxY
        {
            get { return dstMaxY; }
            set
            {
                if (dstMaxY != value)
                {
                    dstMaxY = value;
                    RaisePropertyChanged("DstMaxY");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            var d = PropertyChanged;
            if (d != null)
                d(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
