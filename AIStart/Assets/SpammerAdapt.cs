using UnityEngine;

public class SpammerAdapt : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform player;

    [Header("Configurações de Spawn")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float spawnRadius = 5f;

    [Header("Spawn Adaptativo")]
    [SerializeField] private int maxEnemies = 20;
    [SerializeField] private float minSpawnInterval = 0.5f;
    [SerializeField] private float maxSpawnInterval = 3f;

    private float timer;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }
    }

    void Update()
    {
        if (enemyPrefab == null || player == null)
            return;

        timer += Time.deltaTime;

        float adaptiveInterval = GetAdaptiveSpawnInterval();

        if (timer >= adaptiveInterval)
        {
            if (GetEnemyCount() < maxEnemies)
            {
                SpawnEnemy();
            }

            timer = 0f;
        }
    }

    private void SpawnEnemy()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);

        Vector2 offset = new Vector2(
            Mathf.Cos(angle),
            Mathf.Sin(angle)
        ) * spawnRadius;

        Vector3 spawnPosition = player.position +
            new Vector3(offset.x, 0f, offset.y);

        Instantiate(
            enemyPrefab,
            spawnPosition,
            Quaternion.identity
        );
    }

    private int GetEnemyCount()
    {
        return GameObject.FindGameObjectsWithTag("Enemy").Length;
    }

    private float GetAdaptiveSpawnInterval()
    {
        int enemyCount = GetEnemyCount();

        float percentage = Mathf.Clamp01(
            (float)enemyCount / maxEnemies
        );

        return Mathf.Lerp(
            minSpawnInterval,
            maxSpawnInterval,
            percentage
        );
    }
}