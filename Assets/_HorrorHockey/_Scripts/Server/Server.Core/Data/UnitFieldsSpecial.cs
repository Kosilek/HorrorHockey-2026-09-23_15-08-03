using FM.Mathematics.DataTypes;
using Server.Core.Entity;
using Server.Core.Interfaces;
using Server.Core.Targets;
using System;
using System.Collections.Generic;

namespace Server.Core.Data
{
    public class UnitFieldsSpecial
    {
        public Action<string, object> SendCommand { get; set; }
        public TargetSelector TargetSelector { get; set; }
        public INavigationService NavigationService { get; set; }

        /// <summary>
        /// <para>{EN} Units whose current target is the unit with this id. Null if nobody.</para>
        /// {RU} Юниты, чья текущая цель — юнит с этим id. null, если таких нет.
        /// </summary>
        public Func<int, List<UnitRuntime>> GetUnitsByTarget { get; set; }

        /// <summary>
        /// <para>{EN} Fills the list with the units of this grid cell and the cells around it. The list is cleared first.</para>
        /// {RU} Заполняет список юнитами этой клетки сетки и клеток вокруг неё. Список сначала очищается.
        /// </summary>
        public Action<Int2, List<UnitRuntime>> FillNearbyUnits { get; set; }

        /// <summary>
        /// <para>{EN} Unit, previous target, new target. Either target may be null.</para>
        /// {RU} Юнит, прежняя цель, новая цель. Любая цель может быть null.
        /// </summary>
        public Action<UnitRuntime, UnitRuntime, UnitRuntime> TargetChanged { get; set; }
        public Action<int> CallDeath { get; set; }
    }
}
