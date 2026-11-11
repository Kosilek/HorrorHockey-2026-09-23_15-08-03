using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using FmVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Shared.Core.Contracts
{
    public interface IRuntimeService
    {
        void SpawnUnit(Byte2 unitSlot, ushort indexUnit, Team team, PlayerIndex playerIndex);
        void CreateRoute(ushort indexUnit, FmVector2 start, FmVector2[] controls);
        void CreateRoute(ushort indexUnit, FmVector2 start, FmVector2[] controls, bool stopAtEnd, float startSpeed);
        void SlowUnit(ushort indexUnit, float speed);
        void StunUnit(ushort indexUnit);
        void UnstunUnit(ushort indexUnit);
        void StopUnit(ushort indexUnit);
        void StartAnimationClip(int indexUnit, byte animationClip);
        void SetTarget(ushort indexUnit, int indexTarget);
    }
}
