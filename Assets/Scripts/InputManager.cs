using UnityEngine;

public class InputManager : MonoBehaviour
{
    public GridNavigator player;

    void Update()
    {
        if (player == null) return;

        if (Input.GetKeyDown(KeyCode.W)) player.MoveInDirection(Vector3.forward);
        if (Input.GetKeyDown(KeyCode.S)) player.MoveInDirection(Vector3.back);
        if (Input.GetKeyDown(KeyCode.A)) player.MoveInDirection(Vector3.left);
        if (Input.GetKeyDown(KeyCode.D)) player.MoveInDirection(Vector3.right);
    }
}