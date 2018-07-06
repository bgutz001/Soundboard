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

            audioProcessor = new Audio();
            microphones = new List<MMDevice>();
            speakers = new List<MMDevice>();
            // Register selection changed event handler
            this.lbMicrophones.SelectionChanged += MicrophoneSelectionChanged;
            this.lbSpeakers.SelectionChanged += SpeakerSelectionChanged;
            this.btnCapture.Click += CaptureClick;


            // Fill microphone list
            audioProcessor.GetMicrophones(microphones);
            foreach (var i in microphones)
            {
                this.lbMicrophones.Items.Add(i.DeviceFriendlyName);
                this.lbMicrophones.SelectedIndex = 0;
            }

            // Fill speakers list
            audioProcessor.GetSpeakers(speakers);
            foreach (var i in speakers)
            {
                this.lbSpeakers.Items.Add(i.DeviceFriendlyName);
                this.lbSpeakers.SelectedIndex = 0;
            }
        }

        void MicrophoneSelectionChanged(object sender, EventArgs e)
        {
            Console.WriteLine("INFO: Microphone selection changed index = " + ((ListBox)sender).SelectedIndex.ToString());
            audioProcessor.SetMicrophone(microphones.ElementAt(((ListBox)sender).SelectedIndex));
        }
        void SpeakerSelectionChanged(object sender, EventArgs e)
        {
            Console.WriteLine("INFO: Speaker selection changed index = " + ((ListBox)sender).SelectedIndex.ToString());
            audioProcessor.SetSpeaker(speakers.ElementAt(((ListBox)sender).SelectedIndex));
        }

        void CaptureClick(object sender, EventArgs e)
        {
            Console.WriteLine("INFO: Capture clicked");
            Button btn = (Button) sender;
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
            
        }

    }
}