using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Dmo;
using NAudio.Wave;
using System.Diagnostics;
using System.Windows.Media;
using System.Collections.ObjectModel;

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
    public class SampleQueue : Queue<Sample> { }
    public class Complex
    {
        public double Real { get; set; }
        public double Imaginary { get; set; }

        public Complex()
        {
            Real = 0;
            Imaginary = 0;
        }

        public Complex(double real, double imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public static Complex operator *(Complex a, Complex b)
        {
            return new Complex((a.Real * b.Real - a.Imaginary * b.Imaginary), (a.Real * b.Imaginary + a.Imaginary * b.Real));
        }

        public static Complex operator +(Complex a, Complex b)
        {
            return new Complex((a.Real + b.Real), (a.Imaginary + b.Imaginary));
        }

        public static Complex operator -(Complex a, Complex b)
        {
            return new Complex((a.Real - b.Real), (a.Imaginary - b.Imaginary));
        }

        public override string ToString()
        {
            return ("Real: " + Real + " Imaginary: " + Imaginary);
        }
        public double Magnitude()
        {
            return Math.Sqrt(SquaredMagnitude());
        }

        public double SquaredMagnitude()
        {
            return Real * Real + Imaginary * Imaginary;
        }
    }

    public class Audio
    {
        private static readonly double PI = 3.14159265259;
        private static readonly int NUM_SAMPLES_TO_PROCESS = 4096; // The nummber of sample to wait for before processing

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

        public double PitchFactor { get; set; }
        public ObservableCollection<double> GraphXData { get; set; }
        public ObservableCollection<double> GraphYData { get; set; }

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
            PitchFactor = 1;
            GraphXData = new ObservableCollection<double>();
            GraphYData = new ObservableCollection<double>();

            GraphXData.Add(1);
            GraphYData.Add(1);
            GraphXData.Add(1);
            GraphYData.Add(1);
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

        public void StartCapture()
        {
            micAudioClient.Start();
            speakAudioClient.Start();

            BenchmarkFFT(1);
            GraphXData.Add(1);

            cancelTokenSource = new CancellationTokenSource();
            captureTask = new Task(GetCaptureData, cancelTokenSource.Token);
            captureTask.Start();
        }
        public void StopCapture()
        {
            cancelTokenSource.Cancel();

            captureTask.Wait(); // Wait for task to finish
            micAudioClient.Stop();
            speakAudioClient.Stop();
            captureTask.Dispose();
        }

        public void GetCaptureData()
        {
            int numChannels = micAudioClient.MixFormat.Channels;
            List<SampleQueue> micData = new List<SampleQueue>(numChannels);
            for (int i = 0; i < numChannels; ++i)
            {
                micData.Add(new SampleQueue());
            }

            // Check for speaker supported format
            if (!(speakAudioClient.MixFormat.Encoding == WaveFormatEncoding.Extensible &&
                ((WaveFormatExtensible)micAudioClient.MixFormat).SubFormat.Equals(new Guid("00000003-0000-0010-8000-00aa00389b71"))))
            {
                throw new Exception("Capture format not supported." + micAudioClient.MixFormat.Encoding.ToString());
            }
            // Check for microphone supported format
            if (!(micAudioClient.MixFormat.Encoding == WaveFormatEncoding.Extensible &&
                ((WaveFormatExtensible)micAudioClient.MixFormat).SubFormat.Equals(new Guid("00000003-0000-0010-8000-00aa00389b71"))))
            {
                throw new Exception("Capture format not supported." + micAudioClient.MixFormat.Encoding.ToString());
            }

            while (!cancelTokenSource.Token.IsCancellationRequested)
            {
                // Capture data ========================================================================================================
                int nextPacketSize = micAudioClient.AudioCaptureClient.GetNextPacketSize();
                if (nextPacketSize != 0)
                {
                    int framesCaptured = micData[0].Count;
                    while (framesCaptured < NUM_SAMPLES_TO_PROCESS)
                    {
                        unsafe
                        {
                            // Read Frames from microphone
                            void* pData = micAudioClient.AudioCaptureClient.GetBuffer(out int numFrames, out AudioClientBufferFlags buffFlags, out long devicePosition, out long qpcPosition).ToPointer();
                            for (int frame = 0; frame < numFrames; ++frame)
                            {
                                for (int channel = 0; channel < numChannels; ++channel)
                                {
                                    Sample sample = new Sample((byte*)pData, micAudioClient.MixFormat.BitsPerSample);
                                    pData = (byte*)pData + micAudioClient.MixFormat.BlockAlign;
                                    micData.ElementAt(channel).Enqueue(sample);
                                }
                                ++framesCaptured;
                            }
                            micAudioClient.AudioCaptureClient.ReleaseBuffer(numFrames);
                        }
                    }

                    // Process audio ===========================================================================================================

                    // Only process the first NUM_SAMPLES_TO_PROCESS
                    List<Channel> speakData = new List<Channel>();
                    foreach (var ch in micData)
                    {
                        speakData.Add(new Channel());
                        for (int i = 0; i < NUM_SAMPLES_TO_PROCESS; ++i)
                        {
                            speakData[speakData.Count - 1].Add(ch.Dequeue());
                        }
                    }

                    // Combine all channels into a single channel
                    speakData = ModifyChannels(speakData, 1);
                    Channel processData = speakData[0];


                    FFT(processData, out Complex[] frequencies);

                    PrintPeakFrequencies(ref frequencies, micAudioClient.MixFormat.SampleRate);

                    IFFT(frequencies, out Channel IFFTsamples);

                    // Modify Speaker data to correct number of channels
                    speakData[0] = processData;
                    speakData = ModifyChannels(speakData, speakAudioClient.MixFormat.Channels);

                    // Write data ====================================================================================================================
                    int framesRequested = speakData.ElementAt(0).Count;
                    unsafe
                    {
                        void* pData = speakAudioClient.AudioRenderClient.GetBuffer(framesRequested).ToPointer();
                        for (int frame = 0; frame < framesRequested; ++frame)
                        {
                            foreach (var ch in speakData)
                            {
                                *(float*)pData = ch.ElementAt(frame).ToFloat();
                                pData = (float*)pData + 1;
                            }
                        }
                    }
                    speakAudioClient.AudioRenderClient.ReleaseBuffer(framesRequested, AudioClientBufferFlags.None);
                }
                else
                {
                    // maybe wait
                    Thread.Sleep(1);
                }        
            }
        }

        private List<Channel> Resample(List<Channel> data, uint numSamples)
        {
            if (numSamples == 0)
            {
                throw new Exception("Can not resample to have 0 samples.");
            }
            //TODO: check if data is well formed

            // Check if we need to do anything
            if (data[0].Count == numSamples)
            {
                return data;
            }

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

        // TODO: Completely broken
        private void TimeStretchModification(ref List<Channel> data, double factor)
        {
            double threshold = 0.001;
            if (data.Count <= 0)
            {
                throw new Exception("Number of channels must be non-zero and non-negative.");
            }
            else if (factor <= 0)
            {
                throw new Exception("Time Stretch Factor must be non-negative");
            }

            if (factor > 1 + threshold)
            {
                // Assume every channel has the same number of samples
                uint numOutFrames = (uint)Math.Ceiling(checked(data[0].Count * factor));
                foreach (var ch in data)
                {           
                    while (ch.Count < numOutFrames)
                    {
                        ch.Add(new Sample(0f));
                    }
                }
            }
            else if (factor < 1 - threshold)
            {
                // Assume every channel has the same number of samples
                uint numOutFrames = (uint)Math.Floor(checked(data[0].Count * factor));
                uint numFramesToRemove = (uint)data[0].Count - numOutFrames;
                uint stepSize = (uint)data[0].Count / numFramesToRemove;
            }
        }

        private void FFT(Channel data, out Complex[] output)
        {
            // Find power of 2
            int power = 1;
            while ((data.Count / (double)power) > 1)
            {
                power *= 2;
            }

            // Copy input data into complex number form
            output = new Complex[power];
            int i = 0;
            for (; i < data.Count; ++i)
            {
                output[i] = new Complex(data[i].ToFloat(), 0);
            }

            // Zero pad to next power of 2
            while (i < power)
            {
                output[i] = new Complex();
            }
            
            // Call FFT implementation
            output = FFT_Recursive(output, power, false);
        }
        private void IFFT(Complex[] data, out Channel output)
        {
            // Check if data length is of power 2
            double temp = data.Length;
            while (temp != 1)
            {
                if (temp % 2 != 0)
                    throw new Exception("data length must be a power of 2 in IFFT.");
                temp /= 2;
            }

            data = FFT_Recursive(data, data.Length, true);
            // Copy data over to a channel format
            output = new Channel();
            foreach (var complex in data)
            {
                output.Add(new Sample((float)complex.Real / data.Length));
            }
        }

        /// <summary>
        /// Recursice part of FFT
        /// </summary>
        /// <param name="output"></param>
        /// <param name="N"></param>
        /// <param name="stride"></param>
        private Complex[] FFT_Recursive(Complex[] data, int N, bool inverse)
        {
            if (N <= 1)
            {
                return data;
            }

            Complex[] even = new Complex[N / 2];
            Complex[] odd = new Complex[N / 2];
            for (int i = 0; i < N / 2; ++i)
            {
                even[i] = data[2 * i];
                odd[i] = data[2 * i + 1];
            }
            // Even indexs
            even = FFT_Recursive(even, N / 2, inverse);
            // Odd indexs
            odd = FFT_Recursive(odd, N / 2, inverse);


            double EXPONENT = 2 * PI / N;
            if (!inverse)
                EXPONENT *= -1;
            // Combine result
            for (int k = 0; k < N / 2; ++k)
            {
                Complex c = new Complex(Math.Cos(EXPONENT * k), Math.Sin(EXPONENT * k));
                data[k] = even[k] + c * odd[k];
                data[k + N / 2] = even[k] - c * odd[k];
            }
            return data;
        }

        

        /// <summary>
        /// Discrete Fourier Transform
        /// </summary>
        /// <param name="data"></param>
        /// <param name="samplingFreq"></param>
        /// <returns></returns>
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

            return returnData;            
        }

        /// <summary>
        /// Inverse Discrete Fourier Transform
        /// </summary>
        /// <param name="data"> The complex numbers to get the inverse from </param>
        /// <returns> The real numbers in the inverse. </returns>
        private void IDFT(Complex[] data, out Channel output)
        {
            output = new Channel();

            double EXPONENT = 2 * PI / data.Length;
            for (int sample = 0; sample < data.Length; ++sample)
            {
                // For our use we don't care about the imaginary part
                double real = 0;
                double constant = EXPONENT * sample;
                for (int freqBin = 0; freqBin < data.Length; ++freqBin)
                {
                    real += data[freqBin].Real * Math.Cos(constant * freqBin) - data[freqBin].Imaginary * Math.Sin(constant * freqBin);
                }
                real /= data.Length;

                output.Add(new Sample((float)real));
            }
        }

        private void BenchmarkFFT(uint numRepitions = 1)
        {
            Console.WriteLine("Beginning FFT Benchmark ========================================================");
            // Set up data
            Channel data = new Channel();

            int signalFrequency = 1;
            int time = 2;
            int numCycles = time * signalFrequency;
            int numSamples = 2048;
            int sampleRate = numSamples / time;

            for (int i = 0; i < numSamples; ++i)
            {
                data.Add(new Sample((float)Math.Sin(i * (numCycles * 2 * PI / numSamples))));
            }

            // Warm up
            FFT(data, out Complex[] temp);
            temp = DFT(data, 44100);

            long FFT_Time = 0;
            long DFT_Time = 0;
            for (uint rep = 0; rep < numRepitions; ++rep)
            {
                // FFT
                Stopwatch sw = Stopwatch.StartNew();
                FFT(data, out Complex[] frequencies);
                sw.Stop();
                FFT_Time += sw.ElapsedMilliseconds;
                Console.WriteLine("FFT took {0} ms.", sw.ElapsedMilliseconds);
                for (int i = 0; i < numSamples / 2; ++i)
                {
                    Console.Write("Frequency: {0} " + frequencies[i].Magnitude() + " ", (double)i * sampleRate / numSamples);
                }
                Console.WriteLine();


                // DFT
                sw = Stopwatch.StartNew();
                frequencies = DFT(data, 44100);
                sw.Stop();
                DFT_Time += sw.ElapsedMilliseconds;
                Console.WriteLine("DFT took {0} ms.", sw.ElapsedMilliseconds);
                for (int i = 0; i < numSamples / 2; ++i)
                {
                    Console.Write("Frequency: {0} " + frequencies[i].Magnitude() + " ", (double)i * sampleRate / numSamples);
                }
                Console.WriteLine("\n=======================================================");
            }
            Console.WriteLine("Average Time FFT {0}\nAverage Time DFT {1}\n===================================================================", FFT_Time / numRepitions, DFT_Time / numRepitions);

        }
        private void PrintChannel(Channel data)
        {
            foreach (var sample in data)
            {
                Console.Write("{0}, ", sample.ToFloat());
            }
            Console.WriteLine();
        }
        private void PrintPeakFrequencies(ref Complex[] data, int samplingRate)
        {
            List<double> peaks = new List<double>();
            List<int> peakindex = new List<int>();

            double max = data[0].Magnitude();
            int index = 0;
            bool foundpeak = false;
            double threshold = 0.001; // Only show peaks that contain atleast 1/1000 power

            for (int i = 1; i < data.Length / 2; ++i)
            {
                if (max < data[i].Magnitude() && (data[i].Magnitude() / data.Length) > threshold)
                {
                    max = data[i].Magnitude();
                    foundpeak = true;
                    index = i;
                }

                if (foundpeak && (data[i].Magnitude() / data.Length) < 0.0001) // Close to zero so the peak has ended
                {
                    foundpeak = false;
                    peaks.Add(max);
                    peakindex.Add(index);
                    max = 0;
                }

            }
            
            // Print
            for (int i = 0; i < peaks.Count; ++i)
            {
                Console.WriteLine("Frequency Peak {0} found at {1} Hz (Frequency step size is {2} Hz)", peaks[i], (double)peakindex[i] * samplingRate / data.Length, (double)samplingRate / data.Length);
            }
            if (peaks.Count != 0)
                Console.WriteLine("==============================================================");
        }
    }
}
