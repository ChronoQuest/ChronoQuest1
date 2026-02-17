using UnityEngine;

public class UIFollowPlayer : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f); 

    private RectTransform rectTransform; 
    private Canvas canvas; 

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>(); 
        canvas = GetComponentInParent<Canvas>();
    }

    private void LateUpdate()
    {
        if (target == null || canvas == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(
            target.position + worldOffset
        );

        if (screenPos.z < 0)
        {
            rectTransform.gameObject.SetActive(false);
            return;
        }

        rectTransform.gameObject.SetActive(true);
        rectTransform.position = screenPos;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
