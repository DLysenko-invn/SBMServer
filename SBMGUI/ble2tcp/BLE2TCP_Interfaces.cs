

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










}
