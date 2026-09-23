using UnityEngine;

public class EntityRestart : MonoBehaviour
{
    [SerializeField] private Transform entity;
    [SerializeField] private Transform entityRestartPoint;

    public void RestartEntity()
    {
        if (entity == null || entityRestartPoint == null) return;

        entity.position = entityRestartPoint.position;
        entity.rotation = entityRestartPoint.rotation;
    }
}