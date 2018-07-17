using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Dmo;


namespace Soundboard
{
    public class Sample
    {
        private int value;
        public unsafe Sample(byte* d, int numBits)
        {
            int tempValue;
            if (numBits / 8.0 > sizeof(int))
            {
                // TODO: Error message
                throw new Exception("ERROR");
            }
            byte* temp = (byte*)&tempValue;
            for (int i = 0; i < Math.Ceiling(numBits / 8.0); ++i) // Get every byte containing info we need
            {
                *temp++ = *d++;
            }
            // Shift right so padding is on the left
            value = tempValue >> (sizeof(int) * 8 - numBits);
        }

        public Sample(Sample s)
        {
            value = s.value;
        }

        public unsafe Sample(float d)
        {
            value = *(int*)&d;
        }

        public Sample(int d)
        {
            value = d;
        }

        public unsafe float ToFloat()
        {
            int temp = value;
            return *(float*)&temp;
        }

        public int ToInt()
        {
            return value;
        }
    }
    public class Channel : List<Sample> { }
    public class Complex
    {
        private double real;
        private double imaginary;
        private double magnitude;

        public Complex()
        {
            real = 0;
            imaginary = 0;
            magnitude = 0;
        }

        public Complex(double r, double i)
        {
            real = r;
            imaginary = i;
            magnitude = real * real + imaginary * imaginary;
        }

        public void Set(double r, double i)
        {
            real = r;
            imaginary = i;
            magnitude = real * real + imaginary * imaginary;
        }

        public void SetRealPart(double r)
        {
            real = r;
            magnitude = real * real + imaginary * imaginary;
        }

        public void SetImagninaryPart(double i)
        {
            imaginary = i;
            magnitude = real * real + imaginary * imaginary;
        }

        public void GetRealPart(out double r)
        {
            r = real;
        }

        public void GetImaginaryPart(out double i)
        {
            i = imaginary;
        }

        public double Magnitude()
        {
            return Math.Sqrt(magnitude);
        }

        public double SquaredMagnitude()
        {
            return magnitude;
        }
    }

    public class Audio
    {
        private static readonly double PI = 3.14159;

        private MMDeviceEnumerator deviceEnumerator;
        private MMDevice microphone;
        private MMDevice speaker;

        private AudioClient micAudioClient;
        private AudioClient speakAudioClient;
        private Task captureTask;
        private CancellationTokenSource cancelTokenSource;

        private int micFrameSize;
        private int speakFrameSize;

        private Guid audioSession;

        private double pitchScale;
        
        public Audio()
        {
            deviceEnumerator = new MMDeviceEnumerator();
            microphone = null;
            speaker = null;
            micAudioClient = null;
            captureTask = null;
            cancelTokenSource = null;    
            micFrameSize = 0;
            speakFrameSize = 0;
            audioSession = new Guid();
            pitchScale = 2;
        }

        public void GetMicrophones(List<MMDevice> deviceNames)
        {
            MMDeviceCollection deviceCollection = deviceEnumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.Active);
            foreach (var i in deviceCollection)
            {
                deviceNames.Add(i);
            }
        }

        public void GetSpeakers(List<MMDevice> deviceNames)
        {
            MMDeviceCollection deviceCollection = deviceEnumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active);
            foreach (var i in deviceCollection)
            {
                deviceNames.Add(i);
            }
        }

        public void SetMicrophone(MMDevice mic)
        {
            microphone = mic;
            micAudioClient = mic.AudioClient;

            // Initialize AudioClient
            NAudio.Wave.WaveFormat waveFormat = micAudioClient.MixFormat;
            micAudioClient.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.None, 1000, 0, waveFormat, audioSession);
            int bufferSize = micAudioClient.BufferSize;
            micFrameSize = waveFormat.Channels * waveFormat.BitsPerSample / 8; // size in bytes
            Console.WriteLine("INFO: Microphone Buffer size " + bufferSize.ToString() + " Frame size " + micFrameSize.ToString());
            Console.WriteLine("INFO: Microphone wave format " + waveFormat.ToString());
        }

        public void SetSpeaker(MMDevice speak)
        {
            speaker = speak;
            speakAudioClient = speak.AudioClient;

            // Is 7.1 supported?
            Console.WriteLine(speakAudioClient.IsFormatSupported(AudioClientShareMode.Shared, new NAudio.Wave.WaveFormat(44100, 32, 2)));

            // Initalize AudioClient
            NAudio.Wave.WaveFormat waveFormat = speakAudioClient.MixFormat;
            speakAudioClient.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.None, 100000000, 0, waveFormat, audioSession);
            int bufferSize = speakAudioClient.BufferSize;
            speakFrameSize = waveFormat.Channels * waveFormat.BitsPerSample / 8; // size in bytes
            Console.WriteLine("INFO: Speaker Buffer size " + bufferSize.ToString() + " Frame Size " + speakFrameSize.ToString());
            Console.WriteLine("INFO: Speaker wave format " + waveFormat.ToString() + " encoding " + waveFormat.Encoding);
        }

        public double GetPitchScale()
        {
            return pitchScale;
        }

        public void SetPitchScale(double p)
        {
            pitchScale = p;
        }

        public void StartCapture()
        {
            micAudioClient.Start();
            speakAudioClient.Start();

            cancelTokenSource = new CancellationTokenSource();
            captureTask = new Task(GetCaptureData, cancelTokenSource.Token);
            captureTask.Start();
        }

        public void GetCaptureData()
        {
            int numChannels = micAudioClient.MixFormat.Channels;
            List<Channel> channels = new List<Channel>(numChannels);
            for (int i = 0; i < numChannels; ++i)
            {
                channels.Add(new Channel());
            }
            while (!cancelTokenSource.Token.IsCancellationRequested)
            {
                int nextPacketSize = micAudioClient.AudioCaptureClient.GetNextPacketSize();
                while (nextPacketSize != 0)
                {
                    unsafe
                    {
                        // Read Frames from microphone
                        void* pData = micAudioClient.AudioCaptureClient.GetBuffer(out int numFrames, out AudioClientBufferFlags buffFlags, out long devicePosition, out long qpcPosition).ToPointer();
                        for (int frame = 0; frame < numFrames; ++frame) // For every frame available
                        {
                            for (int channel = 0; channel < numChannels; ++channel) // For every Channel
                            {   
                                if (micAudioClient.MixFormat.Encoding == NAudio.Wave.WaveFormatEncoding.Extensible)
                                {
                                    switch (((NAudio.Wave.WaveFormatExtensible)micAudioClient.MixFormat).SubFormat.ToString())
                                    {
                                        case "00000003-0000-0010-8000-00aa00389b71": // Corresponds to WAVE_FORMAT_IEEE_FLOAT 

                                            Sample sample = new Sample((byte*)pData, micAudioClient.MixFormat.BitsPerSample);
                                            pData = (byte*)pData + micAudioClient.MixFormat.BlockAlign;
                                            channels.ElementAt(channel).Add(sample);
                                            break;
                                        default:
                                            throw new Exception("Capture format not supported." + micAudioClient.MixFormat.Encoding.ToString());
                                    }
                                }
 
                            }
                       
                        }
                        micAudioClient.AudioCaptureClient.ReleaseBuffer(numFrames);
                        List<Channel> speakData = channels;
                        DFT(channels.ElementAt(0), micAudioClient.MixFormat.SampleRate);

                        //speakData = PitchScale(channels, 2.0f);

                        // Modify input channels to output channels
                        speakData = ModifyChannels(speakData, speakAudioClient.MixFormat.Channels);

                        // Resample
                        // speakData = Resample(speakData, (uint)(speakData.ElementAt(0).Count * 1.5));

                        

                        // Check if data is well formed
                        // TODO
                        if (speakData.Count == 0)
                        {
                            Console.WriteLine("Attempted to output data with 0 channels.");
                            continue;
                        }


                        // Write Frames to speakers

                        int framesRequested = speakData.ElementAt(0).Count;
                        pData = speakAudioClient.AudioRenderClient.GetBuffer(framesRequested).ToPointer();
                        for (int frame = 0; frame < framesRequested; ++frame)
                        {
                            foreach (var ch in speakData)
                            {
                                if (speakAudioClient.MixFormat.Encoding == NAudio.Wave.WaveFormatEncoding.Extensible)
                                {
                                    switch (((NAudio.Wave.WaveFormatExtensible)speakAudioClient.MixFormat).SubFormat.ToString())
                                    {
                                        case "00000003-0000-0010-8000-00aa00389b71": // Corresponds to WAVE_FORMAT_IEEE_FLOAT 
                                            *(float*)pData = ch.ElementAt(frame).ToFloat();
                                            pData = (float*)pData + 1;
                                            break;
                                        default:
                                            throw new Exception("Render format not supported.");
                                    }
                                }
                                else
                                {
                                    throw new Exception("Render format not supported.");
                                }
                            }
                        }
                        speakAudioClient.AudioRenderClient.ReleaseBuffer(framesRequested, AudioClientBufferFlags.None);

                        // Clear data
                        foreach (var channel in channels)
                        {
                            channel.Clear();
                        }
                    }

                    nextPacketSize = micAudioClient.AudioCaptureClient.GetNextPacketSize();
                }
                Thread.Sleep(1);
            }
        }

        public void StopCapture()
        {
            cancelTokenSource.Cancel();
            micAudioClient.Stop();
            speakAudioClient.Stop();


            captureTask.Wait(); // Wait for task to finish
            captureTask.Dispose();         
        }

        private List<Channel> Resample(List<Channel> data, uint numSamples)
        {
            if (numSamples == 0)
            {
                throw new Exception("Can not resample to have 0 samples.");
            }
            //TODO: check if data is well formed

            List<Channel> returnData = new List<Channel>();
            foreach(var ch in data) // For every channel
            {
                // TODO: handle multiple data types
                returnData.Add(new Channel());
                // Average the high sample and the low sample
                for (uint sample = 0; sample < numSamples; ++sample)
                {
                    double index = (double)sample * (ch.Count - 1) / (numSamples - 1);
                    int highIndex = (int)Math.Ceiling(index);
                    int lowIndex = (int)Math.Floor(index);
                    double slope = ch.ElementAt(highIndex).ToFloat() - ch.ElementAt(lowIndex).ToFloat();
                    double output = slope * (index - lowIndex) + ch.ElementAt(lowIndex).ToFloat();
                    returnData[returnData.Count - 1].Add(new Sample((float)output));
                }
            }

            return returnData;
        }

        // TODO: handle for ints also
        // TODO: Is out naive implementation good enough?
        // TODO: Should we consider the actual channel (i.e. Front Left, Front Right, Center, etc.)?
        /// </summary>
        /// Modifies the the input data to have the correct number of channels specified by the outChannels parameter
        /// <param name="data"> list of channels containing the data we want to modify </param>
        /// <param name="outChannels"> The number of channels we want the data to have </param>
        /// <returns> New a list of channels that has the correct number of channels </returns>
        private List<Channel> ModifyChannels(List<Channel> data, int outChannels)
        {
            if (data.Count == outChannels)
            {
                return data;
            }
            else if (data.Count <= 0 || outChannels <= 0)
            {
                throw new Exception("Number of channels must be non-zero and non-negative.");
            }
            
            // Set up return data to have correct number of channels
            List<Channel> returnData = new List<Channel>();
            for (int i = 0; i < outChannels; ++i)
            {
                returnData.Add(new Channel());
            }

            // TODO: Is our naive implementation good enough?
            for (int frame = 0; frame < data.ElementAt(0).Count; ++frame)
            {
                // Average all the input channels to a single channel
                float frameAvg = 0f;
                for (int ch = 0; ch < data.Count; ++ch)
                {
                    frameAvg += data.ElementAt(ch).ElementAt(frame).ToFloat();
                }
                frameAvg /= data.Count;

                // Distribute average over all output channels
                for (int ch = 0; ch < outChannels; ++ch)
                {
                    returnData.ElementAt((int)ch).Add(new Sample(frameAvg));
                }
            }
            
            return returnData;
        }

        private List<Channel> TimeStretchModification(List<Channel> data, double factor)
        {
            if (data.Count <= 0)
            {
                throw new Exception("Number of channels must be non-zero and non-negative.");
            }
            else if (factor <= 0)
            {
                throw new Exception("Time Stretch Factor must be non-negative");
            }

            uint numOutFrames = (uint)Math.Ceiling(checked(data.Count * factor));

            return data;
        }

        private Complex[] DFT(Channel data, int samplingFreq)
        {
            Channel localData = data;
            // Zero pad to next power of 2
            int power = 1;
            while ((localData.Count / (double)power) > 1)
            {
                power *= 2;
            }
            while (localData.Count < power)
            {
                localData.Add(new Sample(0f));
            }

            // Set up return data array
            Complex[] returnData = new Complex[power];

            double EXPONENT = 2 * PI / power;

            // Calculate
            for (int freqBin = 0; freqBin < power; ++freqBin)
            {
                double constant = EXPONENT * freqBin;
                double real = 0, imaginary = 0;
                for (int sample = 0; sample < power; ++sample)
                {
                    real += localData.ElementAt(sample).ToFloat() * Math.Cos(constant * sample);
                    imaginary -= localData.ElementAt(sample).ToFloat() * Math.Sin(constant * sample);
                }
                returnData[freqBin] = new Complex(real, imaginary);               
            }

            IDFT(returnData);
            return returnData;            
        }

        private double[] IDFT(Complex[] data)
        {
            double[] returnData = new double[data.Length];

            double EXPONENT = 2 * PI / data.Length;
            for (int sample = 0; sample < data.Length; ++sample)
            {
                double real = 0;
                // double imaginary = 0;
                double constant = EXPONENT * sample;
                for (int freqBin = 0; freqBin < data.Length; ++freqBin)
                {
                    data[freqBin].GetRealPart(out double tempr);
                    data[freqBin].GetImaginaryPart(out double tempi);
                    real += tempr * Math.Cos(constant * freqBin) - tempi * Math.Sin(constant * freqBin);
                    // For our use we don't care about the imaginary part
                    // imaginary += tempi * Math.Cos(constant * freqBin) + tempr * Math.Sin(constant * freqBin);
                }
                real /= data.Length;
                // imaginary /= data.Length;

                returnData[sample] = real;      
            }

            return returnData;
        }

    }
}
