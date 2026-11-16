using FM.Mathematics.DataTypes;
using Server.Core.Entity;
using System.Buffers;

namespace Server.Core.Match
{
    public partial class MatchController
    {
        /// <summary>
        /// <para>{EN} Which pairs collide. The single place to filter. Allies never collide.</para>
        /// {RU} Какие пары сталкиваются. Единственное место для фильтра. Союзники не сталкиваются.
        /// </summary>
        private static bool CanCollide(UnitRuntime a, UnitRuntime b)
        {
            return !a.IsDead && !b.IsDead && a.Team != b.Team;
        }

        // После движения: каждой паре, где хоть кто-то бежит, ищем касание за следующий тик. Клиент едет непрерывно и к приходу
        // остановки уже проехал бы весь тик, поэтому касание планируется заранее и маршрут обрезается точкой касания.
        // Бьёт только бегущий, который ещё не в драке. Шанс на падение бросает тот, в кого врезались, если он не лежит:
        // оба бьют — бросают оба. Юнит в драке (после контакта, лёжа, на круге) сам не бьёт, но в него врезаться можно.
        private void FindContacts()
        {
            foreach (UnitRuntime unit in _units.Values)
            {
                unit.ReleaseContactPartner();
                // Кружащий сам касаний не ищет: в него врезается второй, и эту пару проверяет он.
                if (!unit.IsBodyMoving || unit.IsCircling)
                    continue;

                (int[] ids, int count) = _worldGrid.GetNeighborUnitIds(unit.CurrentCell);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (ids[i] == unit.Id || !_units.TryGetValue(ids[i], out UnitRuntime other))
                            continue;

                        // Пару из двух едущих не по кругу проверяет только юнит с меньшим Id.
                        if (other.IsBodyMoving && !other.IsCircling && other.Id < unit.Id)
                            continue;

                        // Исход этой пары уже выбран, пока тела не разошлись.
                        if (unit.HasContactPartner(other) || other.HasContactPartner(unit))
                            continue;

                        bool unitHits = unit.IsCharging && !unit.IsInFight;
                        bool otherHits = other.IsCharging && !other.IsInFight;
                        if (!unitHits && !otherHits)
                            continue;

                        if (!CanCollide(unit, other)
                            || !unit.TryFindHit(other, _deltaTime, out float time, out Vector2 unitCenter, out Vector2 otherCenter))
                            continue;

                        unit.PlanContact(other, time, rollFall: otherHits && !unit.IsStunned, unitCenter);
                        other.PlanContact(unit, time, rollFall: unitHits && !other.IsStunned, otherCenter);
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
