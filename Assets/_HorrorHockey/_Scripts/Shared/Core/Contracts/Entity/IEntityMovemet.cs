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
        /// <para>{EN} Route from the server: the same curve as the server unit from start. A body standing elsewhere slides onto it smoothly.</para>
        /// {RU} Маршрут с сервера: та же кривая от start, что у серверного юнита. Тело, стоящее в другом месте, плавно съезжает на неё.
        /// </summary>
        void StartMovement(Vector2 start, Vector2[] controls);

        /// <summary>
        /// <para>{EN} Server route without braking. Slides onto the curve smoothly, enters at startSpeed and keeps its speed on the last point.</para>
        /// {RU} Маршрут с сервера без торможения. Плавно съезжает на кривую, входит со скоростью startSpeed и на последней точке скорость не гасит.
        /// </summary>
        void StartMovingRoute(Vector2 start, Vector2[] controls, float startSpeed);

        /// <summary>
        /// <para>{EN} Speed from the server slow. It climbs back to max over two accel times.</para>
        /// {RU} Скорость после замедления с сервера. Возвращается к максимуму за два времени разгона.
        /// </summary>
        void SlowTo(float speed);

        /// <summary>
        /// <para>{EN} Stops at once and keeps the route.</para>
        /// {RU} Сразу останавливается и сохраняет маршрут.
        /// </summary>
        void PauseMovement();

        /// <summary>
        /// <para>{EN} Continues the kept route with the usual accel.</para>
        /// {RU} Продолжает сохранённый маршрут обычным разгоном.
        /// </summary>
        void ResumeMovement();

        /// <summary>
        /// <para>{EN} Drops the route. The body stays where it is.</para>
        /// {RU} Сбрасывает маршрут. Тело остаётся на месте.
        /// </summary>
        void StopMovement();

        /// <summary>
        /// <para>{EN} Starts the animation clip sent by the server. The index is the clip in the animator controller.</para>
        /// {RU} Запускает клип анимации, присланный сервером. Индекс — клип в контроллере аниматора.
        /// </summary>
        void StartAnimationClip(byte animationClip);
    }
}
