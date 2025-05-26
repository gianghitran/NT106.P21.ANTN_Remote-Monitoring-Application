using System;
using System.Drawing;
using System.Windows.Forms;
using System.Timers;
using SIPSorceryMedia.Abstractions;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteMonitoringApplication.Services
{    public class ScreenVideoSource : IVideoSource
    {
        public event RawVideoSampleDelegate OnVideoSourceRawSample;
        public event EncodedSampleDelegate OnVideoSourceEncodedSample;
        public event RawVideoSampleFasterDelegate OnVideoSourceRawSampleFaster;
        public event SourceErrorDelegate OnVideoSourceError;

        private readonly object _lockObject = new object();
        private System.Timers.Timer _timer;
        private int _width;
        private int _height;
        private double _fps;
        private bool _isStarted;
        private Bitmap _reusableBitmap;
        private Bitmap _resizedBitmap;
        private Graphics _reusableGraphics;
        private IntPtr _hdc;
        private readonly Rectangle _screenBounds;        private readonly int _targetWidth = 1280;
        private readonly int _targetHeight = 720;
        private volatile bool _isCapturing;
        
        // Performance optimizations
        private byte[] _reuseableBuffer;
        private int _lastBufferSize;
        private readonly SemaphoreSlim _captureSemaphore = new(1, 1);
        
        // Frame rate adaptation
        private long _lastCaptureTime;
        private int _frameSkipCount;
        private int _consecutiveSlowFrames;
        private double _adaptiveFps;
        private readonly object _fpsLock = new object();

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

        private const int SRCCOPY = 0x00CC0020;        public ScreenVideoSource(int width, int height, double fps = 30)
        {
            _width = width;
            _height = height;
            _fps = Math.Min(fps, 60); // Cap at 60 FPS for performance
            _adaptiveFps = _fps;
            _screenBounds = Screen.PrimaryScreen.Bounds;
            
            // Initialize timer with higher precision
            _timer = new System.Timers.Timer(1000.0 / _fps);
            _timer.Elapsed += async (sender, e) => await CaptureFrameAsync();
            
            // Pre-allocate bitmaps for better performance
            InitializeBitmaps();
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
                    double expectedMs = 1000.0 / _adaptiveFps;
                    
                    if (elapsedMs > expectedMs * 1.5) // Frame took 50% longer than expected
                    {
                        _consecutiveSlowFrames++;
                        if (_consecutiveSlowFrames >= 3 && _adaptiveFps > 10)
                        {
                            _adaptiveFps = Math.Max(_adaptiveFps * 0.8, 10); // Reduce FPS by 20%, minimum 10 FPS
                            _timer.Interval = 1000.0 / _adaptiveFps;
                            _consecutiveSlowFrames = 0;
                        }
                    }
                    else if (elapsedMs < expectedMs * 0.8) // Frame completed 20% faster than expected
                    {
                        _consecutiveSlowFrames = Math.Max(_consecutiveSlowFrames - 1, 0);
                        if (_consecutiveSlowFrames == 0 && _adaptiveFps < _fps)
                        {
                            _adaptiveFps = Math.Min(_adaptiveFps * 1.1, _fps); // Increase FPS by 10%, maximum original FPS
                            _timer.Interval = 1000.0 / _adaptiveFps;
                        }
                    }
                }
                _lastCaptureTime = currentTime;
            }
        }private void InitializeBitmaps()
        {
            lock (_lockObject)
            {
                // Dispose previous resources
                _reusableBitmap?.Dispose();
                _resizedBitmap?.Dispose();
                _reusableGraphics?.Dispose();

                // Create optimized bitmaps
                _reusableBitmap = new Bitmap(_width, _height, PixelFormat.Format24bppRgb);
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

        public bool IsVideoSourcePaused() => !_isStarted;

        public Task PauseVideo() 
        { 
            _isStarted = false; 
            _timer.Stop(); 
            return Task.CompletedTask; 
        }

        public Task ResumeVideo() 
        { 
            _isStarted = true; 
            _timer.Start(); 
            return Task.CompletedTask; 
        }

        public Task StartVideo()
        {
            _isStarted = true;
            _timer.Start();
            return Task.CompletedTask;
        }

        public Task CloseVideo() 
        { 
            _isStarted = false; 
            _timer.Stop(); 
            CleanupResources();
            return Task.CompletedTask; 
        }

        private void CleanupResources()
        {
            lock (_lockObject)
            {
                _timer?.Dispose();
                _reusableBitmap?.Dispose();
                _resizedBitmap?.Dispose();
                _reusableGraphics?.Dispose();
                _captureSemaphore?.Dispose();
            }
        }

        private async Task CaptureFrameAsync()
        {
            if (!_isStarted || _isCapturing) return;

            // Use semaphore to prevent concurrent captures
            if (!await _captureSemaphore.WaitAsync(0)) return;

            try
            {
                _isCapturing = true;
                await Task.Run(CaptureFrameOptimized);
            }
            finally
            {
                _isCapturing = false;
                _captureSemaphore.Release();
            }
        }        private void CaptureFrameOptimized()
        {
            if (!_isStarted) return;

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
                    if (_reusableBitmap == null || _reusableBitmap.Width != _width || _reusableBitmap.Height != _height)
                    {
                        InitializeBitmaps();
                    }

                    // Fast screen capture using optimized method
                    CaptureScreenFast(_reusableBitmap);

                    // Resize if needed
                    Bitmap processedBitmap = _reusableBitmap;
                    if (_width != _targetWidth || _height != _targetHeight)
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

                    // Convert to byte array with reusable buffer
                    byte[] buffer = BitmapToByteArrayOptimized(processedBitmap);
                    uint durationMs = (uint)(1000.0 / _adaptiveFps);
                    
                    OnVideoSourceRawSample?.Invoke(durationMs, processedBitmap.Width, processedBitmap.Height, buffer, VideoPixelFormatsEnum.Bgr);
                }
            }
            catch (Exception ex)
            {
                OnVideoSourceError?.Invoke("Screen capture error: " + ex.Message);
            }
        }private void CaptureScreenFast(Bitmap bitmap)
        {
            // Method 1: Use Graphics.CopyFromScreen with optimized settings (current method)
            _reusableGraphics.CopyFromScreen(0, 0, 0, 0, new Size(_width, _height), CopyPixelOperation.SourceCopy);
            
            // Alternative Method 2: Win32 API for even faster capture (uncomment if needed)
            /*
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);
            IntPtr hBitmap = CreateCompatibleBitmap(screenDC, _width, _height);
            IntPtr oldBitmap = SelectObject(memDC, hBitmap);
            
            BitBlt(memDC, 0, 0, _width, _height, screenDC, 0, 0, SRCCOPY);
            
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
                
                Marshal.Copy(bmpData.Scan0, _reuseableBuffer, 0, length);                return _reuseableBuffer;
            }
            finally
            {
                if (bmpData != null)
                    bitmap.UnlockBits(bmpData);
            }
        }

        /// <summary>
        /// Gets current performance statistics
        /// </summary>
        public (double CurrentFPS, double TargetFPS, int SkippedFrames) GetPerformanceStats()
        {
            lock (_fpsLock)
            {
                return (_adaptiveFps, _fps, _frameSkipCount);
            }
        }

        /// <summary>
        /// Manually adjust the target FPS
        /// </summary>
        public void SetTargetFPS(double fps)
        {
            lock (_fpsLock)
            {
                _fps = Math.Min(Math.Max(fps, 5), 60); // Clamp between 5-60 FPS
                _adaptiveFps = _fps;
                _timer.Interval = 1000.0 / _fps;
                _consecutiveSlowFrames = 0;
                _frameSkipCount = 0;
            }
        }

        public List<VideoFormat> GetVideoSourceFormats()
        {
            throw new NotImplementedException();
        }

        public void SetVideoSourceFormat(VideoFormat videoFormat)
        {
            throw new NotImplementedException();
        }

        public void RestrictFormats(Func<VideoFormat, bool> filter)
        {
            throw new NotImplementedException();
        }

        public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
        {
            throw new NotImplementedException();
        }

        public void ExternalVideoSourceRawSampleFaster(uint durationMilliseconds, RawImage rawImage)
        {
            throw new NotImplementedException();
        }

        public void ForceKeyFrame()
        {
            throw new NotImplementedException();
        }

        public bool HasEncodedVideoSubscribers()
        {
            throw new NotImplementedException();
        }
    }
}