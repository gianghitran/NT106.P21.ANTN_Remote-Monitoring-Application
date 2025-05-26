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
using Agora.Rtc;

namespace RemoteMonitoringApplication.Services
{
    class ShareScreenService
    {
        private WebSocketClient _client = SessionManager.Instance.WebSocketClient;
        private RTCPeerConnection _peerConnection;
        private List<RTCIceCandidateInit> _pendingIceCandidates = new List<RTCIceCandidateInit>();
        private bool _remoteDescriptionSet = false;
        public async Task StartScreenSharingAsync(string targetId)
        {
            var config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer>
                    {
                        new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
                    }
            };

            _peerConnection = new RTCPeerConnection();

            //Dùng để xác định kết nối tốt nhất giữa hai client
            _peerConnection.onicecandidate += async (candidate) =>
            {
                if (candidate != null)
                {
                    var iceCandidate = new RTCIceCandidateInit
                    {
                        candidate = candidate.candidate,
                        sdpMid = candidate.sdpMid ?? candidate.sdpMLineIndex.ToString(),
                        sdpMLineIndex = candidate.sdpMLineIndex,
                        usernameFragment = candidate.usernameFragment
                    };

                    var iceCandidateRequest = new
                    {
                        command = "ice_candidate",
                        targetId = targetId,
                        iceCandidate = iceCandidate
                    };

                    var jsonCandidate = iceCandidateRequest.ToJson();
                    await _client.SendMessageAsync(jsonCandidate);
                }
            };

            var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var screenShare = new ScreenVideoSource(screen.Width, screen.Height);
            var videoEndPoint = new SIPSorceryMedia.FFmpeg.FFmpegVideoEndPoint();

            // Convert VideoFormat to SDPAudioVideoMediaFormat
            //var videoFormat = new SDPAudioVideoMediaFormat((SDPWellKnownMediaFormatsEnum)VideoCodecsEnum.VP8); // Or H264
            var videoTrack = new MediaStreamTrack(videoEndPoint.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
            _peerConnection.addTrack(videoTrack);

            await _peerConnection.Start();

            screenShare.OnVideoSourceRawSample += (uint duration, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat) =>
            {
                Console.WriteLine($"📸 Frame: {width}x{height} - {sample.Length} bytes");
                videoEndPoint.ExternalVideoSourceRawSample(duration, width, height, sample, pixelFormat);
            };

            await videoEndPoint.StartVideo();
            await screenShare.StartVideo();

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

            var jsonRequest = JsonSerializer.Serialize(request);
            await _client.SendMessageAsync(jsonRequest);
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

                _peerConnection = new RTCPeerConnection();
                var videoEndPoint = new SIPSorceryMedia.FFmpeg.FFmpegVideoEndPoint();

                var videoTrack = new MediaStreamTrack(videoEndPoint.GetVideoSinkFormats(), MediaStreamStatusEnum.RecvOnly);
                _peerConnection.addTrack(videoTrack);

                await videoEndPoint.StartVideo();


                //Dùng để xác định kết nối tốt nhất giữa hai client
                _peerConnection.onicecandidate += async (candidate) =>
                {
                    if (candidate != null)
                    {
                        var iceCandidate = new RTCIceCandidateInit
                        {
                            candidate = candidate.candidate,
                            sdpMid = candidate.sdpMid ?? candidate.sdpMLineIndex.ToString(),
                            sdpMLineIndex = candidate.sdpMLineIndex,
                            usernameFragment = candidate.usernameFragment
                        };

                        var iceCandidateRequest = new
                        {
                            command = "ice_candidate",
                            targetId = targetId,
                            iceCandidate = iceCandidate
                        };

                        var jsonCandidate = iceCandidateRequest.ToJson();
                        await _client.SendMessageAsync(jsonCandidate);
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

                var jsonAnswerRequest = JsonSerializer.Serialize(answerRequest);
                await _client.SendMessageAsync(jsonAnswerRequest);
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


            }
            catch (ArgumentNullException ex)
            {
                Console.WriteLine($"Error handling incoming answer: {ex.Message}");
                return;
            }
        }

        public async Task HandleIncomingIceCandidate(string message)
        {
            try
            {
                if (message == null)
                {
                    throw new ArgumentNullException(nameof(message), "Incoming ICE candidate message cannot be null.");
                }

                var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                // 1. Add ICE candidate to peer connection
                RTCIceCandidateInit data = root.GetProperty("iceCandidate").Deserialize<RTCIceCandidateInit>();

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
            catch (ArgumentNullException ex)
            {
                Console.WriteLine($"Error handling incoming ICE candidate: {ex.Message}");
                return;
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
            await _client.SendMessageAsync(jsonRequest);
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
