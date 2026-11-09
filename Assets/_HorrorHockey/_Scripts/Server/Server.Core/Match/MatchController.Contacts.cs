using FM.Mathematics.DataTypes;
using Server.Core.Entity;
using System.Buffers;

namespace Server.Core.Match
{
    public partial class MatchController
    {
        /// <summary>
        /// <para>{EN} Which pairs collide. The single place to filter, e.g. allies.</para>
        /// {RU} Какие пары сталкиваются. Единственное место для фильтра, например союзников.
        /// </summary>
        private static bool CanCollide(UnitRuntime a, UnitRuntime b)
        {
            return !a.IsDead && !b.IsDead;
        }

        // После движения: каждой паре, где хоть кто-то бежит, ищем касание за следующий тик. Клиент едет непрерывно и к приходу
        // остановки уже проехал бы весь тик, поэтому касание планируется заранее и маршрут обрезается точкой касания.
        // Шанс на падение бросает тот, в кого врезался бегущий: оба бегут — бросают оба. Если никто не бежит, касание ничего не значит.
        private void FindContacts()
        {
            foreach (UnitRuntime unit in _units.Values)
            {
                unit.ReleaseContactPartner();
                // Новое касание посреди круга перезапустило бы его с середины дуги или остановило юнита.
                if (!unit.IsBodyMoving || unit.IsCircling)
                    continue;

                (int[] ids, int count) = _worldGrid.GetNeighborUnitIds(unit.CurrentCell);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (ids[i] == unit.Id || !_units.TryGetValue(ids[i], out UnitRuntime other) || other.IsCircling)
                            continue;

                        // Пару из двух едущих проверяет только юнит с меньшим Id.
                        if (other.IsBodyMoving && other.Id < unit.Id)
                            continue;

                        // Исход этой пары уже выбран, пока тела не разошлись.
                        if (unit.HasContactPartner(other) || other.HasContactPartner(unit))
                            continue;

                        bool unitCharging = unit.IsCharging;
                        bool otherCharging = other.IsCharging;
                        if (!unitCharging && !otherCharging)
                            continue;

                        if (!CanCollide(unit, other)
                            || !unit.TryFindHit(other, _deltaTime, out float time, out Vector2 unitCenter, out Vector2 otherCenter))
                            continue;

                        unit.PlanContact(other, time, rollFall: otherCharging, unitCenter);
                        other.PlanContact(unit, time, rollFall: unitCharging, otherCenter);
                    }
                }
                finally
                {
                    ArrayPool<int>.Shared.Return(ids);
                }
            }
        }

        private void ResolveContacts()
        {
            foreach (UnitRuntime unit in _units.Values)
                unit.ResolveContact();
        }
    }
}
