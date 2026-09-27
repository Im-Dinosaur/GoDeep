using UnityEngine;
using UnityEngine.InputSystem;

namespace GoDeep
{
    public sealed class PlayerInputComponent : MonoBehaviour
    {
        public struct Frame
        {
            public Vector2 movement; //수평 수영 입력
            public Vector2 look; //마우스 시점 입력
            public float vertical; //상승과 하강 입력
            public bool fast; //빠른 수영 유지
            public bool watch; //워치 확인 유지
            public bool selfUse; //자신에게 사용한 순간
            public bool otherUse; //동료에게 사용한 순간
            public bool switchHand; //빈손과 아이템 전환
            public bool interact; //장치 상호작용
            public bool pause; //일시정지 전환
            public bool restart; //시제품 재시작
            public int signal; //선택한 워치 신호
        }

        public Frame readInput() //현재 프레임의 키보드와 마우스 입력 반환
        {
            var frame = new Frame(); //이번 프레임 입력
            var keyboard = Keyboard.current; //현재 키보드
            var mouse = Mouse.current; //현재 마우스
            if (keyboard != null)
            {
                frame.movement = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                frame.vertical = (keyboard.spaceKey.isPressed ? 1 : 0) - (keyboard.leftCtrlKey.isPressed ? 1 : 0);
                frame.fast = keyboard.leftShiftKey.isPressed;
                frame.switchHand = keyboard.qKey.wasPressedThisFrame;
                frame.interact = keyboard.eKey.wasPressedThisFrame;
                frame.pause = keyboard.escapeKey.wasPressedThisFrame;
                frame.restart = keyboard.rKey.wasPressedThisFrame;
                frame.signal = keyboard.digit1Key.wasPressedThisFrame ? 1 : keyboard.digit2Key.wasPressedThisFrame ? 2 :
                    keyboard.digit3Key.wasPressedThisFrame ? 3 : keyboard.digit4Key.wasPressedThisFrame ? 4 : 0;
            }
            if (mouse != null)
            {
                frame.look = mouse.delta.ReadValue();
                frame.watch = mouse.rightButton.isPressed;
                frame.selfUse = mouse.leftButton.wasPressedThisFrame;
                frame.otherUse = mouse.rightButton.wasPressedThisFrame;
            }
            return frame;
        }
    }
}
