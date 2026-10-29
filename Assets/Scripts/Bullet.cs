using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float damage = 10f;
    public float speed = 25f;
    public Transform target;
    public Vector3 targetLastPos;
    public bool isSniper = false;

    private float lifeTime = 2.5f;

    public void Initialize(Transform targetTransform, float dmg, float spd, bool sniper)
    {
        target = targetTransform;
        damage = dmg;
        speed = spd;
        isSniper = sniper;
        if (target != null)
        {
            targetLastPos = target.position + Vector3.up * 0.4f;
        }
        else
        {
            targetLastPos = transform.position + transform.forward * 10f;
        }
    }

    void Update()
    {
        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 dest = (target != null) ? (target.position + Vector3.up * 0.4f) : targetLastPos;
        Vector3 toDest = dest - transform.position;
        float dist = toDest.magnitude;

        // When reaching impact destination
        if (dist <= speed * Time.deltaTime || dist <= 0.35f)
        {
            OnHit();
            return;
        }

        Vector3 dir = toDest.normalized;
        transform.position += dir * speed * Time.deltaTime;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    void OnHit()
    {
        if (target != null)
        {
            Zombie z = target.GetComponent<Zombie>();
            if (z != null)
            {
                z.TakeDamage(damage);
            }
        }
        Destroy(gameObject);
    }
}
