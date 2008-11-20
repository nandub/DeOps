﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

using RiseOp.Implementation;
using RiseOp.Implementation.Dht;
using RiseOp.Implementation.Protocol;
using RiseOp.Implementation.Transport;


namespace RiseOp.Services.Voice
{
    internal delegate void VolumeUpdateHandler(int inMax, int outMax);


    class VoiceService : OpService
    {
        public string Name { get { return "Voice"; } }
        public uint ServiceID { get { return (uint)ServiceIDs.Voice; } }

        internal OpCore Core;
        internal DhtNetwork Network;

        internal Dictionary<int, VolumeUpdateHandler> VolumeUpdate = new Dictionary<int, VolumeUpdateHandler>(); // gui event

        internal Dictionary<ulong, RemoteVoice> RemoteVoices = new Dictionary<ulong, RemoteVoice>();
        internal ThreadedList<PlayAudio> Players = new ThreadedList<PlayAudio>();

        internal Dictionary<ulong, List<int>> SpeakingTo = new Dictionary<ulong, List<int>>(); // user, window

        internal RecordAudio Recorder;

        internal int RecordingDevice = -1;
        internal int PlaybackDevice = -1;

        int UpdateTimeout = 1000 / 4; // 200ms, 5/second
        Stopwatch LastUpdate = new Stopwatch();
        internal Dictionary<int, Tuple<int, int>> MaxVolume = new Dictionary<int, Tuple<int, int>>(); // window, volume<in,out>
        

        internal VoiceService(OpCore core)
        {
            Core = core;
            Network = core.Network;

            Core.SecondTimerEvent += new TimerHandler(Core_SecondTimer);
            Network.RudpControl.SessionData[ServiceID, 0] += new SessionDataHandler(Session_Data);

            LastUpdate.Start();
        }

        public void Dispose()
        {
            Core.SecondTimerEvent -= new TimerHandler(Core_SecondTimer);
            Network.RudpControl.SessionData[ServiceID, 0] -= new SessionDataHandler(Session_Data);

            ResetDevices();

            SpeakingTo.Clear();
            RemoteVoices.Clear();
            VolumeUpdate.Clear();
        }

        internal void ResetDevices()
        {
            // kill thread
            if (AudioThread != null)
            {
                ThreadRunning = false;
                AudioEvent.Set();
                AudioThread.Join(2000);
                AudioThread = null;
            }

            // deconstruct all record/play streams
            if (Recorder != null)
            {
                Recorder.Dispose();
                Recorder = null;
            }

            Players.ForEach(p => p.Dispose());
            Players.SafeClear();
            
            // will auto be recreated
        }

        public void GetMenuInfo(InterfaceMenuType menuType, List<MenuItemInfo> menus, ulong user, uint project)
        {
            return;
        }

        public void SimTest()
        {
            return;
        }

        public void SimCleanup()
        {
            return;
        }

        void Core_SecondTimer()
        {
            UpdateVolume();

            if (SpeakingTo.Count == 0)
                RecordingActive = false;

            // hearing audio does not time out, we keep it so the history can always be had
        }

        Thread AudioThread;
        bool ThreadRunning;
        internal AutoResetEvent AudioEvent = new AutoResetEvent(false);
        bool RecordingActive;

        void StartAudioThread(bool record)
        {
            if (record)
            {
                if (Recorder == null)
                {
                    RecordAudio tmp = new RecordAudio(this);
                    Recorder = tmp; // use temp so audio thread doesn't use before ready
                }

                RecordingActive = true;
                AudioEvent.Set();
            }

            if (AudioThread != null && AudioThread.IsAlive)
                return;

            AudioThread = new Thread(new ThreadStart(RunAudioThread));
            AudioThread.Name = "Voice Thread";
            ThreadRunning = true;
            AudioThread.Start();
        }

        void RunAudioThread()
        {
            while (ThreadRunning)
            {
                AudioEvent.WaitOne();

                if (Recorder != null && RecordingActive)
                    lock(Recorder)
                        Recorder.ProcessBuffers();

                Players.LockReading(() => Players.ForEach(p => p.ProcessBuffers()));
            }
        }

        internal void RegisterWindow(int window, VolumeUpdateHandler volumeEvent)
        {
            if (Core.InvokeRequired)
            {
                Core.RunInCoreAsync(() => RegisterWindow(window, volumeEvent));
                return;
            }

            MaxVolume[window] = new Tuple<int, int>(0, 0);

            VolumeUpdate[window] = volumeEvent;
        }

        internal void ResetWindow(int window)
        {
            if (Core.InvokeRequired)
            {
                Core.RunInCoreAsync(() => ResetWindow(window));
                return;
            }

            // remove all entries in in/out for window id
            Mute(window);

            foreach (RemoteVoice remote in RemoteVoices.Values)
                if (remote.ListeningTo.ContainsKey(window))
                    remote.ListeningTo.Remove(window);
        }

        internal void UnregisterWindow(int window)
        {
            if (Core.InvokeRequired)
            {
                Core.RunInCoreAsync(() => UnregisterWindow(window));
                return;
            }

            ResetWindow(window);

            if (MaxVolume.ContainsKey(window))
                MaxVolume.Remove(window);

            if (VolumeUpdate.ContainsKey(window))
                VolumeUpdate.Remove(window);
        }

        internal void ListenTo(int window, ulong user, AudioDirection direction)
        {
            if (Core.InvokeRequired)
            {
                Core.RunInCoreAsync(() => ListenTo(window, user, direction));
                return;
            }

            // incoming voice from user will be outputted to speaker, and window notified

            if (!RemoteVoices.ContainsKey(user))
                RemoteVoices[user] = new RemoteVoice();

            RemoteVoice remote = RemoteVoices[user];

            remote.ListeningTo[window] = direction;
        }

        internal void SpeakTo(int window, ulong user)
        {
            if (Core.InvokeRequired)
            {
                Core.RunInCoreAsync(() => SpeakTo(window, user));
                return;
            }

            // voice into microphone will be recorded and sent to this user
            if (!SpeakingTo.ContainsKey(user))
                SpeakingTo[user] = new List<int>();

            if(!SpeakingTo[user].Contains(window))
                SpeakingTo[user].Add(window);


            StartAudioThread(true);
        }

        internal void Mute(int window)
        {
            if (Core.InvokeRequired)
            {
                Core.RunInCoreAsync(() => Mute(window));
                return;
            }

            // stops sending microphone input to window (mute)
            
            foreach (ulong user in SpeakingTo.Keys)
                if (SpeakingTo[user].Contains(window))
                    SpeakingTo[user].Remove(window);

            foreach (ulong user in SpeakingTo.Keys.Where(user => SpeakingTo[user].Count == 0).ToArray())
                SpeakingTo.Remove(user);
        }
        
        internal void Recorder_AudioData(byte[] audio, int maxVolume, int frameSize)
        {
            // IM/Chat services keep these connections alive

            // send audio data, rudp to all users in hearing, tag with audio quality info

            AudioPacket packet = new AudioPacket() { Audio = audio, FrameSize = frameSize };

            int[] windows = new int[0];

            // for each user in the speaking
            foreach (ulong user in SpeakingTo.Keys)
            {
                foreach (int window in SpeakingTo[user])
                    if (MaxVolume.ContainsKey(window) && maxVolume > MaxVolume[window].Param2)
                        MaxVolume[window].Param2 = maxVolume;

                foreach (RudpSession session in Network.RudpControl.GetActiveSessions(user))
                    session.SendData(ServiceID, 0, packet, true);
            }

            UpdateVolume();
        }

        private void UpdateVolume()
        {
            if (LastUpdate.ElapsedMilliseconds > UpdateTimeout)
            {
                // thread safe get volume of incoming audio
                foreach (RemoteVoice remote in RemoteVoices.Values)
                {
                    foreach (int window in remote.ListeningTo.Keys)
                        if (MaxVolume.ContainsKey(window) && remote.VolumeIn > MaxVolume[window].Param1)
                            MaxVolume[window].Param1 = remote.VolumeIn;

                    remote.VolumeIn = 0;
                }

                // alert each window with its current volume status in/out and reset
                foreach (int window in MaxVolume.Keys)
                {
                    Tuple<int, int> volume = MaxVolume[window];

                    Core.RunInGuiThread(VolumeUpdate[window], volume.Param1, volume.Param2);

                    volume.Param1 = 0;
                    volume.Param2 = 0;
                }

                LastUpdate.Reset();
                LastUpdate.Start();
            }
        }

        void Session_Data(RudpSession session, byte[] data)
        {
            G2Header root = new G2Header(data);

            if (G2Protocol.ReadPacket(root))
            {
                switch (root.Name)
                {
                    case VoicePacket.Audio:
                        ReceiveAudio(AudioPacket.Decode(root), session);
                        break;
                }
            }
        }

        private void ReceiveAudio(AudioPacket packet, RudpSession session)
        {
            if (!RemoteVoices.ContainsKey(session.UserID))
                RemoteVoices[session.UserID] = new RemoteVoice();

            RemoteVoice user = RemoteVoices[session.UserID];

            if (!user.Streams.ContainsKey(session.RoutingID))
            {
                user.Streams[session.RoutingID] = new PlayAudio(this, packet.FrameSize, user);
                Players.SafeAdd(user.Streams[session.RoutingID]);
            }

            PlayAudio stream = user.Streams[session.RoutingID];

            // reset if user changed quality setting
            if (stream.FrameSize != packet.FrameSize)
            {
                stream.Dispose();
                user.Streams[session.RoutingID] = new PlayAudio(this, packet.FrameSize, user);
                Players.SafeAdd(user.Streams[session.RoutingID]);
                stream = user.Streams[session.RoutingID];
            }

            StartAudioThread(false);

            stream.Receive_AudioData(packet.Audio);

            UpdateVolume();
        }
    }

    class RemoteVoice
    {
        // window and direction audio comes from
        internal Dictionary<int, AudioDirection> ListeningTo = new Dictionary<int, AudioDirection>();

        // routing ID, and audio stream for that user
        internal Dictionary<ulong, PlayAudio> Streams = new Dictionary<ulong, PlayAudio>();

        internal int VolumeIn;


        internal AudioDirection GetDirection()
        {
            AudioDirection result = AudioDirection.None;

            foreach (AudioDirection direction in ListeningTo.Values)
            {
                if (direction == AudioDirection.None)
                    continue;

                if (result == AudioDirection.None)
                    result = direction;

                else if ( direction == AudioDirection.Both ||
                         (direction == AudioDirection.Left && result == AudioDirection.Right) || 
                         (direction == AudioDirection.Right && result == AudioDirection.Left))
                    result = AudioDirection.Both;


                if (result == AudioDirection.Both)
                    break;
            }

            return result;
        }
    }

    internal class VoicePacket
    {
        internal const byte Audio = 0x10;
    }

    internal class AudioPacket : G2Packet
    {
        const byte Packet_FrameSize = 0x10;


        internal byte[] Audio;
        internal int FrameSize;


        internal override byte[] Encode(G2Protocol protocol)
        {
            lock (protocol.WriteSection)
            {
                G2Frame packet = protocol.WritePacket(null, VoicePacket.Audio, Audio);

                protocol.WritePacket(packet, Packet_FrameSize, CompactNum.GetBytes(FrameSize));

                return protocol.WriteFinish();
            }
        }

        internal static AudioPacket Decode(G2Header root)
        {
            AudioPacket packet = new AudioPacket();
            
            if (G2Protocol.ReadPayload(root))
                packet.Audio = Utilities.ExtractBytes(root.Data, root.PayloadPos, root.PayloadSize);

            G2Protocol.ResetPacket(root);

            G2Header child = new G2Header(root.Data);

            while (G2Protocol.ReadNextChild(root, child) == G2ReadResult.PACKET_GOOD)
            {
                if (!G2Protocol.ReadPayload(child))
                    continue;

                switch (child.Name)
                {
                    case Packet_FrameSize:
                        packet.FrameSize = CompactNum.ToInt32(child.Data, child.PayloadPos, child.PayloadSize);
                        break;
                }
            }

            return packet;
        }
    }
}