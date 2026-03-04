using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class DoorSceneLoader : MonoBehaviour
{
    [SerializeField] private string sceneName;
    [SerializeField] private bool isExitDoor;
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        if (!isExitDoor) animator.SetBool("IsOpened", true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (isExitDoor) {
            animator.SetBool("IsOpened", true);
            StartCoroutine(Transition(other.gameObject));
            }
            else{
                animator.SetBool("IsOpened", false);
            }
        }
    }
    private IEnumerator Transition(GameObject player)
    {
        player.SetActive(false);
        yield return new WaitForSeconds(0.5f);
        animator.SetBool("IsOpened", false);
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene(sceneName);
    }

}