using UnityEngine;

public class ForcefieldVisual : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float showDistance = 3f;
    [SerializeField] private float fadeSpeed = 3f;

    private SpriteRenderer sr;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        float targetAlpha = dist < showDistance ? 1f : 0f;

        Color c = sr.color;
        c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
        sr.color = c;
    }
}