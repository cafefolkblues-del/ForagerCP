using UnityEngine;

namespace ForagerCP
{
    /// F키 상호작용 대상(변환기·상점). 태그 문자열이 아니라 컴포넌트로 판별한다.
    public interface IInteractable
    {
        Transform Transform { get; }
        string Label { get; }
        void Interact();
    }
}
