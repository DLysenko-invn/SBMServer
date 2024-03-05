

namespace BLE2TCP
{
    interface ILog
    {
        void LogWarning(string text);
        void LogLine(string text);
        void LogError(string text);
    }

    interface ITransport
    {
        byte[] ReadBytes(int size);
        int WriteBytes(byte[] data);
        bool Start();
        bool WaitForConnection();
        void CloseConnection();
        void Stop();
        bool IsConnected { get; }
        void OnTimer();
    }


    interface IPacketParser
    {
        PacketBase ProcessPacket(PacketBase packet);
        void ProcessPacketReset();

    }



    interface IPacketSender
    {
        bool SendPacket(PacketBase packet);
    }



    interface IPayload
    {

        void FromBytes(byte[] data);
        void FromBytes(byte[] data, int startindex, int size);

        byte[] ToBytes();

        int ByteSize { get; }

        

    }


    public interface IServerStatus
    { 
        const int SERVER_STOPPED = -1;
        const int RESET = -1;
        
        int Port { get;set; }
        string IP { get;set; }
        int ConnectionsCount { get;set; }
        ulong RXBytes { get; }
        ulong TXBytes { get; }
        
        
        
        void IncRX(int incval);
        void IncTX(int incval);
        
        void DeviceFound(IDeviceInfo devinfo);
        void DeviceListClear();
        IDeviceInfo[] Devices {get;}

    }

    public interface IDeviceInfo
    {
        string Name { get; }
        string Id { get; }
        string InterfaceName { get; }

        bool IsConnected { get;}

        bool IsTDK { get; }


        bool IsPaired { get; }
        bool IsConnectable { get; }

    }


}
