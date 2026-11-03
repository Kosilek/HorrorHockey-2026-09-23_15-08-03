using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Core.Match
{
    public partial class MatchController
    {
        public void Tick()
        {
            while (_commands.TryDequeue(out Action command))
                command();

            MoveUnits();
        }

        private void MoveUnits()
        {
            foreach (var unit in _units.Values)
            {
                unit.Tick(_deltaTime);
            }
        }
    }
}
