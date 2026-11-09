using _FM.DI;
using ContractsServer.Command;
using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using Shared.Core.Contracts.Server;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityLife.Service
{
    public class GameService : MonoBehaviour
    {
        [Inject]
        private INetworkService _networkService;

        [SerializeField] private Team team;
        [SerializeField] private PlayerIndex playerIndex;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(1f);
            UnityInjectService.Instance.Inject(this);
        }

        void Update()
        {
            if (_networkService == null)
                return;

            _networkService.Dispatch();

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                _networkService.Send(CommandWs.StartMatch, null);
            }
            if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                _networkService.Send(CommandWs.SpawnUnit, (new Byte2(0, 0), Team.Blue, PlayerIndex.BlueLeft));
            }
            if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                _networkService.Send(CommandWs.SpawnUnit, (new Byte2(0, 0), Team.Blue, PlayerIndex.BlueMiddle));
            }
            if (Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                _networkService.Send(CommandWs.SpawnUnit, (new Byte2(0, 0), Team.Blue, PlayerIndex.BlueRight));
            }
            if (Keyboard.current != null && Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                _networkService.Send(CommandWs.SpawnUnit, (new Byte2(0, 0), Team.Red, PlayerIndex.RedLeft));
            }
            if (Keyboard.current != null && Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                _networkService.Send(CommandWs.SpawnUnit, (new Byte2(0, 0), Team.Red, PlayerIndex.RedMiddle));
            }
            if (Keyboard.current != null && Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                _networkService.Send(CommandWs.SpawnUnit, (new Byte2(0, 0), Team.Red, PlayerIndex.RedRight));
            }
        }
    }
}