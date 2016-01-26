using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using fftwlib;
using NAudio.Wave;

namespace AccentTutor
{
    public class FftDataAvailableHandlerArgs : EventArgs
    {
        private float[] fftData;
        public FftDataAvailableHandlerArgs(float[] fftData)
        {
            this.fftData = fftData;
        }

        public float[] FftData
        {
            get
            {
                return fftData;
            }
        }
    }

    public delegate void FftDataAvailableHandler(object sender, FftDataAvailableHandlerArgs e);

    public class AudioInFft : IDisposable
    {
        private bool isDisposed = false;

        public const int SAMPLE_RATE = 44100;
        public const int SAMPLES_IN_FFT = 44100 / 2;
        public const int BIT_DEPTH = 16;

        WaveIn waveIn;

        IntPtr fftwDataIn;
        IntPtr fftwDataOut;
        IntPtr fftwPlan;

        float[] dataIn;
        float[] dataOut;

        public FftDataAvailableHandler FftDataAvilable;

        public AudioInFft()
        {
            int i = WaveIn.DeviceCount;
            if (WaveIn.DeviceCount <= 0)
            {
                // We need a device!
                Debug.WriteLine("We have no microphone AHHH!");
                return;
            }
            
            initFftw(SAMPLES_IN_FFT);
        }

        public void Stop() {
            waveIn.StopRecording();
        }

        public void Start() {
            waveIn = new WaveIn();
            waveIn.DeviceNumber = 0;
            // The number of milliseconds to get a buffer size of SAMPLES_IN_FFT
            waveIn.BufferMilliseconds = 1000 / (SAMPLE_RATE / SAMPLES_IN_FFT);
            waveIn.DataAvailable += waveIn_DataAvailable;
            // 44.1khz 16-bit mono
            waveIn.WaveFormat = new WaveFormat(SAMPLE_RATE, BIT_DEPTH, 1);
            
            waveIn.StartRecording();
        }

        void initFftw(int n)
        {
            //create two unmanaged arrays, properly aligned
            fftwDataIn = fftwf.malloc(n * sizeof(float));
            fftwDataOut = fftwf.malloc(n * sizeof(float));

            //create two managed arrays, possibly misalinged
            dataIn = new float[n];
            dataOut = new float[n];

            //copy managed arrays to unmanaged arrays
            Marshal.Copy(dataIn, 0, fftwDataIn, dataIn.Length);
            Marshal.Copy(dataOut, 0, fftwDataOut, dataOut.Length);

            //create a few test transforms
            fftwPlan = fftwf.r2r_1d(n, fftwDataIn, fftwDataOut, fftw_kind.DHT, fftw_flags.Measure);
        }

        void freeFftw()
        {
            fftwf.free(fftwDataIn);
            fftwf.free(fftwDataOut);
            fftwf.destroy_plan(fftwPlan);
        }

        // The hann windowing function at i for a size N window.
        float hannWindow(int i, int N)
        {
            return (float)(0.5 * (1.0 - Math.Cos(2 * Math.PI * i / (N - 1))));
        }

        void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded != SAMPLES_IN_FFT * (BIT_DEPTH / 8))
            {
                Debug.WriteLine("Data available was not what was expected");
                return;
            }

            // Copy over data into our array... applying a hann window
            for (int i = 0; i < SAMPLES_IN_FFT; i++)
            {
                dataIn[i] = e.Buffer[i] * hannWindow(i, SAMPLES_IN_FFT);
            }

            // Copy the data over to the FFT's area
            Marshal.Copy(dataIn, 0, fftwDataIn, dataIn.Length);

            // Do the FFT!
            fftwf.execute(fftwPlan);

            // Get the data back!
            Marshal.Copy(fftwDataOut, dataOut, 0, dataOut.Length);

            if (FftDataAvilable != null)
            {
                FftDataAvilable(this, new FftDataAvailableHandlerArgs(dataOut));
            }
        }

        #region dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposingManaged)
        {
            if (!isDisposed)
            {
                freeFftw();
                isDisposed = true;
            }
        }

        ~AudioInFft()
        {
            Dispose(false);
        }

        #endregion
    }
}
