namespace ForagerCP
{
    /// 풀에서 꺼내지고 되돌아갈 때 알림을 받고 싶은 컴포넌트가 구현한다.
    /// 풀은 오브젝트를 파괴하지 않고 재사용하므로, Awake/OnDestroy 대신 이 두 지점에서 상태를 초기화해야 한다.
    public interface IPooled
    {
        /// 꺼내진 직후. 이전 사용 때의 상태(체력, 타이머, 연출 등)를 여기서 되돌린다.
        void OnSpawnedFromPool();

        /// 되돌아가기 직전. 구독 해제나 코루틴 정리를 여기서 한다.
        void OnReturnedToPool();
    }
}
