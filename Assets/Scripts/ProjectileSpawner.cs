using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _projectilePrefab = null;
    [SerializeField] private float _spawnRate = 1f;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(SpawnProjectiles());
    }

    IEnumerator SpawnProjectiles()
    {
        while (true)
        {
            Instantiate(_projectilePrefab, transform.position, Quaternion.identity);
            yield return new WaitForSeconds(_spawnRate);
        }
    }
}
