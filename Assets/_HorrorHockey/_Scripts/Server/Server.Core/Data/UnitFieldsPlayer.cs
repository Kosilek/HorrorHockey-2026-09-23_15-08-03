using ContractsServer.Core.Enum;

namespace Server.Core.Data
{
    public struct UnitFieldsPlayer
    {
        public ushort Id { get; set; }
        public Team Team { get; set; }
        public PlayerIndex PlayerIndex { get; set; }
    }
}
