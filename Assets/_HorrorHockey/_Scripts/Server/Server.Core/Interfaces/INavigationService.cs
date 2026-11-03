using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;

namespace Server.Core.Interfaces
{
    public enum TeamSide
    {
        Blue,
        Red
    }

    public enum RouteLane
    {
        Left,
        Middle,
        Right
    }

    public interface INavigationService
    {
        Vector2 GetStartPosition(PlayerIndex playerIndex);
        bool TryGetRoute(TeamSide team, RouteLane lane, out Vector2[] controls);

        /// <summary>
        /// <para>{EN} Controls from an arbitrary point to another with obstacle bypass. Controls[0] is from.</para>
        /// {RU} Контролы от произвольной точки до другой с обходом препятствий. Controls[0] — это from.
        /// </summary>
        bool TryBuildRoute(Vector2 from, Vector2 to, out Vector2[] controls);
    }
}
