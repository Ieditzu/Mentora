using System;
using System.Collections.Generic;
using System.IO;

namespace Mentora.Network
{
    public class PacketManager
    {
        public Packet CreatePacket(int id)
        {
            return id switch
            {
                1 => new HandShakePacket(),
                2 => new AuthPacket(),
                4 => new AddChildPacket(),
                8 => new CompleteTaskPacket(),
                9 => new ActionResponsePacket(),
                10 => new AuthResponsePacket(),
                11 => new FetchTasksPacket(),
                12 => new FetchTasksResponsePacket(),
                15 => new FetchChildrenPacket(),
                16 => new FetchChildrenResponsePacket(),
                19 => new GenerateQRLoginPacket(),
                20 => new QRLoginResponsePacket(),
                21 => new ClaimQRLoginPacket(),
                22 => new ChildAuthResponsePacket(),
                23 => new FetchChildStatsPacket(),
                24 => new FetchChildStatsResponsePacket(),
                25 => new VerifySessionPacket(),
                28 => new ExecuteCPPCodePacket(),
                29 => new ExecuteCPPCodeResponsePacket(),
                30 => new AskAiPacket(),
                31 => new AiResponsePacket(),
                13 => new FetchGoalsPacket(),
                14 => new FetchGoalsResponsePacket(),
                33 => new RecordLearningEventPacket(),
                34 => new ExecutePythonCodePacket(),
                35 => new ExecutePythonCodeResponsePacket(),
                36 => new FetchPublishedCoursesPacket(),
                37 => new FetchPublishedCoursesResponsePacket(),
                38 => new FetchCourseDetailPacket(),
                39 => new FetchCourseDetailResponsePacket(),
                40 => new SubmitCourseCompletionPacket(),
                41 => new FetchAllChildrenPacket(),
                42 => new FetchAllChildrenResponsePacket(),
                43 => new DevLoginAsChildPacket(),
                44 => new DevCreateChildProfilePacket(),
                45 => new GenerateAiTaskPacket(),
                46 => new GenerateAiTaskResponsePacket(),
                47 => new CompanionSpeakPacket(),
                48 => new CompanionSpeakResponsePacket(),
                49 => new MultiplayerJoinPacket(),
                50 => new MultiplayerWelcomePacket(),
                51 => new MultiplayerPlayerStatePacket(),
                52 => new MultiplayerPlayerLeftPacket(),
                53 => new QuizStartPacket(),
                54 => new QuizAnswerPacket(),
                55 => new QuizResultPacket(),
                56 => new MultiplayerVoicePacket(),
                57 => new MultiplayerUdpHelloPacket(),
                58 => new CompanionVoiceTextPacket(),
                59 => new CompanionVoiceAudioPacket(),
                60 => new CodeWorldCommandPacket(),
                61 => new CodeWorldStatePacket(),
                _ => throw new Exception("Unknown packet ID: " + id),
            };
        }
    }

    public class HandShakePacket : Packet
    {
        public string HostId;
        public HandShakePacket(string hostId) : base(1) { HostId = hostId; }
        public HandShakePacket() : base(1) { }
        protected override void Write(BinaryWriter writer) { PutString(writer, HostId); }
        protected override void Read(BinaryReader reader) { HostId = ReadString(reader); }
    }

    public class AuthPacket : Packet
    {
        public string EmailHash;
        public string PasswordHash;
        public AuthPacket(string emailHash, string passwordHash) : base(2) { EmailHash = emailHash; PasswordHash = passwordHash; }
        public AuthPacket() : base(2) { }
        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, EmailHash ?? string.Empty);
            PutString(writer, PasswordHash ?? string.Empty);
        }
        protected override void Read(BinaryReader reader)
        {
            EmailHash = ReadString(reader);
            PasswordHash = ReadString(reader);
        }
    }

    public class GenerateQRLoginPacket : Packet
    {
        public GenerateQRLoginPacket() : base(19) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader) { }
    }

    public class ClaimQRLoginPacket : Packet
    {
        public string Token;
        public long ChildId;
        public ClaimQRLoginPacket(string token, long childId) : base(21) { Token = token; ChildId = childId; }
        public ClaimQRLoginPacket() : base(21) { }
        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, Token ?? string.Empty);
            byte[] childBytes = BitConverter.GetBytes(ChildId);
            if (BitConverter.IsLittleEndian) Array.Reverse(childBytes);
            writer.Write(childBytes);
        }
        protected override void Read(BinaryReader reader)
        {
            Token = ReadString(reader);
            byte[] childBytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(childBytes);
            ChildId = BitConverter.ToInt64(childBytes, 0);
        }
    }

    public class QRLoginResponsePacket : Packet
    {
        public string Token;
        public QRLoginResponsePacket() : base(20) { }
        protected override void Write(BinaryWriter writer) { PutString(writer, Token); }
        protected override void Read(BinaryReader reader) { Token = ReadString(reader); }
    }

    public class ChildAuthResponsePacket : Packet
    {
        public bool Success;
        public long ChildId;
        public string ChildName;
        public string SessionToken;
        public ChildAuthResponsePacket() : base(22) { }
        protected override void Write(BinaryWriter writer)
        {
            writer.Write((byte)(Success ? 1 : 0));
            byte[] longBytes = BitConverter.GetBytes(ChildId);
            if (BitConverter.IsLittleEndian) Array.Reverse(longBytes);
            writer.Write(longBytes);
            PutString(writer, ChildName);
            PutString(writer, SessionToken);
        }
        protected override void Read(BinaryReader reader)
        {
            Success = reader.ReadByte() == 1;
            byte[] longBytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(longBytes);
            ChildId = BitConverter.ToInt64(longBytes, 0);
            ChildName = ReadString(reader);
            SessionToken = ReadString(reader);
        }
    }

    public class AuthResponsePacket : Packet
    {
        public bool Success;
        public long ParentId;
        public string Message;
        public string ParentPfp;
        public AuthResponsePacket() : base(10) { }
        protected override void Write(BinaryWriter writer)
        {
            writer.Write((byte)(Success ? 1 : 0));
            byte[] longBytes = BitConverter.GetBytes(ParentId);
            if (BitConverter.IsLittleEndian) Array.Reverse(longBytes);
            writer.Write(longBytes);
            PutString(writer, Message);
            PutString(writer, ParentPfp);
        }
        protected override void Read(BinaryReader reader)
        {
            Success = reader.ReadByte() == 1;
            byte[] longBytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(longBytes);
            ParentId = BitConverter.ToInt64(longBytes, 0);
            Message = ReadString(reader);
            ParentPfp = ReadString(reader);
        }
    }

    public class VerifySessionPacket : Packet
    {
        public long ChildId;
        public string SessionToken;
        public VerifySessionPacket(long childId, string sessionToken) : base(25) { ChildId = childId; SessionToken = sessionToken; }
        public VerifySessionPacket() : base(25) { }
        protected override void Write(BinaryWriter writer)
        {
            byte[] longBytes = BitConverter.GetBytes(ChildId);
            if (BitConverter.IsLittleEndian) Array.Reverse(longBytes);
            writer.Write(longBytes);
            PutString(writer, SessionToken);
        }
        protected override void Read(BinaryReader reader)
        {
            byte[] longBytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(longBytes);
            ChildId = BitConverter.ToInt64(longBytes, 0);
            SessionToken = ReadString(reader);
        }
    }

    public class CompleteTaskPacket : Packet
    {
        public long ChildId;
        public long TaskId;
        public CompleteTaskPacket(long childId, long taskId) : base(8) { ChildId = childId; TaskId = taskId; }
        public CompleteTaskPacket() : base(8) { }
        protected override void Write(BinaryWriter writer)
        {
            byte[] cBytes = BitConverter.GetBytes(ChildId);
            if (BitConverter.IsLittleEndian) Array.Reverse(cBytes);
            writer.Write(cBytes);
            byte[] tBytes = BitConverter.GetBytes(TaskId);
            if (BitConverter.IsLittleEndian) Array.Reverse(tBytes);
            writer.Write(tBytes);
        }
        protected override void Read(BinaryReader reader)
        {
            byte[] cBytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(cBytes);
            ChildId = BitConverter.ToInt64(cBytes, 0);
            byte[] tBytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(tBytes);
            TaskId = BitConverter.ToInt64(tBytes, 0);
        }
    }

    public class AddChildPacket : Packet
    {
        public string ChildName;
        public AddChildPacket(string childName) : base(4) { ChildName = childName; }
        public AddChildPacket() : base(4) { }
        protected override void Write(BinaryWriter writer) { PutString(writer, ChildName ?? string.Empty); }
        protected override void Read(BinaryReader reader) { ChildName = ReadString(reader); }
    }

    public class ActionResponsePacket : Packet
    {
        public int RequestPacketId;
        public bool Success;
        public string Message;
        public long ResultId;
        public ActionResponsePacket() : base(9) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader)
        {
            RequestPacketId = ReadInt32BigEndian(reader);
            Success = reader.ReadByte() == 1;
            Message = ReadString(reader);
            byte[] rBytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(rBytes);
            ResultId = BitConverter.ToInt64(rBytes, 0);
        }
    }

    public class FetchTasksPacket : Packet
    {
        public FetchTasksPacket() : base(11) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader) { }
    }

    public class FetchChildrenPacket : Packet
    {
        public FetchChildrenPacket() : base(15) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader) { }
    }

    public class FetchChildrenResponsePacket : Packet
    {
        public struct ChildDto
        {
            public long Id;
            public string Name;
            public int TotalPoints;
            public bool IsOnline;
            public string ProfilePicture;
        }

        public List<ChildDto> Children = new List<ChildDto>();
        public FetchChildrenResponsePacket() : base(16) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader)
        {
            int size = ReadInt32BigEndian(reader);
            for (int i = 0; i < size; i++)
            {
                byte[] idBytes = reader.ReadBytes(8);
                if (BitConverter.IsLittleEndian) Array.Reverse(idBytes);
                long id = BitConverter.ToInt64(idBytes, 0);
                string name = ReadString(reader);
                int totalPoints = ReadInt32BigEndian(reader);
                bool isOnline = reader.ReadByte() == 1;
                string profilePicture = ReadString(reader);
                Children.Add(new ChildDto
                {
                    Id = id,
                    Name = name,
                    TotalPoints = totalPoints,
                    IsOnline = isOnline,
                    ProfilePicture = profilePicture
                });
            }
        }
    }

    public class FetchTasksResponsePacket : Packet
    {
        public struct TaskDto { public long Id; public string Title; public int Points; }
        public List<TaskDto> Tasks = new List<TaskDto>();
        public FetchTasksResponsePacket() : base(12) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader)
        {
            int size = ReadInt32BigEndian(reader);
            for (int i = 0; i < size; i++)
            {
                byte[] idBytes = reader.ReadBytes(8);
                if (BitConverter.IsLittleEndian) Array.Reverse(idBytes);
                long id = BitConverter.ToInt64(idBytes, 0);
                string title = ReadString(reader);
                int points = ReadInt32BigEndian(reader);
                Tasks.Add(new TaskDto { Id = id, Title = title, Points = points });
            }
        }
    }

    public class FetchChildStatsPacket : Packet
    {
        public FetchChildStatsPacket() : base(23) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader) { }
    }

    public class FetchChildStatsResponsePacket : Packet
    {
        public string Name;
        public int TotalPoints;
        public string GameStatsJson;
        public int Streak;
        public int CompletedTaskCount;
        public int TotalTaskCount;
        public FetchChildStatsResponsePacket() : base(24) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader)
        {
            Name = ReadString(reader);
            TotalPoints = ReadInt32BigEndian(reader);
            GameStatsJson = ReadString(reader);
            Streak = ReadInt32BigEndian(reader);
            CompletedTaskCount = ReadInt32BigEndian(reader);
            TotalTaskCount = ReadInt32BigEndian(reader);
        }
    }

    public class ExecuteCPPCodePacket : Packet
    {
        public string Code;
        public ExecuteCPPCodePacket(string code) : base(28) { Code = code; }
        public ExecuteCPPCodePacket() : base(28) { }
        protected override void Write(BinaryWriter writer) { PutString(writer, Code ?? string.Empty); }
        protected override void Read(BinaryReader reader) { Code = ReadString(reader); }
    }

    public class ExecuteCPPCodeResponsePacket : Packet
    {
        public string Output;
        public string Error;
        public ExecuteCPPCodeResponsePacket(string output, string error) : base(29) { Output = output; Error = error; }
        public ExecuteCPPCodeResponsePacket() : base(29) { }
        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, Output ?? string.Empty);
            PutString(writer, Error ?? string.Empty);
        }
        protected override void Read(BinaryReader reader)
        {
            Output = ReadString(reader);
            Error = ReadString(reader);
        }
    }

    public class ExecutePythonCodePacket : Packet
    {
        public string Code;
        public ExecutePythonCodePacket(string code) : base(34) { Code = code; }
        public ExecutePythonCodePacket() : base(34) { }
        protected override void Write(BinaryWriter writer) { PutString(writer, Code ?? string.Empty); }
        protected override void Read(BinaryReader reader) { Code = ReadString(reader); }
    }

    public class ExecutePythonCodeResponsePacket : Packet
    {
        public string Output;
        public string Error;
        public ExecutePythonCodeResponsePacket(string output, string error) : base(35) { Output = output; Error = error; }
        public ExecutePythonCodeResponsePacket() : base(35) { }
        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, Output ?? string.Empty);
            PutString(writer, Error ?? string.Empty);
        }
        protected override void Read(BinaryReader reader)
        {
            Output = ReadString(reader);
            Error = ReadString(reader);
        }
    }

    public class FetchPublishedCoursesPacket : Packet
    {
        public FetchPublishedCoursesPacket() : base(36) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader) { }
    }

    public class FetchPublishedCoursesResponsePacket : Packet
    {
        public string CoursesJson;
        public FetchPublishedCoursesResponsePacket() : base(37) { }
        public FetchPublishedCoursesResponsePacket(string coursesJson) : base(37) { CoursesJson = coursesJson; }
        protected override void Write(BinaryWriter writer) { PutString(writer, CoursesJson ?? "[]"); }
        protected override void Read(BinaryReader reader) { CoursesJson = ReadString(reader); }
    }

    public class FetchCourseDetailPacket : Packet
    {
        public long CourseId;
        public FetchCourseDetailPacket(long courseId) : base(38) { CourseId = courseId; }
        public FetchCourseDetailPacket() : base(38) { }
        protected override void Write(BinaryWriter writer)
        {
            byte[] bytes = BitConverter.GetBytes(CourseId);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            writer.Write(bytes);
        }
        protected override void Read(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            CourseId = BitConverter.ToInt64(bytes, 0);
        }
    }

    public class FetchCourseDetailResponsePacket : Packet
    {
        public string CourseJson;
        public FetchCourseDetailResponsePacket() : base(39) { }
        public FetchCourseDetailResponsePacket(string courseJson) : base(39) { CourseJson = courseJson; }
        protected override void Write(BinaryWriter writer) { PutString(writer, CourseJson ?? "{}"); }
        protected override void Read(BinaryReader reader) { CourseJson = ReadString(reader); }
    }

    public class SubmitCourseCompletionPacket : Packet
    {
        public long CourseId;
        public int Score;
        public int TotalQuestions;
        public SubmitCourseCompletionPacket() : base(40) { }
        public SubmitCourseCompletionPacket(long courseId, int score, int totalQuestions) : base(40)
        {
            CourseId = courseId;
            Score = score;
            TotalQuestions = totalQuestions;
        }
        protected override void Write(BinaryWriter writer)
        {
            byte[] bytes = BitConverter.GetBytes(CourseId);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            writer.Write(bytes);
            WriteInt32BigEndian(writer, Score);
            WriteInt32BigEndian(writer, TotalQuestions);
        }
        protected override void Read(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            CourseId = BitConverter.ToInt64(bytes, 0);
            Score = ReadInt32BigEndian(reader);
            TotalQuestions = ReadInt32BigEndian(reader);
        }
    }

    public class FetchAllChildrenPacket : Packet
    {
        public FetchAllChildrenPacket() : base(41) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader) { }
    }

    public class FetchAllChildrenResponsePacket : Packet
    {
        public struct ChildDto
        {
            public long Id;
            public string Name;
            public int TotalPoints;
            public bool IsOnline;
            public string ProfilePicture;
        }

        public List<ChildDto> Children = new List<ChildDto>();
        public FetchAllChildrenResponsePacket() : base(42) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader)
        {
            int size = ReadInt32BigEndian(reader);
            for (int i = 0; i < size; i++)
            {
                byte[] idBytes = reader.ReadBytes(8);
                if (BitConverter.IsLittleEndian) Array.Reverse(idBytes);
                long id = BitConverter.ToInt64(idBytes, 0);
                string name = ReadString(reader);
                int totalPoints = ReadInt32BigEndian(reader);
                bool isOnline = reader.ReadByte() == 1;
                string profilePicture = ReadString(reader);
                Children.Add(new ChildDto
                {
                    Id = id,
                    Name = name,
                    TotalPoints = totalPoints,
                    IsOnline = isOnline,
                    ProfilePicture = profilePicture
                });
            }
        }
    }

    public class DevLoginAsChildPacket : Packet
    {
        public long ChildId;
        public DevLoginAsChildPacket(long childId) : base(43) { ChildId = childId; }
        public DevLoginAsChildPacket() : base(43) { }
        protected override void Write(BinaryWriter writer)
        {
            byte[] bytes = BitConverter.GetBytes(ChildId);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            writer.Write(bytes);
        }
        protected override void Read(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            ChildId = BitConverter.ToInt64(bytes, 0);
        }
    }

    public class DevCreateChildProfilePacket : Packet
    {
        public string ChildName;
        public DevCreateChildProfilePacket(string childName) : base(44) { ChildName = childName; }
        public DevCreateChildProfilePacket() : base(44) { }
        protected override void Write(BinaryWriter writer) { PutString(writer, ChildName ?? string.Empty); }
        protected override void Read(BinaryReader reader) { ChildName = ReadString(reader); }
    }

    public class AskAiPacket : Packet
    {
        public string Question;
        public string Context;
        public AskAiPacket(string question, string context) : base(30) { Question = question; Context = context; }
        public AskAiPacket() : base(30) { }
        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, Question ?? string.Empty);
            PutString(writer, Context ?? string.Empty);
        }
        protected override void Read(BinaryReader reader)
        {
            Question = ReadString(reader);
            Context = ReadString(reader);
        }
    }

    public class AiResponsePacket : Packet
    {
        public string Response;
        public AiResponsePacket(string response) : base(31) { Response = response; }
        public AiResponsePacket() : base(31) { }
        protected override void Write(BinaryWriter writer) { PutString(writer, Response ?? string.Empty); }
        protected override void Read(BinaryReader reader) { Response = ReadString(reader); }
    }

    public class FetchGoalsPacket : Packet
    {
        public long ChildId;
        public FetchGoalsPacket(long childId) : base(13) { ChildId = childId; }
        public FetchGoalsPacket() : base(13) { }
        protected override void Write(BinaryWriter writer)
        {
            byte[] bytes = BitConverter.GetBytes(ChildId);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            writer.Write(bytes);
        }
        protected override void Read(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            ChildId = BitConverter.ToInt64(bytes, 0);
        }
    }

    public class FetchGoalsResponsePacket : Packet
    {
        public struct GoalDto
        {
            public long Id;
            public string Title;
            public string Reward;
            public bool IsCompleted;
            public int RequiredPoints;
            public long RequiredTaskId;
        }
        public List<GoalDto> Goals = new List<GoalDto>();
        public FetchGoalsResponsePacket() : base(14) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader)
        {
            int size = ReadInt32BigEndian(reader);
            for (int i = 0; i < size; i++)
            {
                byte[] idBytes = reader.ReadBytes(8);
                if (BitConverter.IsLittleEndian) Array.Reverse(idBytes);
                long id = BitConverter.ToInt64(idBytes, 0);
                string title = ReadString(reader);
                string reward = ReadString(reader);
                bool isCompleted = reader.ReadByte() == 1;
                int reqPoints = ReadInt32BigEndian(reader);
                byte[] taskBytes = reader.ReadBytes(8);
                if (BitConverter.IsLittleEndian) Array.Reverse(taskBytes);
                long reqTaskId = BitConverter.ToInt64(taskBytes, 0);
                Goals.Add(new GoalDto { Id = id, Title = title, Reward = reward, IsCompleted = isCompleted, RequiredPoints = reqPoints, RequiredTaskId = reqTaskId });
            }
        }
    }

    public class RecordLearningEventPacket : Packet
    {
        public string EventType;
        public string Topic;
        public int Correctness;
        public string Details;

        public RecordLearningEventPacket() : base(33) { }
        public RecordLearningEventPacket(string eventType, string topic, int correctness, string details) : base(33)
        {
            EventType = eventType;
            Topic = topic;
            Correctness = correctness;
            Details = details;
        }

        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, EventType ?? string.Empty);
            PutString(writer, Topic ?? string.Empty);
            WriteInt32BigEndian(writer, Correctness);
            PutString(writer, Details ?? string.Empty);
        }

        protected override void Read(BinaryReader reader)
        {
            EventType = ReadString(reader);
            Topic = ReadString(reader);
            Correctness = ReadInt32BigEndian(reader);
            Details = ReadString(reader);
        }
    }

    // ── AI-Generated Task Packets (45 / 46) ──────────────────────────────────

    public class GenerateAiTaskPacket : Packet
    {
        public string Language; // "python" or "cpp"
        public GenerateAiTaskPacket(string language = "python") : base(45) { Language = language; }
        public GenerateAiTaskPacket() : base(45) { Language = "python"; }
        protected override void Write(BinaryWriter writer) { PutString(writer, Language ?? "python"); }
        protected override void Read(BinaryReader reader) { Language = ReadString(reader); }
    }

    public class GenerateAiTaskResponsePacket : Packet
    {
        public long TaskId;
        public string Title;
        public string Description;
        public string CodeTemplate;
        public string Language;
        public int PointValue;

        public GenerateAiTaskResponsePacket() : base(46) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader)
        {
            byte[] idBytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(idBytes);
            TaskId = BitConverter.ToInt64(idBytes, 0);
            Title = ReadString(reader);
            Description = ReadString(reader);
            CodeTemplate = ReadString(reader);
            Language = ReadString(reader);
            PointValue = ReadInt32BigEndian(reader);
        }
    }

    // ── Companion Speak Packets (47 / 48) ────────────────────────────────────

    public class CompanionSpeakPacket : Packet
    {
        public string Trigger;
        public CompanionSpeakPacket(string trigger) : base(47) { Trigger = trigger; }
        public CompanionSpeakPacket() : base(47) { Trigger = "idle"; }
        protected override void Write(BinaryWriter writer) { PutString(writer, Trigger ?? "idle"); }
        protected override void Read(BinaryReader reader) { Trigger = ReadString(reader); }
    }

    public class CompanionSpeakResponsePacket : Packet
    {
        public string Line;
        public string Emotion; // "happy" | "encouraging" | "concerned" | "excited" | "thinking"
        public string SourceTranscript;
        public CompanionSpeakResponsePacket() : base(48) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader)
        {
            Line = ReadString(reader);
            Emotion = ReadString(reader);
            SourceTranscript = reader.BaseStream.Position < reader.BaseStream.Length ? ReadString(reader) : string.Empty;
        }
    }

    public class CompanionVoiceTextPacket : Packet
    {
        public string Transcript;
        public string Context;

        public CompanionVoiceTextPacket(string transcript, string context) : base(58)
        {
            Transcript = transcript;
            Context = context;
        }

        public CompanionVoiceTextPacket() : base(58) { }

        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, Transcript ?? string.Empty);
            PutString(writer, Context ?? string.Empty);
        }

        protected override void Read(BinaryReader reader)
        {
            Transcript = ReadString(reader);
            Context = ReadString(reader);
        }
    }

    public class CompanionVoiceAudioPacket : Packet
    {
        private const int MaxVoiceAudioBytes = 512 * 1024;

        public int SampleRate;
        public byte[] Pcm16;
        public string Context;

        public CompanionVoiceAudioPacket(int sampleRate, byte[] pcm16, string context) : base(59)
        {
            SampleRate = sampleRate;
            Pcm16 = pcm16 ?? Array.Empty<byte>();
            Context = context;
        }

        public CompanionVoiceAudioPacket() : base(59) { }

        protected override void Write(BinaryWriter writer)
        {
            WriteInt32BigEndian(writer, SampleRate);
            byte[] data = Pcm16 ?? Array.Empty<byte>();
            WriteInt32BigEndian(writer, data.Length);
            writer.Write(data);
            PutString(writer, Context ?? string.Empty);
        }

        protected override void Read(BinaryReader reader)
        {
            SampleRate = ReadInt32BigEndian(reader);
            int length = ReadInt32BigEndian(reader);
            if (length < 0 || length > MaxVoiceAudioBytes)
            {
                Pcm16 = Array.Empty<byte>();
                Context = string.Empty;
                return;
            }

            Pcm16 = reader.ReadBytes(length);
            Context = ReadString(reader);
        }
    }

    public class MultiplayerJoinPacket : Packet
    {
        public string PlayerName;
        public MultiplayerJoinPacket(string playerName) : base(49) { PlayerName = playerName; }
        public MultiplayerJoinPacket() : base(49) { }
        protected override void Write(BinaryWriter writer) { PutString(writer, PlayerName ?? string.Empty); }
        protected override void Read(BinaryReader reader) { PlayerName = ReadString(reader); }
    }

    public class MultiplayerWelcomePacket : Packet
    {
        public string ClientId;
        public string PlayerName;
        public MultiplayerWelcomePacket(string clientId, string playerName) : base(50) { ClientId = clientId; PlayerName = playerName; }
        public MultiplayerWelcomePacket() : base(50) { }
        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, ClientId ?? string.Empty);
            PutString(writer, PlayerName ?? string.Empty);
        }
        protected override void Read(BinaryReader reader)
        {
            ClientId = ReadString(reader);
            PlayerName = ReadString(reader);
        }
    }

    public class MultiplayerPlayerStatePacket : Packet
    {
        public string ClientId;
        public string PlayerName;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float Yaw;
        public int Sequence;
        public string ModelId;

        public MultiplayerPlayerStatePacket(string clientId, string playerName, UnityEngine.Vector3 position, float yaw, int sequence = 0, string modelId = "") : base(51)
        {
            ClientId = clientId;
            PlayerName = playerName;
            PositionX = position.x;
            PositionY = position.y;
            PositionZ = position.z;
            Yaw = yaw;
            Sequence = sequence;
            ModelId = modelId;
        }
        public MultiplayerPlayerStatePacket() : base(51) { }
        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, ClientId ?? string.Empty);
            PutString(writer, PlayerName ?? string.Empty);
            writer.Write(PositionX);
            writer.Write(PositionY);
            writer.Write(PositionZ);
            writer.Write(Yaw);
            WriteInt32BigEndian(writer, Sequence);
            PutString(writer, ModelId ?? string.Empty);
        }
        protected override void Read(BinaryReader reader)
        {
            ClientId = ReadString(reader);
            PlayerName = ReadString(reader);
            PositionX = reader.ReadSingle();
            PositionY = reader.ReadSingle();
            PositionZ = reader.ReadSingle();
            Yaw = reader.ReadSingle();
            Sequence = reader.BaseStream.Position + 4 <= reader.BaseStream.Length
                ? ReadInt32BigEndian(reader)
                : 0;
            ModelId = reader.BaseStream.Position + 4 <= reader.BaseStream.Length
                ? ReadString(reader)
                : string.Empty;
        }
    }

    public class MultiplayerPlayerLeftPacket : Packet
    {
        public string ClientId;
        public MultiplayerPlayerLeftPacket(string clientId) : base(52) { ClientId = clientId; }
        public MultiplayerPlayerLeftPacket() : base(52) { }
        protected override void Write(BinaryWriter writer) { PutString(writer, ClientId ?? string.Empty); }
        protected override void Read(BinaryReader reader) { ClientId = ReadString(reader); }
    }

    public class MultiplayerVoicePacket : Packet
    {
        public string ClientId;
        public int Sequence;
        public int SampleRate;
        public byte[] Pcm16;

        public MultiplayerVoicePacket(string clientId, int sequence, int sampleRate, byte[] pcm16) : base(56)
        {
            ClientId = clientId;
            Sequence = sequence;
            SampleRate = sampleRate;
            Pcm16 = pcm16 ?? Array.Empty<byte>();
        }

        public MultiplayerVoicePacket() : base(56) { }

        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, ClientId ?? string.Empty);
            WriteInt32BigEndian(writer, Sequence);
            WriteInt32BigEndian(writer, SampleRate);
            byte[] data = Pcm16 ?? Array.Empty<byte>();
            WriteInt32BigEndian(writer, data.Length);
            writer.Write(data);
        }

        protected override void Read(BinaryReader reader)
        {
            ClientId = ReadString(reader);
            Sequence = ReadInt32BigEndian(reader);
            SampleRate = ReadInt32BigEndian(reader);
            int length = ReadInt32BigEndian(reader);
            if (length < 0 || length > 32768)
            {
                Pcm16 = Array.Empty<byte>();
                return;
            }

            Pcm16 = reader.ReadBytes(length);
        }
    }

    public class MultiplayerUdpHelloPacket : Packet
    {
        public string ClientId;
        public string PlayerName;

        public MultiplayerUdpHelloPacket(string clientId, string playerName) : base(57)
        {
            ClientId = clientId;
            PlayerName = playerName;
        }

        public MultiplayerUdpHelloPacket() : base(57) { }

        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, ClientId ?? string.Empty);
            PutString(writer, PlayerName ?? string.Empty);
        }

        protected override void Read(BinaryReader reader)
        {
            ClientId = ReadString(reader);
            PlayerName = ReadString(reader);
        }
    }

    public class CodeWorldCommandPacket : Packet
    {
        public string CommandText;
        public string AuthorClientId;

        public CodeWorldCommandPacket(string commandText, string authorClientId = "") : base(60)
        {
            CommandText = commandText;
            AuthorClientId = authorClientId;
        }

        public CodeWorldCommandPacket() : base(60) { }

        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, CommandText ?? string.Empty);
            PutString(writer, AuthorClientId ?? string.Empty);
        }

        protected override void Read(BinaryReader reader)
        {
            CommandText = ReadString(reader);
            AuthorClientId = ReadString(reader);
        }
    }

    public class CodeWorldStatePacket : Packet
    {
        public bool IsActive;
        public string HistoryText;

        public CodeWorldStatePacket(bool isActive, string historyText) : base(61)
        {
            IsActive = isActive;
            HistoryText = historyText;
        }

        public CodeWorldStatePacket() : base(61) { }

        protected override void Write(BinaryWriter writer)
        {
            WriteInt32BigEndian(writer, IsActive ? 1 : 0);
            PutString(writer, HistoryText ?? string.Empty);
        }

        protected override void Read(BinaryReader reader)
        {
            IsActive = ReadInt32BigEndian(reader) != 0;
            HistoryText = ReadString(reader);
        }
    }

    // ── Quiz Packets (53 / 54 / 55) ──────────────────────────────────────────

    /// <summary>Host → all: start a question round.</summary>
    public class QuizStartPacket : Packet
    {
        public string Prompt;
        public string OptionsStr; // pipe-delimited: "Option A|Option B|Option C|Option D"
        public int CorrectIndex;
        public int QuestionIndex;
        public int Total;
        public int TimerSeconds;

        public QuizStartPacket(string prompt, string optionsStr, int correctIndex,
            int questionIndex, int total, int timerSeconds) : base(53)
        {
            Prompt = prompt; OptionsStr = optionsStr; CorrectIndex = correctIndex;
            QuestionIndex = questionIndex; Total = total; TimerSeconds = timerSeconds;
        }
        public QuizStartPacket() : base(53) { }

        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, Prompt ?? string.Empty);
            PutString(writer, OptionsStr ?? string.Empty);
            WriteInt32BigEndian(writer, CorrectIndex);
            WriteInt32BigEndian(writer, QuestionIndex);
            WriteInt32BigEndian(writer, Total);
            WriteInt32BigEndian(writer, TimerSeconds);
        }
        protected override void Read(BinaryReader reader)
        {
            Prompt = ReadString(reader);
            OptionsStr = ReadString(reader);
            CorrectIndex = ReadInt32BigEndian(reader);
            QuestionIndex = ReadInt32BigEndian(reader);
            Total = ReadInt32BigEndian(reader);
            TimerSeconds = ReadInt32BigEndian(reader);
        }
    }

    /// <summary>Client → host: player submits their answer.</summary>
    public class QuizAnswerPacket : Packet
    {
        public string ClientId;
        public int AnswerIndex;

        public QuizAnswerPacket(string clientId, int answerIndex) : base(54)
        { ClientId = clientId; AnswerIndex = answerIndex; }
        public QuizAnswerPacket() : base(54) { }

        protected override void Write(BinaryWriter writer)
        {
            PutString(writer, ClientId ?? string.Empty);
            WriteInt32BigEndian(writer, AnswerIndex);
        }
        protected override void Read(BinaryReader reader)
        {
            ClientId = ReadString(reader);
            AnswerIndex = ReadInt32BigEndian(reader);
        }
    }

    /// <summary>Host → all: reveal correct answer and current scores.</summary>
    public class QuizResultPacket : Packet
    {
        public int CorrectIndex;
        public string ScoresJson; // "clientId:score,clientId:score"

        public QuizResultPacket(int correctIndex, string scoresJson) : base(55)
        { CorrectIndex = correctIndex; ScoresJson = scoresJson; }
        public QuizResultPacket() : base(55) { }

        protected override void Write(BinaryWriter writer)
        {
            WriteInt32BigEndian(writer, CorrectIndex);
            PutString(writer, ScoresJson ?? string.Empty);
        }
        protected override void Read(BinaryReader reader)
        {
            CorrectIndex = ReadInt32BigEndian(reader);
            ScoresJson = ReadString(reader);
        }
    }
}
