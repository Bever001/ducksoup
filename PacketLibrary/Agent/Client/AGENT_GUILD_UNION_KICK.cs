using PacketLibrary.Enums;
using SilkroadSecurityAPI;

namespace PacketLibrary.Agent.Client;

public class AGENT_GUILD_UNION_KICK : IPacketStructure
{
    public static ushort MsgId => 0x70FD;
    public static bool Encrypted => false;
    public static bool Massive => false;
    public PacketDirection FromDirection => PacketDirection.Client;
    public PacketDirection ToDirection => PacketDirection.Server;

    public Task Read(Packet packet)
    {
        throw new NotImplementedException();
    }

    public Packet Build()
    {
        throw new NotImplementedException();
    }

    public static Packet of()
    {
        throw new NotImplementedException();
    }
}

