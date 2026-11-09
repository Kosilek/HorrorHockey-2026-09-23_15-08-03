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

        /// <summary>
        /// <para>{EN} Controls through arbitrary waypoints with obstacle bypass. Controls[0] is waypoints[0].</para>
        /// {RU} Контролы через произвольные точки с обходом препятствий. Controls[0] — это waypoints[0].
        /// </summary>
        bool TryBuildRoute(Vector2[] waypoints, out Vector2[] controls);

        /// <summary>
        /// <para>{EN} False if the point lies inside an obstacle with the route clearance.</para>
        /// {RU} false, если точка внутри препятствия с учётом зазора маршрута.
        /// </summary>
        bool IsWalkable(Vector2 point);
    }
}
