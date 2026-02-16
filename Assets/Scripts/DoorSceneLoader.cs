using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEditor.UI;


public class DoorSceneLoader : MonoBehaviour
{
    [SerializeField] private string sceneName;
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            animator.SetBool("IsOpened", true);
            StartCoroutine(Transition(other.gameObject));
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
