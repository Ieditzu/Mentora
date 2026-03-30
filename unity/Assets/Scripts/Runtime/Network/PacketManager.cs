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
                8 => new CompleteTaskPacket(),
                9 => new ActionResponsePacket(),
                11 => new FetchTasksPacket(),
                12 => new FetchTasksResponsePacket(),
                19 => new GenerateQRLoginPacket(),
                20 => new QRLoginResponsePacket(),
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

    public class GenerateQRLoginPacket : Packet
    {
        public GenerateQRLoginPacket() : base(19) { }
        protected override void Write(BinaryWriter writer) { }
        protected override void Read(BinaryReader reader) { }
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
}
