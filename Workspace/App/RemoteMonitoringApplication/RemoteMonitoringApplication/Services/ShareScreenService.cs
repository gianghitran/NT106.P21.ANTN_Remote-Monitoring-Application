using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Text.Json;
using System.Linq.Expressions;
using SIPSorcery.SIP.App;
using TinyJson;
using SIPSorceryMedia.FFmpeg;
using System.IO;
using System.Net;
using System.Windows.Media.Imaging;

namespace RemoteMonitoringApplication.Services
{
    class ShareScreenService
    {
        private CClient _client = SessionManager.Instance.tcpClient;
        private RTCPeerConnection _peerConnection;
        private List<RTCIceCandidateInit> _pendingIceCandidates = new List<RTCIceCandidateInit>();
        private bool _remoteDescriptionSet = false;
        private ScreenVideoSource _screenShare;
        private bool _isSharing = false;
        private IVideoEncoder _videoDecoder = new FFmpegVideoEncoder();

        // Thêm event để notify khi nhận frame
        public event Action<byte[], uint, string, int, int> OnFrameReceived;
        public event Action<Bitmap> OnDecodedFrameReceived;

        public async Task StartScreenSharingAsync(string targetId)
        {
            var config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer>
                    {
                        new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
                    }
            };

            _peerConnection = new RTCPeerConnection(config);

            //Dùng để xác định kết nối tốt nhất giữa hai client
            _peerConnection.onicecandidate += async (candidate) =>
            {
                if (candidate != null)
                {
                    var iceCandidate = new RTCIceCandidateInit
                    {
                        candidate = candidate.candidate,
                        sdpMid = candidate.sdpMid,
                        sdpMLineIndex = candidate.sdpMLineIndex,
                        usernameFragment = candidate.usernameFragment
                    };

                    await _client.SendMessageAsync(
                        SessionManager.Instance.ClientId,
                        targetId,
                        new
                        {
                            command = "ice_candidate",
                            targetId = targetId,
                            iceCandidate = iceCandidate
                        }
                    );
                }
            };

            var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;

            IVideoEncoder encoder = new FFmpegVideoEncoder();

            _screenShare = new ScreenVideoSource(encoder, screen.Width, screen.Height);
            _screenShare.SetVideoSourceFormat(ScreenVideoSource.SupportedFormats
                                            .First(f => f.Codec == VideoCodecsEnum.H264));

            var videoTrack = new MediaStreamTrack(_screenShare.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
            _peerConnection.addTrack(videoTrack);

            await _peerConnection.Start();

            // Subscribe to video frame events for host
            _screenShare.OnVideoSourceRawSample += (uint duration, int width, int height, byte[] rawSample, VideoPixelFormatsEnum type) =>
            {
                OnFrameReceived?.Invoke(rawSample, duration, type.ToString(), width, height);
            };

            // Send encoded video frames to peer connection
            _screenShare.OnVideoSourceEncodedSample += (uint duration, byte[] encodedSample) =>
            {
                //Console.WriteLine($"📦 Encoded frame: {encodedSample.Length} bytes");
                try
                {
                    if (_peerConnection != null)
                    {
                        _peerConnection.SendVideo(duration, encodedSample);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error sending encoded frame: " + ex.ToString());
                }
            };


            // Create offer and set it as the local description
            var offer = _peerConnection.createOffer();
            await _peerConnection.setLocalDescription(offer);

            // Send the offer to the server
            var request = new ShareScreenRequest
            {
                command = "start_share",
                targetId = targetId,
                sdp = offer.sdp,
                sdpType = "offer"
            };

            await _client.SendMessageAsync(
                SessionManager.Instance.ClientId,
                targetId,
                request
            );
        }

        public async Task HandleIncomingOffer(string sdp, string targetId)
        {
            try
            {
                if (sdp == null)
                {
                    throw new ArgumentNullException(nameof(sdp), "Incoming offer message cannot be null.");
                }

                var config = new RTCConfiguration
                {
                    iceServers = new List<RTCIceServer>
                    {
                        new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
                    }
                };

                _peerConnection = new RTCPeerConnection(config);

                var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                IVideoEncoder encoder = new FFmpegVideoEncoder();

                _peerConnection.onconnectionstatechange += async (RTCPeerConnectionState newState) =>
                {
                    if (newState == RTCPeerConnectionState.closed)
                    {
                        await StopScreenSharingAsync();
                    }
                };

                var screenShare = new ScreenVideoSource(encoder, screen.Width, screen.Height);
                var sdpVideoFormats = screenShare.GetSDPVideoFormats();

                // Khởi tạo MediaStreamTrack đầy đủ tham số
                var videoTrack = new MediaStreamTrack(screenShare.GetVideoSourceFormats(), MediaStreamStatusEnum.RecvOnly);

                _peerConnection.addTrack(videoTrack);

                _peerConnection.OnVideoFrameReceived += (IPEndPoint ep, uint timestamp, byte[] sample, VideoFormat type) =>
                {
                    DecodeH264Frame(sample, timestamp);
                };


                //Dùng để xác định kết nối tốt nhất giữa hai client
                _peerConnection.onicecandidate += async (candidate) =>
                {
                    if (candidate != null)
                    {
                        var iceCandidate = new RTCIceCandidateInit
                        {
                            candidate = candidate.candidate,
                            sdpMid = candidate.sdpMid,
                            sdpMLineIndex = candidate.sdpMLineIndex,
                            usernameFragment = candidate.usernameFragment
                        };

                        await _client.SendMessageAsync(
                            SessionManager.Instance.ClientId,
                            targetId,
                            new
                            {
                                command = "ice_candidate",
                                targetId = targetId,
                                iceCandidate = iceCandidate
                            }
                        );
                    }
                };

                // 1. Đặt SDP offer từ client A
                var offerSdp = SDP.ParseSDPDescription(sdp);
                _peerConnection.SetRemoteDescription(SdpType.offer, offerSdp);

                _remoteDescriptionSet = true;

                // Thêm các ICE đã lưu trước đó
                foreach (var pending in _pendingIceCandidates)
                {
                    _peerConnection.addIceCandidate(pending);
                    Console.WriteLine("✅ Đã add ICE đã lưu trước đó.");
                }
                _pendingIceCandidates.Clear();


                _peerConnection.onicegatheringstatechange += (state) =>
                {
                    Console.WriteLine("ICE state: " + state);
                    if (state == RTCIceGatheringState.complete)
                    {
                        Console.WriteLine("✅ ICE gathering complete.");
                    }
                };


                // 2. Tạo SDP answer
                var answer = _peerConnection.createAnswer();
                await _peerConnection.setLocalDescription(answer);

                // 3. Gửi answer về server
                var answerRequest = new ShareScreenRequest
                {
                    command = "start_share",
                    targetId = targetId,
                    sdp = answer.sdp,
                    sdpType = "answer"
                };

                await _client.SendMessageAsync(
                        SessionManager.Instance.ClientId,
                        targetId,
                        answerRequest
                    );
            }
            catch (ArgumentNullException ex)
            {
                Console.WriteLine($"Error handling incoming offer: {ex.Message}");
                return;
            }
        }

        public async Task HandleIncomingAnswer(string sdp, string targetId)
        {
            try
            {
                if (sdp == null)
                {
                    throw new ArgumentNullException(nameof(sdp), "Incoming answer message cannot be null.");
                }

                // 1. Đặt SDP answer từ client 
                var answerSdp = SDP.ParseSDPDescription(sdp);
                _peerConnection.SetRemoteDescription(SdpType.answer, answerSdp);

                _remoteDescriptionSet = true;

                // Thêm các ICE đã lưu trước đó
                foreach (var pending in _pendingIceCandidates)
                {
                    _peerConnection.addIceCandidate(pending);
                    Console.WriteLine("✅ Đã add ICE đã lưu trước đó.");
                }
                _pendingIceCandidates.Clear();
                await _screenShare.StartVideo();

            }
            catch (ArgumentNullException ex)
            {
                Console.WriteLine($"Error handling incoming answer: {ex.Message}");
                return;
            }
        }

        public async Task HandleIncomingIceCandidate(string iceCandidateJson)
        {
            try
            {
                RTCIceCandidateInit data = JsonSerializer.Deserialize<RTCIceCandidateInit>(iceCandidateJson);

                if (data.candidate == null || data.sdpMid == null)
                {
                    throw new ArgumentNullException("ICE candidate or SDP mid cannot be null.");
                }

                if (!_remoteDescriptionSet)
                {
                    Console.WriteLine("🔄 Remote chưa set, lưu ICE lại.");
                    _pendingIceCandidates.Add(data);
                }
                else
                {
                    _peerConnection.addIceCandidate(data);
                    Console.WriteLine("✅ ICE candidate added.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling incoming ICE candidate: {ex.Message}");
            }
        }

        public async Task StopScreenSharingAsync()
        {
            if (_peerConnection != null)
            {
                _peerConnection.close();
                _peerConnection = null;
            }

            var request = new BaseRequest { command = "stop_share" };
            var jsonRequest = System.Text.Json.JsonSerializer.Serialize(request);
            if (_screenShare != null || _isSharing)
            {
                await _screenShare.CloseVideo();
            }
            //await _client.SendMessageAsync(jsonRequest);
        }

        private void DecodeH264Frame(byte[] frameData, uint timestamp)
        {
            try
            {
                // Decode H264 frame
                if (_videoDecoder == null)
                {
                    _videoDecoder = new FFmpegVideoEncoder();
                }

                var decodedFrame = _videoDecoder.DecodeVideo(frameData.ToArray(), VideoPixelFormatsEnum.Bgr, VideoCodecsEnum.H264);
                if (decodedFrame != null)
                {
                    foreach (var frame in decodedFrame)
                    {
                        OnFrameReceived?.Invoke(frame.Sample, timestamp, "H264", (int)frame.Width, (int)frame.Height);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error decoding H264 frame: {ex.Message}");
            }
        }

        public Bitmap ConvertToBitmap(byte[] data, string codec, int width, int height)
        {
            try
            {
                VideoCodecsEnum codecEnum = codec == "VP8" ? VideoCodecsEnum.VP8 : VideoCodecsEnum.H264;

                if (data != null)
                {
                    // Tạo Bitmap từ buffer BGR
                    var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
                    System.Runtime.InteropServices.Marshal.Copy(data, 0, bmpData.Scan0, data.Length);
                    bmp.UnlockBits(bmpData);
                    return bmp;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Decode error: " + ex.Message);
            }
            return null;
        }

        public ImageSource BitmapToImageSource(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        public class BaseRequest
        {
            public string command { get; set; }
        }

        public class ShareScreenRequest : BaseRequest
        {
            public string targetId { get; set; }     // ClientId muốn mở peer-to-peer
            public string sdp { get; set; }          // Nội dung SDP offer
            public string sdpType { get; set; }      // "offer" hoặc "answer"
        }

        public class ShareScreenResponse : BaseRequest
        {
            public string sdp { get; set; }          // Nội dung SDP answer
            public string sdpType { get; set; }      // "offer" hoặc "answer"
        }

        public class IceCandidateRequest : BaseRequest
        {
            public string targetId { get; set; }     // ClientId muốn gửi ICE candidate
            public string candidate { get; set; }    // Nội dung ICE candidate
            public string sdpMid { get; set; }       // SDP mid của track
            public ushort sdpMLineIndex { get; set; }   // SDP m-line index của track

            public string usernameFragment { get; set; }
        }
    }
}
