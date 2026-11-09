using ContractsServer.Data.Navigation;
using System.Collections.Generic;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace ContractsServer.Core.Interfaces
{
    public interface INavigationProviderService
    {
        BasicRouteData BasicRouteData { get; }
        IReadOnlyList<NavigationObstacle> Obstacles { get; }

        /// <summary>
        /// <para>{EN} Lane controls with obstacle bypass, blue direction. Server and client both move by these.</para>
        /// {RU} Контролы полос с обходом препятствий, направление синих. По ним ходят и сервер, и клиент.
        /// </summary>
        BasicRoutePositionData BlueRoutes { get; }

        /// <summary>
        /// <para>{EN} Same lanes reversed for red. The bypass is built for this direction separately.</para>
        /// {RU} Те же полосы в обратную сторону для красных. Обход строится для этого направления отдельно.
        /// </summary>
        BasicRoutePositionData RedRoutes { get; }

        /// <summary>
        /// <para>{EN} Controls through arbitrary waypoints with obstacle bypass. Thread safe. Send start and these controls to the client, it builds the same curve.</para>
        /// {RU} Контролы через произвольные точки с обходом препятствий. Потокобезопасно. Старт и эти контролы шлются клиенту — он строит ту же кривую.
        /// </summary>
        FMVector2[] BuildRoute(FMVector2[] waypoints);

        /// <inheritdoc cref="BuildRoute(FMVector2[])"/>
        FMVector2[] BuildRoute(FMVector2 from, FMVector2 to);

        /// <summary>
        /// <para>{EN} False if the point lies inside an obstacle with the route clearance. Thread safe.</para>
        /// {RU} false, если точка внутри препятствия с учётом зазора маршрута. Потокобезопасно.
        /// </summary>
        bool IsWalkable(FMVector2 point);
    }
}