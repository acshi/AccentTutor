﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics;
//using fftwlib;

namespace SpectrumAnalyzer {
    public class FftDataAvailableHandlerArgs : EventArgs {
        private float[] fftData;
        public FftDataAvailableHandlerArgs(float[] fftData) {
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

    public class FftProcessor : IDisposable {
        private bool isDisposed = false;

        //public const int SAMPLES_IN_FFT = 44100 / 2;
        public const int SAMPLES_IN_UPDATE = 44100 / 5;
        public const int FFT_LENGTH = 16384;

        //IntPtr fftwDataIn;
        //IntPtr fftwDataOut;
        //IntPtr fftwPlan;

        //float[] dataIn;
        //float[] dataOut;
        float[] windowData;
        Complex[] dataIn;
        Complex[] complexDataOut;
        float[] dataOut;

        public FftDataAvailableHandler FftDataAvilable;

        public FftProcessor() {
            initFftw(FFT_LENGTH);
        }

        void initFftw(int n) {
            //create two unmanaged arrays, properly aligned
            //fftwDataIn = fftwf.malloc(n * sizeof(float));
            //fftwDataOut = fftwf.malloc(n * sizeof(float));

            //create two managed arrays, possibly misalinged
            //dataIn = new float[n];
            windowData = new float[n];
            dataIn = new Complex[n];
            complexDataOut = new Complex[n];
            dataOut = new float[n];

            //copy managed arrays to unmanaged arrays
            //Marshal.Copy(dataIn, 0, fftwDataIn, dataIn.Length);
            //Marshal.Copy(dataOut, 0, fftwDataOut, dataOut.Length);

            //create a few test transforms
            //fftwPlan = fftwf.r2r_1d(n, fftwDataIn, fftwDataOut, fftw_kind.DHT, fftw_flags.Measure);
        }

        void freeFftw() {
            //fftwf.free(fftwDataIn);
            //fftwf.free(fftwDataOut);
            //fftwf.destroy_plan(fftwPlan);
        }

        // The hann windowing function at i for a size N window.
        float hannWindow(int i, int N) {
            return (float)(0.5 * (1.0 - Math.Cos(2 * Math.PI * i / (N - 1))));
        }

        public void ProcessSamples(float[] samples) {
            if (samples.Length != SAMPLES_IN_UPDATE) {
                Debug.WriteLine("Data available was not what was expected");
                return;
            }

            // Move the window to make room for new data
            Array.Copy(windowData, SAMPLES_IN_UPDATE, windowData, 0, FFT_LENGTH - SAMPLES_IN_UPDATE);
            // Insert new data
            Array.Copy(samples, 0, windowData, FFT_LENGTH - SAMPLES_IN_UPDATE, SAMPLES_IN_UPDATE);

            // Copy over data into our array... applying a hann window
            for (int i = 0; i < FFT_LENGTH; i++) {
                dataIn[i] = windowData[i] * hannWindow(i, FFT_LENGTH);
            }

            // Copy the data over to the FFT's area
            //Marshal.Copy(dataIn, 0, fftwDataIn, dataIn.Length);

            // Do the FFT!
            //fftwf.execute(fftwPlan);

            // Get the data back!
            //Marshal.Copy(fftwDataOut, dataOut, 0, dataOut.Length);

            dataIn.CopyTo(complexDataOut, 0);
            Fourier.Radix2Forward(complexDataOut, FourierOptions.NoScaling);
            for (int i = 0; i < complexDataOut.Length; i++) {
                dataOut[i] = (float)complexDataOut[i].Magnitude;
            }

            if (FftDataAvilable != null) {
                FftDataAvilable(this, new FftDataAvailableHandlerArgs(dataOut));
            }
        }

        #region dispose

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposingManaged) {
            if (!isDisposed) {
                freeFftw();
                isDisposed = true;
            }
        }

        ~FftProcessor() {
            Dispose(false);
        }

        #endregion
    }
}
