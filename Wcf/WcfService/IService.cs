using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace WcfService
{
    [DataContract]
    public enum MessageStatus
    {
        [EnumMember]
        Ok,

        [EnumMember]
        Error
    }

    [DataContract]
    public enum MessageError
    {
        [EnumMember]
        AlreadyRegistred,

        [EnumMember]
        TooManyRegistred
    }

    [DataContract]
    public class Message
    {
        [DataMember]
        public MessageStatus Status;

        [DataMember]
        public MessageError? Error;
    }

    [DataContract]
    public enum WorkerState
    {
        [EnumMember]
        Standby,

        [EnumMember]
        Active,

        [EnumMember]
        Dead
    }

    [DataContract]
    public class WorkerInfo
    {
        [DataMember]
        public WorkerState State;

        [DataMember]
        public DateTime LastHeartbeat;

        public ICallback Callback {  get; set; }
    }

    public interface ICallback
    {
        [OperationContract(IsOneWay = true)]
        void ChangeWorkerState(WorkerState newState);
    }

    [ServiceContract(CallbackContract = typeof(ICallback), SessionMode = SessionMode.Required)]
    public interface IService
    {
        [OperationContract]
        Message Register(int registrationWorkerId);

        [OperationContract]
        void SendHeartbeat(int workerId);
    }
}
