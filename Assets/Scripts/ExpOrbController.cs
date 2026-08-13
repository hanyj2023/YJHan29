using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExpOrbController : MonoBehaviour
{
    private const float MoveSpeed = 10f;
    private const float CollectDistance = 0.1f;

    [Header("Experience Orb")]
    [SerializeField, Min(1)]
    private int expValue = 1;

    private bool isCollected;

    public int ExpValue => expValue;

    private void Update()
    {
        if (isCollected || LevelUpPanelController.IsPaused)
        {
            return;
        }

        ExpDropManager manager = ExpDropManager.Instance;
        if (manager == null || !manager.isActiveAndEnabled)
        {
            return;
        }

        Vector3 playerPosition = manager.transform.position;
        float distance = Vector2.Distance(transform.position, playerPosition);
        if (distance > manager.MagnetRange)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            playerPosition,
            MoveSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, playerPosition) <= CollectDistance)
        {
            Collect(manager);
        }
    }

    private void Collect(ExpDropManager manager)
    {
        if (isCollected)
        {
            return;
        }

        isCollected = true;
        manager.AddExperience(expValue);
        Destroy(gameObject);
    }

    private void OnValidate()
    {
        expValue = Mathf.Max(1, expValue);
    }
}
