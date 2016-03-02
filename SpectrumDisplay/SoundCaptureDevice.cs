using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Reflection;
using Microsoft.DirectX.DirectSound;

namespace InstrumentalistTuner
{
    public class SoundCaptureDevice
    {
        public delegate void DataBlockRetrieved(short[] data);

        private DeviceInformation inputDevice;
        private WaveFormat inputFormat;

        private DataBlockRetrieved callback;

        private int samplesPerBlock;
        private int bytesPerBlock;

        private int bufferMultiplier;

        private bool captureAborted;

        /// <summary>
        /// Gets whether this SoundCaptureDevice has aborted capture do so
        /// some external change. If the problem is believed to be resolved,
        /// capture may be attempted to be restarted.
        /// </summary>
        public bool CaptureAborted
        {
            get { return captureAborted; }
        }

        private bool isCapturing;

        /// <summary>
        /// Gets whether this SoundCaptureDevice is currently capturing data.
        /// Aside from the start and stop methods, capturing may stop when the
        /// physical device is removed or another external change prevents its continuing.
        /// </summary>
        public bool IsCapturing
        {
            get { return isCapturing; }
        }
        private bool shouldQuit;

        /// <summary>
        /// Gets or sets the format to be/being captured
        /// </summary>
        public WaveFormat Format
        {
            set
            {
                if (isCapturing) {
                    throw new InvalidOperationException("Input format cannot be changed while capturing is in progress");
                }
                inputFormat = value;
            }
            get { return inputFormat; }
        }

        /// <summary>
        /// Gets or sets the sampling rate, adjusting AverageBytesPerSecond as well to reflect any changes
        /// </summary>
        public int SamplesPerSecond
        {
            set
            {
                if (isCapturing) {
                    throw new InvalidOperationException("Sampling rate cannot be changed while capturing is in progress");
                }
                inputFormat.SamplesPerSecond = value;
                inputFormat.AverageBytesPerSecond = inputFormat.BlockAlign * inputFormat.SamplesPerSecond;
            }

            get { return inputFormat.SamplesPerSecond; }
        }


        /// <summary>
        /// Creates a new sound capture device to capture sound with a certain format from a certain device
        /// </summary>
        /// <param name="device">The device to capture from</param>
        /// <param name="format">The format to capture with</param>
        public SoundCaptureDevice(DeviceInformation device, WaveFormat format)
        {
            inputDevice = device;
            inputFormat = format;

            isCapturing = false;
            shouldQuit = false;
        }

        /// <summary>
        /// Starts a new thread to capture sound input data and return it to a callback in
        /// specified block sizes
        /// </summary>
        /// <param name="callbackDelegate">The callback delegate to receive data blocks</param>
        /// <param name="samplesPerBlock">The number of samples per datablock to return</param>
        /// <param name="bufferMultiplier">The multiplier of the datablock size for the read buffer</param>
        public void StartCapture(DataBlockRetrieved callbackDelegate, int samplesPerBlock, int bufferMultiplier)
        {
            lock (this) {
                if (isCapturing) {
                    throw new InvalidOperationException("Already capturing");
                }
                isCapturing = true;
                captureAborted = false;
            }

            callback = callbackDelegate;

            this.samplesPerBlock = samplesPerBlock;
            bytesPerBlock = samplesPerBlock * inputFormat.BlockAlign;//BlockAlign is one sample
            this.bufferMultiplier = bufferMultiplier;

            //Start capturing
            new Thread(captureLoop).Start();
        }

        /// <summary>
        /// Stops capturing
        /// </summary>
        public void StopCapture()
        {
            shouldQuit = true;
        }

        private void captureLoop()
        {
            try {
                Capture capture = new Capture(inputDevice.DriverGuid);

                CaptureBufferDescription bufferDescription = defaultContructor<CaptureBufferDescription>();
                bufferDescription.Format = inputFormat;
                bufferDescription.BufferBytes = bytesPerBlock * bufferMultiplier;

                CaptureBuffer buffer = new CaptureBuffer(bufferDescription, capture);

                buffer.Start(true);

                int nextRead = 0;//The position from next to read

                int lastReadPosition = 0;//The last read position from GetCurrentPosition

                while (!shouldQuit) {

                    int capturePosition;//The end of the current block being read into; I don't need this
                    int readPosition;//I can read as far as this

                    buffer.GetCurrentPosition(out capturePosition, out readPosition);

                    //Do we have enough data to read?
                    int nextReadEnd = (nextRead + bytesPerBlock) % bufferDescription.BufferBytes;
                    //Ensure that reading has been done up to the end of the block requested
                    if (readPosition > nextReadEnd || readPosition < lastReadPosition) {
                        short[] data = (short[])buffer.Read(nextRead, typeof(short), LockFlag.None, samplesPerBlock);
                        nextRead = nextReadEnd;
                        callback(data);
                    } else {
                        Thread.Sleep(1);
                    }

                    lastReadPosition = readPosition;
                }

                buffer.Stop();
                capture.Dispose();
            } catch (Exception e) {
                if (!DeviceExists(this)) {
                    captureAborted = true;
                    System.Console.Out.WriteLine("Device was removed/unplugged, capture aborted!");
                } else {
                    System.Console.Out.WriteLine("Unrecognized Error: " + e);
                }
            }

            shouldQuit = false;
            isCapturing = false;
        }

        /// <summary>
        /// Creates a single channel, 16 bit, 44100 hz, PCM wave input format
        /// </summary>
        /// <returns>The default format</returns>
        public static WaveFormat GetDefaultFormat()
        {
            WaveFormat inputFormat = defaultContructor<WaveFormat>();

            inputFormat.Channels = 1;
            inputFormat.BitsPerSample = 16;
            inputFormat.SamplesPerSecond = 44100;
            inputFormat.FormatTag = WaveFormatTag.Pcm;

            inputFormat.BlockAlign = (short)Math.Ceiling((inputFormat.Channels * inputFormat.BitsPerSample) / 8.0);
            inputFormat.AverageBytesPerSecond = inputFormat.BlockAlign * inputFormat.SamplesPerSecond;

            return inputFormat;
        }

        //Pointless function that uses reflection to call the default contructor of a type
        //Prevents me from getting pointless annoying fake errors
        private static T defaultContructor<T>()
        {
            return (T)typeof(T).GetConstructor(System.Type.EmptyTypes).Invoke(null);
        }

        /// <summary>
        /// Finds out whether the actual device referenced by SoundCaptureDevice still exists.
        /// </summary>
        /// <param name="captureDevice">The SoundCaptureDevice to check</param>
        /// <returns>True if and only if the referenced driver guid is in the
        /// CaptureDevicesCollection or if it is Guid.Empty and at least one other device exists,
        /// false otherwise.</returns>
        public static bool DeviceExists(SoundCaptureDevice captureDevice)
        {
            if (captureDevice == null)
            {
                return false;
            }
            CaptureDevicesCollection allDevices = new CaptureDevicesCollection();
            foreach (DeviceInformation device in allDevices) {
                //Checks for guid,
                if (device.DriverGuid == captureDevice.inputDevice.DriverGuid &&
                    (device.DriverGuid != Guid.Empty || allDevices.Count > 1)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a default device for sound capture, null if one does not exist or
        /// if their are no actual devices to be made default by the default.
        /// </summary>
        /// <returns>The defualt sound capture device</returns>
        public static SoundCaptureDevice GetDefaultDevice()
        {
            CaptureDevicesCollection allDevices = new CaptureDevicesCollection();
            foreach (DeviceInformation device in allDevices) {
                if (device.DriverGuid == Guid.Empty && allDevices.Count > 1) {
                    return new SoundCaptureDevice(device, GetDefaultFormat());
                }
            }
            return null;
        }
    }
}
