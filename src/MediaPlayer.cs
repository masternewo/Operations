using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace Operations.Client
{
    /// <summary>
    /// Plays briefing voice messages and videos from files in the local cache (the file is downloaded once, in chunks, and verified by hash).
    /// Voice goes through one AudioSource; video renders into a RenderTexture that the dossier shows. Everything is guarded: if the
    /// game build has no video module or cannot decode the file, the player gets a clear message and the screen keeps working.
    /// </summary>
    internal sealed class MediaPlayer : MonoBehaviour
    {
        private AudioSource _audio;
        private object _video;                 // VideoHost (kept as object so a missing video module cannot break loading of this class)
        public string AudioKey, VideoKey;      // what is playing: "<missionId>:<key>"
        public string CurrentPath;             // never pruned while playing
        public RenderTexture VideoTexture { get; private set; }
        public Action<string> OnMessage = delegate { };

        public bool IsAudioPlaying(string key) { return AudioKey == key && _audio != null && _audio.isPlaying; }
        public bool IsVideoPlaying(string key) { return VideoKey == key; }

        private AudioSource Audio()
        {
            if (_audio == null) { _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false; _audio.spatialBlend = 0f; _audio.volume = 0.9f; _audio.ignoreListenerPause = true; }
            return _audio;
        }

        public void StopAll()
        {
            if (_audio != null) _audio.Stop();
            AudioKey = null;
            StopVideo();
            CurrentPath = null;
        }

        public void PlayAudio(string key, string path)
        {
            StopAll(); AudioKey = key; CurrentPath = path;
            StartCoroutine(LoadAudio(path));
        }

        private IEnumerator LoadAudio(string path)
        {
            AudioType type = path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS : path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? AudioType.WAV : AudioType.MPEG;
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
            {
                yield return req.SendWebRequest();
                if (!string.IsNullOrEmpty(req.error)) { OnMessage("Voice message cannot be played: " + req.error); AudioKey = null; yield break; }
                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null || AudioKey == null) { AudioKey = null; yield break; }
                AudioSource a = Audio(); a.clip = clip; a.Play();
            }
        }

        public void PlayVideo(string key, string path)
        {
            StopAll(); VideoKey = key; CurrentPath = path;
            try { StartVideo(path); }
            catch (Exception e) { OperationsPlugin.Log.LogWarning("Video playback unavailable: " + e.Message); OnMessage("This game build cannot play the video."); VideoKey = null; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void StartVideo(string path)
        {
            var go = new GameObject("Operations.Video"); go.transform.SetParent(transform, false);
            var vp = go.AddComponent<VideoPlayer>();
            VideoTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32); VideoTexture.Create();
            vp.playOnAwake = false; vp.isLooping = false; vp.source = VideoSource.Url; vp.url = new Uri(path).AbsoluteUri;
            vp.renderMode = VideoRenderMode.RenderTexture; vp.targetTexture = VideoTexture;
            vp.audioOutputMode = VideoAudioOutputMode.AudioSource; vp.EnableAudioTrack(0, true); vp.SetTargetAudioSource(0, Audio());
            vp.prepareCompleted += p => { if (VideoKey != null) p.Play(); };
            vp.errorReceived += (p, msg) => { OperationsPlugin.Log.LogWarning("Video error: " + msg); OnMessage("The video cannot be played (" + msg + ")."); VideoKey = null; };
            vp.loopPointReached += p => { VideoKey = null; };
            vp.Prepare();
            _video = go;
        }

        private void StopVideo()
        {
            VideoKey = null;
            var go = _video as GameObject; _video = null;
            if (go != null) { try { Destroy(go); } catch (Exception) { /* already gone */ } }
            if (VideoTexture != null) { VideoTexture.Release(); Destroy(VideoTexture); VideoTexture = null; }
        }

        private void OnDestroy() { StopAll(); }
    }
}
