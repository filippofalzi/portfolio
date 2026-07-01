using UnityEngine;

public class ArenaLimits : MonoBehaviour
{
    [SerializeField] private Vector3 center;
    [SerializeField] private float arenaWidth = 18f;
    [SerializeField] private float arenaDepth = 28f;

    private CharacterController playerCtrl;
    private RacoonHero player;

    private void Start()
    {
        playerCtrl = GetComponent<CharacterController>();
        player = GetComponent<RacoonHero>();
    }

    private void LateUpdate()
    {
        Vector3 playerPosition = transform.position;

        float leftLimit = center.x - arenaWidth / 2f;
        float rightLimit = center.x + arenaWidth / 2f;
        float backLimit = center.z - arenaDepth / 2f;
        float frontLimit = center.z + arenaDepth / 2f;

        if (playerPosition.x < leftLimit ||
            playerPosition.x > rightLimit ||
            playerPosition.z < backLimit ||
            playerPosition.z > frontLimit)
        {
            playerCtrl.Move(
                -player.motionVector *
                player.currentSpeed *
                Time.fixedDeltaTime
            );
        }
    }
}