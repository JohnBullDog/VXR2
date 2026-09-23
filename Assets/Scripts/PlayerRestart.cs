using UnityEngine;

public class PlayerRestart : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform restartPoint;

    public void RestartPlayer()
    {
        if (player == null || restartPoint == null) return;

        CharacterController controller = player.GetComponent<CharacterController>();

        if (controller != null)
            controller.enabled = false;

        player.position = restartPoint.position;
        player.rotation = restartPoint.rotation;

        if (controller != null)
            controller.enabled = true;
    }
}