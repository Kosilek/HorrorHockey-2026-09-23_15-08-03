using Server.Core.Targets;
using System;

namespace Server.Core.Data
{
    public class UnitFieldsSpecial
    {
        public Action<string, object> SendCommand { get; set; }
        public TargetSelector TargetSelector { get; set; }
    }
}
