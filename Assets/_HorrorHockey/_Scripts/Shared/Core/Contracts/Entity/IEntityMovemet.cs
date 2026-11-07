using FM.Mathematics.DataTypes;

namespace Shared.Core.Contracts
{
    public interface IEntityMovemet
    {
        /// <summary>
        /// <para>{EN} Route from the body's current position through the points, without snapping.</para>
        /// {RU} Маршрут от текущей позиции тела через список точек, без телепорта.
        /// </summary>
        void StartMovement(Vector2[] points);

        /// <summary>
        /// <para>{EN} Route from the server: the body snaps to start and follows the same curve as the server unit.</para>
        /// {RU} Маршрут с сервера: тело встаёт в start и идёт по той же кривой, что и серверный юнит.
        /// </summary>
        void StartMovement(Vector2 start, Vector2[] controls);

        /// <summary>
        /// <para>{EN} Server route without braking. The body snaps to start, enters at startSpeed and keeps its speed on the last point.</para>
        /// {RU} Маршрут с сервера без торможения. Тело встаёт в start, входит со скоростью startSpeed и на последней точке скорость не гасит.
        /// </summary>
        void StartMovingRoute(Vector2 start, Vector2[] controls, float startSpeed);
    }
}
