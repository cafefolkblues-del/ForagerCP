using UnityEngine;
using UnityEngine.InputSystem;

namespace ForagerCP
{
    /// 사양 1-11 테스트용 치트. 정식 UI 없이 F1~F5로 처리한다.
    /// 에디터 스크립트가 아니라 런타임 컴포넌트라 빌드에서도 그대로 동작한다.
    public class DebugCheats : MonoBehaviour
    {
        [SerializeField] GoldWallet _gold;
        [SerializeField] PlayerLevel _level;
        [SerializeField] Transform _player;
        [SerializeField] Transform _spawnPoint;

        [SerializeField] int _goldStep = 100;
        [SerializeField] int _expStep = 10;
        [SerializeField] int _levelStep = 1;

        MineralNode[] _nodes;

        void Awake() => _nodes = FindObjectsByType<MineralNode>(FindObjectsSortMode.None);

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) _gold.Add(_goldStep);
            if (keyboard.f2Key.wasPressedThisFrame) _level.AddExp(_expStep);
            if (keyboard.f3Key.wasPressedThisFrame) _level.DebugAddLevel(_levelStep);
            if (keyboard.f4Key.wasPressedThisFrame) RespawnAllNodes();
            if (keyboard.f5Key.wasPressedThisFrame) ResetPlayerPosition();
        }

        void RespawnAllNodes()
        {
            for (int i = 0; i < _nodes.Length; i++) _nodes[i].Respawn();
        }

        void ResetPlayerPosition()
        {
            if (_player == null || _spawnPoint == null) return;

            // 이동 중 관성이 남아 스폰 지점에서 미끄러지는 것을 막으려고 속도까지 죽인다.
            if (_player.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            _player.position = _spawnPoint.position;
        }
    }
}
