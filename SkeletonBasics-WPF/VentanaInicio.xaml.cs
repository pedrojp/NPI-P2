using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    /// <summary>
    /// Lógica de interacción para Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public Window1()
        {
            InitializeComponent();
            this.campo_m_error.Text = "5";
            this.campo_series.Text = "5";
            this.campo_repeticiones.Text = "5";
        }

        private void Click(object sender, RoutedEventArgs e)
        {
            MainWindow.m_error = System.Convert.ToDouble(this.campo_m_error.Text)/100;
            MainWindow.series = System.Convert.ToInt16(this.campo_series.Text);
            MainWindow.repeticiones = System.Convert.ToInt16(this.campo_repeticiones.Text);
           // System.Windows.MessageBox.Show("Mee");
            this.Close();
        }



    }
}
