using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the menu's "Exit" animation, then loads <see cref="nextSceneName"/>.
/// <see cref="GoToNextScene"/> is meant for a UI Button's OnClick.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    [SerializeField] private Animator menuAnimator;
    [Tooltip("Length of the exit animation, in seconds.")]
    [SerializeField] private float exitAnimationSeconds = 0.5f;
    [SerializeField] private string nextSceneName;

    private bool isTransitioning;

    public void GoToNextScene()
    {
        if (isTransitioning) return; // repeated clicks would load the scene twice
        isTransitioning = true;
        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        menuAnimator.SetTrigger("Exit");
        yield return new WaitForSeconds(exitAnimationSeconds);
        SceneManager.LoadScene(nextSceneName);
    }
}
