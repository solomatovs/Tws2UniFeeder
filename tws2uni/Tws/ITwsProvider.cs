using IBApi;

namespace Tws2UniFeeder
{
    public interface ITwsProvider
    {
        void Connect(string host, int port, int clientId);
        bool IsConnected { get; }
        void Disconnect();
        void SubscribeTickByTick(string symbol, Contract contract);
    }
}
