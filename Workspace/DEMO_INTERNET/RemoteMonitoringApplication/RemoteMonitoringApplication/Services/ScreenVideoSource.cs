//-----------------------------------------------------------------------------
// Filename: ScreenVideoSource.cs
//
// Description: Implements a video source from screen capture with optimized performance.
//
// Author(s):
// Remote Monitoring Application Team
//
// History:
// 27 May 2025	Initial version	Refactored based on VideoBitmapSource pattern.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace RemoteMonitoringApplication.Services
{
    public class ScreenVideoSource : IVideoSource, IDisposable
    {
        private const int VIDEO_SAMPLING_RATE = 90000;
        private const int MAXIMUM_FRAMES_PER_SECOND = 60;
        private const int DEFAULT_FRAMES_PER_SECOND = 15;
        private const int MINIMUM_FRAMES_PER_SECOND = 5;
        private const int TIMER_DISPOSE_WAIT_MILLISECONDS = 1000;
        private const int DEFAULT_TARGET_WIDTH = 1280;
        private const int DEFAULT_TARGET_HEIGHT = 720;
        private const int SRCCOPY = 0x00CC0020;

        private static readonly ILogger logger = NullLogger.Instance;

        public static readonly List<VideoFormat> SupportedFormats = new List<VideoFormat>
        {
            new VideoFormat(VideoCodecsEnum.VP8, 96, VIDEO_SAMPLING_RATE),
            new VideoFormat(VideoCodecsEnum.H264, 100, VIDEO_SAMPLING_RATE, "packetization-mode=1")
        };

        // Win32 API for faster screen capture
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        // Events
        public event RawVideoSampleDelegate OnVideoSourceRawSample = delegate { };
        public event EncodedSampleDelegate OnVideoSourceEncodedSample = delegate { };
        public event RawVideoSampleFasterDelegate OnVideoSourceRawSampleFaster = delegate { };
        public event SourceErrorDelegate OnVideoSourceError = delegate { };

        // Core fields
        private readonly object _lockObject = new object();
        private System.Threading.Timer _sendTimer;
        private int _frameSpacing;
        private bool _isStarted;
        private bool _isPaused;
        private bool _isClosed;
        private int _frameCount;
        private IVideoEncoder _videoEncoder;
        private MediaFormatManager<VideoFormat> _formatManager;

        // Screen capture specific fields
        private int _sourceWidth;
        private int _sourceHeight;
        private int _targetWidth;
        private int _targetHeight;
        private readonly Rectangle _screenBounds;

        // Performance optimization fields
        private Bitmap _reusableBitmap;
        private Bitmap _resizedBitmap;
        private Graphics _reusableGraphics;
        private byte[] _reuseableBuffer;
        private int _lastBufferSize;
        private readonly SemaphoreSlim _captureSemaphore = new(1, 1);

        // Adaptive frame rate fields
        private long _lastCaptureTime;
        private int _frameSkipCount;
        private int _consecutiveSlowFrames;
        private double _adaptiveFps;
        private readonly object _fpsLock = new object();

        public bool IsClosed => _isClosed;
        public ScreenVideoSource(IVideoEncoder encoder = null, int sourceWidth = 0, int sourceHeight = 0, int targetWidth = DEFAULT_TARGET_WIDTH, int targetHeight = DEFAULT_TARGET_HEIGHT)
        {
            _videoEncoder = encoder;
            _formatManager = new MediaFormatManager<VideoFormat>(SupportedFormats);

            _screenBounds = Screen.PrimaryScreen.Bounds;
            _sourceWidth = sourceWidth > 0 ? sourceWidth : _screenBounds.Width;
            _sourceHeight = sourceHeight > 0 ? sourceHeight : _screenBounds.Height;
            _targetWidth = targetWidth;
            _targetHeight = targetHeight;

            _adaptiveFps = DEFAULT_FRAMES_PER_SECOND;
            _frameSpacing = 1000 / DEFAULT_FRAMES_PER_SECOND;

            _sendTimer = new System.Threading.Timer(CaptureFrame, null, Timeout.Infinite, Timeout.Infinite);

            InitializeBitmaps();
        }

        // Format management methods
        public void RestrictFormats(Func<VideoFormat, bool> filter) => _formatManager.RestrictFormats(filter);
        public List<VideoFormat> GetVideoSourceFormats() => _formatManager.GetSourceFormats();
        public void SetVideoSourceFormat(VideoFormat videoFormat) => _formatManager.SetSelectedFormat(videoFormat);

        public List<SDPAudioVideoMediaFormat> GetSDPVideoFormats()
        {
            var sdpFormats = new List<SDPAudioVideoMediaFormat>();

            foreach (var format in _formatManager.GetSourceFormats())
            {
                var sdpFormat = new SDPAudioVideoMediaFormat(
                    SDPMediaTypesEnum.video,
                    format.FormatID,
                    format.Codec.ToString(),
                    format.ClockRate
                );
                sdpFormats.Add(sdpFormat);
            }

            return sdpFormats;
        }

        // Video control methods
        public void ForceKeyFrame() => _videoEncoder?.ForceKeyFrame();
        public bool HasEncodedVideoSubscribers() => OnVideoSourceEncodedSample != null;
        public bool IsVideoSourcePaused() => _isPaused;

        public void SetFrameRate(int framesPerSecond)
        {
            if (framesPerSecond < MINIMUM_FRAMES_PER_SECOND || framesPerSecond > MAXIMUM_FRAMES_PER_SECOND)
            {
                logger.LogWarning("{FramesPerSecond} frames per second not in the allowed range of {MinimumFramesPerSecond} to {MaximumFramesPerSecond}, ignoring.",
                    framesPerSecond, MINIMUM_FRAMES_PER_SECOND, MAXIMUM_FRAMES_PER_SECOND);
                return;
            }

            lock (_fpsLock)
            {
                _adaptiveFps = framesPerSecond;
                _frameSpacing = 1000 / framesPerSecond;

                if (_isStarted && !_isPaused)
                {
                    _sendTimer.Change(0, _frameSpacing);
                }
            }
        }

        public Task StartVideo()
        {
            if (!_isStarted && !_isClosed)
            {
                _isStarted = true;
                _isPaused = false;
                _sendTimer.Change(0, _frameSpacing);
                logger.LogInformation("Screen video source started with {Width}x{Height} resolution at {Fps} FPS",
                    _targetWidth, _targetHeight, _adaptiveFps);
            }
            return Task.CompletedTask;
        }

        public Task PauseVideo()
        {
            _isPaused = true;
            _sendTimer.Change(Timeout.Infinite, Timeout.Infinite);
            logger.LogInformation("Screen video source paused");
            return Task.CompletedTask;
        }

        public Task ResumeVideo()
        {
            if (_isStarted && _isPaused && !_isClosed)
            {
                _isPaused = false;
                _sendTimer.Change(0, _frameSpacing);
                logger.LogInformation("Screen video source resumed");
            }
            return Task.CompletedTask;
        }

        public async Task CloseVideo()
        {
            if (!_isClosed)
            {
                _isClosed = true;
                _isStarted = false;
                _isPaused = true;

                // Dừng timer ngay lập tức
                _sendTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                // Đợi một chút để đảm bảo không còn callback nào chạy
                await Task.Delay(100);

                // Cleanup resource
                CleanupResources();
                logger.LogInformation("Screen video source closed");
            }
        }
        private void InitializeBitmaps()
        {
            lock (_lockObject)
            {
                // Dispose previous resources
                _reusableBitmap?.Dispose();
                _resizedBitmap?.Dispose();
                _reusableGraphics?.Dispose();

                // Create optimized bitmaps
                _reusableBitmap = new Bitmap(_sourceWidth, _sourceHeight, PixelFormat.Format24bppRgb);
                _resizedBitmap = new Bitmap(_targetWidth, _targetHeight, PixelFormat.Format24bppRgb);
                _reusableGraphics = Graphics.FromImage(_reusableBitmap);

                // Set high-performance graphics settings
                _reusableGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                _reusableGraphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                _reusableGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                _reusableGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
                _reusableGraphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
            }
        }

        private void CleanupResources()
        {
            lock (_lockObject)
            {
                var bitmap = _reusableBitmap;
                var resized = _resizedBitmap;
                var graphics = _reusableGraphics;
                var semaphore = _captureSemaphore;

                _reusableBitmap = null;
                _resizedBitmap = null;
                _reusableGraphics = null;
                _reuseableBuffer = null;

                bitmap?.Dispose();
                resized?.Dispose();
                graphics?.Dispose();
                semaphore?.Dispose();
            }
        }

        /// <summary>
        /// Adjusts frame rate dynamically based on system performance
        /// </summary>
        private void AdaptFrameRate()
        {
            lock (_fpsLock)
            {
                long currentTime = Environment.TickCount64;
                if (_lastCaptureTime > 0)
                {
                    long elapsedMs = currentTime - _lastCaptureTime;
                    double expectedMs = _frameSpacing;

                    if (elapsedMs > expectedMs * 1.5) // Frame took 50% longer than expected
                    {
                        _consecutiveSlowFrames++;
                        if (_consecutiveSlowFrames >= 3 && _adaptiveFps > MINIMUM_FRAMES_PER_SECOND)
                        {
                            _adaptiveFps = Math.Max(_adaptiveFps * 0.8, MINIMUM_FRAMES_PER_SECOND);
                            _frameSpacing = (int)(1000.0 / _adaptiveFps);
                            _consecutiveSlowFrames = 0;
                            logger.LogDebug("Reduced frame rate to {Fps} FPS due to performance", _adaptiveFps);
                        }
                    }
                    else if (elapsedMs < expectedMs * 0.8) // Frame completed 20% faster than expected
                    {
                        _consecutiveSlowFrames = Math.Max(_consecutiveSlowFrames - 1, 0);
                        if (_consecutiveSlowFrames == 0 && _adaptiveFps < DEFAULT_FRAMES_PER_SECOND)
                        {
                            _adaptiveFps = Math.Min(_adaptiveFps * 1.1, DEFAULT_FRAMES_PER_SECOND);
                            _frameSpacing = (int)(1000.0 / _adaptiveFps);
                            logger.LogDebug("Increased frame rate to {Fps} FPS", _adaptiveFps);
                        }
                    }
                }
                _lastCaptureTime = currentTime;
            }
        }

        private void CaptureFrame(object state)
        {
            if (!_isStarted || _isPaused || _isClosed) return;
            if (_reusableBitmap == null || _reusableGraphics == null) return;

            // Use semaphore to prevent concurrent captures
            if (!_captureSemaphore.Wait(0)) return;

            try
            {
                CaptureFrameOptimized();
            }
            catch (Exception ex)
            {
                OnVideoSourceError?.Invoke($"Screen capture error: {ex.Message}");
                logger.LogError(ex, "Error during screen capture");
            }
            finally
            {
                _captureSemaphore.Release();
            }
        }


        private uint GetDurationRtpTS()
        {
            uint fps = _frameSpacing > 0 ? (uint)(1000 / _frameSpacing) : DEFAULT_FRAMES_PER_SECOND;
            return VIDEO_SAMPLING_RATE / fps;
        }


        private void CaptureFrameOptimized()
        {
            if (!_isStarted || _isPaused || _isClosed) return;

            try
            {
                // Adaptive frame rate adjustment
                AdaptFrameRate();

                // Implement frame skipping under high load
                if (_consecutiveSlowFrames > 5)
                {
                    _frameSkipCount++;
                    if (_frameSkipCount % 2 == 0) // Skip every other frame when under high load
                    {
                        return;
                    }
                }
                else
                {
                    _frameSkipCount = 0;
                }

                lock (_lockObject)
                {
                    // Check if bitmaps need reinitialization
                    if (_reusableBitmap == null || _reusableBitmap.Width != _sourceWidth || _reusableBitmap.Height != _sourceHeight)
                    {
                        InitializeBitmaps();
                    }

                    // Fast screen capture using optimized method
                    CaptureScreenFast(_reusableBitmap);

                    // Resize if needed
                    Bitmap processedBitmap = _reusableBitmap;
                    if (_sourceWidth != _targetWidth || _sourceHeight != _targetHeight)
                    {
                        using (Graphics resizeGraphics = Graphics.FromImage(_resizedBitmap))
                        {
                            resizeGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                            resizeGraphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                            resizeGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                            resizeGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
                            resizeGraphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;

                            resizeGraphics.DrawImage(_reusableBitmap, 0, 0, _targetWidth, _targetHeight);
                        }
                        processedBitmap = _resizedBitmap;
                    }

                    _frameCount++;

                    // Send raw video sample if there are subscribers
                    if (OnVideoSourceRawSample != null)
                    {
                        byte[] buffer = BitmapToByteArrayOptimized(processedBitmap);
                        uint durationMs = (uint)_frameSpacing;
                        OnVideoSourceRawSample.Invoke(durationMs, processedBitmap.Width, processedBitmap.Height, buffer, VideoPixelFormatsEnum.Bgr);
                    }

                    // Encode and send encoded video sample if encoder is available and there are subscribers
                    if (_videoEncoder != null && OnVideoSourceEncodedSample != null && !_formatManager.SelectedFormat.IsEmpty())
                    {
                        byte[] i420Buffer = BitmapToI420Buffer(processedBitmap);
                        //Console.WriteLine("Encode: width={0}, height={1}, codec={2}, buffer={3}", processedBitmap.Width, processedBitmap.Height, _formatManager.SelectedFormat.Codec, i420Buffer?.Length);
                        if (i420Buffer != null)
                        {
                            var encodedBuffer = _videoEncoder.EncodeVideo(processedBitmap.Width, processedBitmap.Height, i420Buffer, VideoPixelFormatsEnum.I420, _formatManager.SelectedFormat.Codec);
                            if (encodedBuffer != null)
                            {
                                uint fps = _frameSpacing > 0 ? (uint)(1000 / _frameSpacing) : DEFAULT_FRAMES_PER_SECOND;
                                uint durationRtpTS = VIDEO_SAMPLING_RATE / fps;
                                OnVideoSourceEncodedSample.Invoke(durationRtpTS, encodedBuffer);
                            }
                        }
                    }

                    if (_frameCount == int.MaxValue)
                    {
                        _frameCount = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                OnVideoSourceError?.Invoke("Screen capture error: " + ex.Message);
                logger.LogError(ex, "Error during optimized screen capture");
            }
        }
        private void CaptureScreenFast(Bitmap bitmap)
        {
            // Method 1: Use Graphics.CopyFromScreen with optimized settings (current method)
            _reusableGraphics.CopyFromScreen(0, 0, 0, 0, new Size(_sourceWidth, _sourceHeight), CopyPixelOperation.SourceCopy);

            // Alternative Method 2: Win32 API for even faster capture (uncomment if needed)
            /*
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);
            IntPtr hBitmap = CreateCompatibleBitmap(screenDC, _sourceWidth, _sourceHeight);
            IntPtr oldBitmap = SelectObject(memDC, hBitmap);
            
            BitBlt(memDC, 0, 0, _sourceWidth, _sourceHeight, screenDC, 0, 0, SRCCOPY);
            
            Bitmap tempBitmap = Image.FromHbitmap(hBitmap);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(tempBitmap, 0, 0);
            }
            
            // Cleanup
            tempBitmap.Dispose();
            SelectObject(memDC, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDC);
            ReleaseDC(IntPtr.Zero, screenDC);
            */
        }

        private byte[] BitmapToByteArrayOptimized(Bitmap bitmap)
        {
            BitmapData bmpData = null;
            try
            {
                bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                int length = Math.Abs(bmpData.Stride) * bmpData.Height;

                // Reuse buffer if possible
                if (_reuseableBuffer == null || _reuseableBuffer.Length != length)
                {
                    _reuseableBuffer = new byte[length];
                    _lastBufferSize = length;
                }

                Marshal.Copy(bmpData.Scan0, _reuseableBuffer, 0, length);
                return _reuseableBuffer;
            }
            finally
            {
                if (bmpData != null)
                    bitmap.UnlockBits(bmpData);
            }
        }

        private byte[] BitmapToI420Buffer(Bitmap bitmap)
        {
            try
            {
                int width = bitmap.Width;
                int height = bitmap.Height;

                int yStride = width;
                int uvStride = width / 2;

                int ySize = yStride * height;
                int uvSize = uvStride * (height / 2);

                byte[] i420Buffer = new byte[ySize + 2 * uvSize]; // I420 layout: Y plane, then U plane, then V plane.

                BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                try
                {
                    unsafe
                    {
                        byte* ptr = (byte*)bmpData.Scan0.ToPointer();
                        int stride = bmpData.Stride;

                        int yIndex = 0;
                        int uIndex = ySize;         // U plane starts after Y plane
                        int vIndex = uIndex + uvSize; // V plane starts after U plane

                        for (int j = 0; j < height; j++)
                        {
                            byte* row = ptr + (j * stride);

                            for (int i = 0; i < width; i++)
                            {
                                // BGR format in bitmap
                                byte b = row[i * 3];
                                byte g = row[i * 3 + 1];
                                byte r = row[i * 3 + 2];

                                // Convert RGB to YUV (BT.601 standard)
                                byte y = (byte)((0.299 * r) + (0.587 * g) + (0.114 * b));
                                byte u = (byte)((-0.168736 * r) + (-0.331264 * g) + (0.5 * b) + 128);
                                byte v = (byte)((0.5 * r) + (-0.418688 * g) + (-0.081312 * b) + 128);

                                // Write Y plane
                                i420Buffer[yIndex++] = y;

                                // Subsample U & V (4:2:0 means one U & V sample for every 2x2 block)
                                if (j % 2 == 0 && i % 2 == 0)
                                {
                                    i420Buffer[uIndex++] = u;
                                    i420Buffer[vIndex++] = v;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bmpData);
                }

                return i420Buffer;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error converting bitmap to I420");
                return Array.Empty<byte>();
            }
        }        /// <summary>
                 /// Gets current performance statistics
                 /// </summary>
        public (double CurrentFPS, double TargetFPS, int SkippedFrames) GetPerformanceStats()
        {
            lock (_fpsLock)
            {
                return (_adaptiveFps, DEFAULT_FRAMES_PER_SECOND, _frameSkipCount);
            }
        }

        /// <summary>
        /// Manually adjust the target resolution
        /// </summary>
        public void SetTargetResolution(int width, int height)
        {
            lock (_lockObject)
            {
                _targetWidth = width;
                _targetHeight = height;
                InitializeBitmaps();
                logger.LogInformation("Target resolution changed to {Width}x{Height}", width, height);
            }
        }

        /// <summary>
        /// Get current capture dimensions
        /// </summary>
        public (int SourceWidth, int SourceHeight, int TargetWidth, int TargetHeight) GetDimensions()
        {
            return (_sourceWidth, _sourceHeight, _targetWidth, _targetHeight);
        }

        // Not implemented methods from IVideoSource interface
        public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
        {
            throw new NotImplementedException("The screen video source does not offer any encoding services for external sources.");
        }

        public void ExternalVideoSourceRawSampleFaster(uint durationMilliseconds, RawImage rawImage)
        {
            throw new NotImplementedException("The screen video source does not offer any encoding services for external sources.");
        }

        public void Dispose()
        {
            if (!_isClosed)
            {
                CloseVideo().Wait(TIMER_DISPOSE_WAIT_MILLISECONDS);
            }

            _sendTimer?.Dispose();
            _videoEncoder?.Dispose();
        }
    }
}