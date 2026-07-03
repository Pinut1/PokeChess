//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    public class MoveObject : MonoBehaviour
    {
        
        [SerializeField] private GameObject moveObject;
        [SerializeField] private MoveType moveType;
        [SerializeField][Range(0,2)] private float sensitivity;

        private void Update()
        {
            switch (moveType)
            {
                case MoveType.KeyboardStep:
                    HandleKeyboardStepMovement();
                    break;
                case MoveType.KeyboardContinuous:
                    HandleKeyboardContinuousMovement();
                    break;
                case MoveType.MouseDrag:
                    break;
            }

        }

        private void HandleKeyboardStepMovement()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                moveObject.transform.position += new Vector3(-sensitivity, 0, 0);
            }
            if (Input.GetKeyDown(KeyCode.W))
            {
                moveObject.transform.position += new Vector3(0, sensitivity, 0);
            }
            if (Input.GetKeyDown(KeyCode.D))
            {
                moveObject.transform.position += new Vector3(sensitivity, 0, 0);
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                moveObject.transform.position += new Vector3(0, -sensitivity, 0);
            }
        }

        private void HandleKeyboardContinuousMovement()
        {
            if (Input.GetKey(KeyCode.A))
            {
                moveObject.transform.position += new Vector3(-sensitivity, 0, 0);
            }
            if (Input.GetKey(KeyCode.W))
            {
                moveObject.transform.position += new Vector3(0, sensitivity, 0);
            }
            if (Input.GetKey(KeyCode.D))
            {
                moveObject.transform.position += new Vector3(sensitivity, 0, 0);
            }
            if (Input.GetKey(KeyCode.S))
            {
                moveObject.transform.position += new Vector3(0, -sensitivity, 0);
            }
        }
        
    }

    public enum MoveType
    {
        KeyboardStep,
        KeyboardContinuous,
        MouseDrag
    }
}