using System;
using System.Collections.Generic;
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
using NAudio.CoreAudioApi;

namespace Soundboard
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Audio audioProcessor;
        private List<MMDevice> microphones;
        private List<MMDevice> speakers;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;       

            audioProcessor = new Audio();
            this.slider.DataContext = audioProcessor;
            this.grphFreq.DataContext = audioProcessor;

            microphones = new List<MMDevice>();
            speakers = new List<MMDevice>();    

            // Fill microphone list
            audioProcessor.GetMicrophones(microphones);
            foreach (var i in microphones)
            {
                this.cmbMicrophones.Items.Add(i.DeviceFriendlyName);
                this.cmbMicrophones.SelectedIndex = 0;
            }

            // Fill speakers list
            audioProcessor.GetSpeakers(speakers);
            foreach (var i in speakers)
            {
                this.cmbSpeakers.Items.Add(i.DeviceFriendlyName);
                this.cmbSpeakers.SelectedIndex = 0;
            }
        }

        void MicrophoneSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            Console.WriteLine("INFO: Microphone selection changed index = " + cb.SelectedIndex.ToString());
            audioProcessor.SetMicrophone(microphones.ElementAt(cb.SelectedIndex));
            e.Handled = true;
        }
        void SpeakerSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            Console.WriteLine("INFO: Speaker selection changed index = " + cb.SelectedIndex.ToString());
            audioProcessor.SetSpeaker(speakers.ElementAt(cb.SelectedIndex));
            e.Handled = true;
        }

        void CaptureClick(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("INFO: Capture clicked");
            Button btn = sender as Button;
            if (btn.Content.Equals("Start Capture"))
            {
                btn.Content = "Stop Capture";
                audioProcessor.StartCapture();
            }
            else if (btn.Content.Equals("Stop Capture"))
            {
                btn.Content = "Start Capture";
                audioProcessor.StopCapture();
            }
            e.Handled = true;
        }

        void PitchFactorChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            e.Handled = true;
        }
    }
}