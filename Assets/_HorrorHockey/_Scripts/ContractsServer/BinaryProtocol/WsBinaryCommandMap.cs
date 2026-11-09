using ContractsServer.Command;
using System.Collections.Generic;

namespace ContractsServer.BinaryProtocol
{
    public static class WsBinaryCommandMap
    {
        private static readonly Dictionary<string, BinaryMessageType> CommandToType = new Dictionary<string, BinaryMessageType>
        {
            [CommandWs.StartMatch] = BinaryMessageType.StartMatch,
            [CommandWs.CreateRoute] = BinaryMessageType.CreateRoute,
            [CommandWs.SlowUnit] = BinaryMessageType.SlowUnit,
            [CommandWs.StunUnit] = BinaryMessageType.StunUnit,
            [CommandWs.UnstunUnit] = BinaryMessageType.UnstunUnit,
            [CommandWs.StopUnit] = BinaryMessageType.StopUnit,
            [CommandWs.StartAnimationClip] = BinaryMessageType.StartAnimationClip,
        };

        private static readonly Dictionary<BinaryMessageType, string> TypeToCommand = BuildTypeToCommand();

        public static bool TryGetMessageType(string command, out BinaryMessageType messageType) =>
            CommandToType.TryGetValue(command, out messageType);

        public static bool TryGetCommand(BinaryMessageType messageType, out string command)
        {
            if (messageType == BinaryMessageType.SpawnUnitRequest || messageType == BinaryMessageType.SpawnUnit)
            {
                command = CommandWs.SpawnUnit;
                return true;
            }

            return TypeToCommand.TryGetValue(messageType, out command);
        }

        private static Dictionary<BinaryMessageType, string> BuildTypeToCommand()
        {
            var reverse = new Dictionary<BinaryMessageType, string>(CommandToType.Count);
            foreach (KeyValuePair<string, BinaryMessageType> pair in CommandToType)
            {
                reverse[pair.Value] = pair.Key;
            }

            return reverse;
        }
    }
}
