using UnityEngine;
namespace RainyApartmentVR
{
    // 触摸只在待机时接管姿态，保留动画、牵手和全身动作的优先权。
    public static class CharacterPoseGate
    {
        public static bool CanTouch(GameObject character)
        {
            var behaviour = character.GetComponent<CharacterBehaviour>();
            var prayer = character.GetComponent<CharacterPrayer>();
            var actions = character.GetComponent<CharacterActionLibrary>();
            var hug = character.GetComponent<CharacterInteraction>();
            if (hug && hug.IsHugging) return false;
            if ((prayer && prayer.IsActive) || (actions && actions.IsPlayingClip) ||
                (behaviour && (behaviour.IsWalking || behaviour.IsPostureLocked || behaviour.IsHandHeld))) return false;
            var animator = behaviour ? behaviour.animator : character.GetComponentInChildren<Animator>();
            return animator && !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle");
        }
    }
}
