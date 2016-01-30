using System;
using System.Diagnostics;
using NAudio.Wave;

namespace AccentTutor
{
    public class AudioAvailableHandlerArgs : EventArgs
    {
        private float[] samples;
        public AudioAvailableHandlerArgs(float[] samples)
        {
            this.samples = samples;
        }

        public float[] Samples
        {
            get
            {
                return samples;
            }
        }
    }

    public delegate void AudioAvailableHandler(object sender, AudioAvailableHandlerArgs e);

    public class AudioIn
    {

        public const int SAMPLE_RATE = 44100;
        public const int BIT_DEPTH = 16;
        public int samplesPerBatch;

        WaveIn waveIn;

        float[] data;

        public AudioAvailableHandler AudioAvilable;

        public AudioIn(int samplesPerBatch)
        {
            this.samplesPerBatch = samplesPerBatch;

            int i = WaveIn.DeviceCount;
            if (WaveIn.DeviceCount <= 0)
            {
                // We need a device!
                Debug.WriteLine("We have no microphone AHHH!");
                return;
            }
        }

        public void Stop() {
            waveIn.StopRecording();
        }

        public void Start() {
            waveIn = new WaveIn();
            waveIn.DeviceNumber = 0;
            // The number of milliseconds to get a buffer size of SAMPLES_IN_FFT
            waveIn.BufferMilliseconds = 1000 / (SAMPLE_RATE / samplesPerBatch);
            waveIn.DataAvailable += waveIn_DataAvailable;
            // 44.1khz 16-bit mono
            waveIn.WaveFormat = new WaveFormat(SAMPLE_RATE, BIT_DEPTH, 1);

            data = new float[samplesPerBatch];
            
            waveIn.StartRecording();
        }

        void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded != samplesPerBatch * (BIT_DEPTH / 8))
            {
                Debug.WriteLine("Data available was not what was expected");
                return;
            }

            // Put bytes into 16-bit samples as floats.
            for (int i = 0; i < samplesPerBatch; i++) {
                data[i] = (short)(e.Buffer[i * 2] | e.Buffer[i * 2 + 1] << 8);
            }

            if (AudioAvilable != null) {
                AudioAvilable(this, new AudioAvailableHandlerArgs(data));
            }
        }
    }
}
