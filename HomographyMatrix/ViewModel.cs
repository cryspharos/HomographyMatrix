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

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            var d = PropertyChanged;
            if (d != null)
                d(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
