using Android.Media;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AndroidAccentTutor
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
        public int samplesPerBatch;

        AudioRecord audioRecord;

        byte[] rawData;
        float[] data;

        public AudioAvailableHandler AudioAvailable;

        private bool isRecording;

        public AudioIn(int samplesPerBatch)
        {
            if ((1000f / ((float)SAMPLE_RATE / samplesPerBatch) % 1f != 0f)) {
                throw new ArgumentException("Samples per batch must make up an integer number of milliseconds.");
            }
            this.samplesPerBatch = samplesPerBatch;

            rawData = new byte[samplesPerBatch * 2];
            data = new float[samplesPerBatch];
            audioRecord = new AudioRecord(AudioSource.Mic, SAMPLE_RATE, ChannelIn.Mono, Encoding.Pcm16bit, samplesPerBatch * 2 * 10); // 10 Batches in the buffer.
        }

        private async Task audionInAsync() {
            audioRecord.StartRecording();
            while (isRecording) {
                int bytesRead = await audioRecord.ReadAsync(rawData, 0, data.Length);
                // Put bytes into 16-bit samples as floats.
                for (int i = 0; i < samplesPerBatch; i++) {
                    data[i] = (short)(rawData[i * 2] | rawData[i * 2 + 1] << 8);
                }
                if (AudioAvailable != null) {
                    AudioAvailable(this, new AudioAvailableHandlerArgs(data));
                }
            }
            audioRecord.Stop();
        }

        public void Stop() {
            isRecording = false;
        }

        public void Start() {
            isRecording = true;
            audionInAsync().ContinueWith(t => Debug.WriteLine(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
