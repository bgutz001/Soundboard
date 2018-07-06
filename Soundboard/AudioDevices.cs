using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;


namespace Soundboard
{
    public class Audio
    {
        private MMDeviceEnumerator deviceEnumerator;
        private MMDevice microphone;
        private MMDevice speaker;

        private AudioClient micAudioClient;
        private AudioClient speakAudioClient;
        private Task captureTask;
        private CancellationTokenSource cancelTokenSource;

        private List<byte> data;

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
            data = new List<byte>();
            micFrameSize = 0;
            speakFrameSize = 0;
            audioSession = new Guid();

            pitchScale = 0;
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

            // Initalize AudioClient
            NAudio.Wave.WaveFormat waveFormat = speakAudioClient.MixFormat;
            speakAudioClient.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.None, 1000000, 0, waveFormat, audioSession);
            int bufferSize = speakAudioClient.BufferSize;
            speakFrameSize = waveFormat.Channels * waveFormat.BitsPerSample / 8; // size in bytes
            Console.WriteLine("INFO: Speaker Buffer size " + bufferSize.ToString() + " Frame Size " + speakFrameSize.ToString());
            Console.WriteLine("INFO: Speaker wave formate " + waveFormat.ToString());
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
            while (!cancelTokenSource.Token.IsCancellationRequested)
            {
                int nextPacketSize = micAudioClient.AudioCaptureClient.GetNextPacketSize();
                

                while (nextPacketSize != 0)
                {
                    unsafe
                    {
                        // Read Frames from microphone
                        List<float> data = new List<float>();
                        int frameByte, channel;
                        void* pData = micAudioClient.AudioCaptureClient.GetBuffer(out int numFrames, out AudioClientBufferFlags buffFlags, out long devicePosition, out long qpcPosition).ToPointer();
                        
                        for (int frame = 0; frame < numFrames; ++frame) // For every frame available
                        {
                            for (channel = 0; channel < micAudioClient.MixFormat.Channels; ++channel) // For every Channel
                            {
                                // Convert bytes to float
                                float temp = 0;
                                byte* pTemp = (byte*)&temp;
                                for (frameByte = 0; frameByte < micAudioClient.MixFormat.BitsPerSample/8; ++frameByte) // For every byte in the sample
                                {
                                    // TODO: if we have more than 4 bytes then we are accessing unallocated memory
                                    *pTemp++ = *(byte*)pData;
                                    pData = (byte*)pData + 1;
                                }
                                data.Add(temp); // Add float to data list
                            }
                        }
                        micAudioClient.AudioCaptureClient.ReleaseBuffer(numFrames);

                        // Scale the pitch
                        List<float> wData = ScalePitch(data);

                        // Output correct number of channels
                        wData = ModifyChannels(wData, (uint)micAudioClient.MixFormat.Channels, (uint)speakAudioClient.MixFormat.Channels);

                        // Write Frames to speaker
                        int framesRequested = wData.Count / speakAudioClient.MixFormat.Channels;
                        pData = speakAudioClient.AudioRenderClient.GetBuffer(framesRequested).ToPointer();
                        foreach (var sample in wData)
                        {
                            *(float*)pData = sample;
                            pData = (float*)pData + 1;
                        }
                        speakAudioClient.AudioRenderClient.ReleaseBuffer(framesRequested, AudioClientBufferFlags.None);
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


            captureTask.Wait();
            captureTask.Dispose();
            
        }

        private List<float> ScalePitch(List<float> data)
        {
            //
            return data;
        }


        /// </summary>
        /// If we
        /// <param name="data"> list of samples with interleaved channels </param>
        /// <returns> new frame data that has the correct number of channels </returns>
        private List<float> ModifyChannels(List<float> data, uint inChannels, uint outChannels)
        {
            if (inChannels == outChannels)
            {
                return data;
            }
            else if (inChannels == 0 || outChannels == 0)
            {
                throw new Exception("Number of channels must be non-zero.");
            }
            // TODO: Is out naive implementation good enough?
            // Average in channels to a single channel
            // Duplicate the average over all the out channels

            List<float> returnData = new List<float>();
            for (uint i = 0; i < data.Count; i += inChannels)
            {
                float frameAvg = 0f;
                for (uint j = 0; j < inChannels; ++j)
                {
                    frameAvg += data.ElementAt((int)(i + j));
                }
                frameAvg /= inChannels;

                for (uint j = 0; j < outChannels; ++j)
                {
                    returnData.Add(frameAvg);
                }
            }

            return returnData;
        }
    }
}
