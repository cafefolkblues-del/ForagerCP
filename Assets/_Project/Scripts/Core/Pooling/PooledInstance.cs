using UnityEngine;

namespace ForagerCP
{
    /// 풀이 만들어낸 인스턴스에 자동으로 붙는 꼬리표.
    /// "이 오브젝트가 어느 풀 소속인지"를 들고 있어야 아무 데서나 Despawn을 호출할 수 있다.
    [DisallowMultipleComponent]
    public class PooledInstance : MonoBehaviour
    {
        public GameObjectPool Pool { get; private set; }

        /// 지금 꺼내져 쓰이는 중인지. 같은 오브젝트를 두 번 반납하는 사고를 막는 데 쓴다.
        public bool IsInUse { get; private set; }

        public void Bind(GameObjectPool pool) => Pool = pool;

        public void MarkInUse(bool inUse) => IsInUse = inUse;

        /// 스스로 풀에 돌아간다. 호출하는 쪽이 풀을 몰라도 되게 하는 통로.
        public void Release()
        {
            if (Pool == null)
            {
                Destroy(gameObject);
                return;
            }

            Pool.Release(gameObject);
        }
    }
}
