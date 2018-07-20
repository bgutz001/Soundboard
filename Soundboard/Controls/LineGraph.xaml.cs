using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

namespace Soundboard.Controls
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class LineGraph : UserControl
    {
        public ObservableCollection<double> YData
        {
            get
            {
                return (ObservableCollection<double>)GetValue(YDataProperty);
            }
            set
            {
                value.CollectionChanged += DataChanged;
                SetValue(YDataProperty, value);
            }
        }
        public ObservableCollection<double> XData
        {
            get
            {
                return (ObservableCollection<double>)GetValue(XDataProperty);
            }
            set
            {
                value.CollectionChanged += DataChanged;
                SetValue(XDataProperty, value);
            }

        }
        #region Properties
        public static readonly DependencyProperty XDataProperty =
            DependencyProperty.Register(
                "XData",
                typeof(ObservableCollection<double>),
                typeof(LineGraph),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataPropertyChanged)));

        public static readonly DependencyProperty YDataProperty =
            DependencyProperty.Register(
                "YData",
                typeof(ObservableCollection<double>),
                typeof(LineGraph),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataPropertyChanged)));

        #endregion
        
        private static void OnDataPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {           
            LineGraph temp = d as LineGraph;
            temp.DataChanged(e);
        }

        private void DataChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == XDataProperty)
            {
                XData = e.NewValue as ObservableCollection<double>;
                DataChanged(XData, new EventArgs());
                return;
            }
            else if (e.Property == YDataProperty)
            {
                YData = e.NewValue as ObservableCollection<double>;
                DataChanged(YData, new EventArgs());
                return;
            }
            Console.WriteLine("Unknown property " + e.Property + " changed");
        }

        private void DataChanged(object sender, EventArgs e)
        {
            if (sender as ObservableCollection<double> == XData)
            {
                Console.WriteLine("XData Item Change");
            }
            else if (sender as ObservableCollection<double> == YData)
            {
                Console.WriteLine("YData Item Change");
            }
            Redraw();
        }

        private void Redraw()
        {
            Line temp = new Line();
            temp.Margin = new Thickness(10, 10, 10, 10);
            temp.Visibility = System.Windows.Visibility.Visible;
            temp.Stroke = System.Windows.Media.Brushes.Black;
            temp.StrokeThickness = 4;
            temp.X1 = 0;
            temp.Y1 = 0;
            temp.X2 = 10;
            temp.Y2 = 10;
            this.Canvas.Children.Add(temp);

            Console.WriteLine("Done Redrawinng");
        }

        void CaptureClick(object Sender, RoutedEventArgs e)
        {
            Console.WriteLine("LineGraph click.");
            if (XData != null)
                Console.WriteLine("XData Count {0}", XData.Count);
            if (YData != null)
                Console.WriteLine("YData Count {0}", YData.Count);

            XData.Add(1);
            e.Handled = true;
        }

        public LineGraph()
        {
            InitializeComponent();
            
        }



    }
}
