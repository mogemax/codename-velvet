using System.Collections;
using CodenameVelvet.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the menu's "Exit" animation (optionally the menu background's shipwreck after it), then loads
/// <see cref="nextSceneName"/>.
/// <see cref="GoToNextScene"/> is meant for a UI Button's OnClick.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    [SerializeField] private Animator menuAnimator;
    [Tooltip("Length of the exit animation, in seconds.")]
    [SerializeField] private float exitAnimationSeconds = 0.5f;
    [SerializeField] private string nextSceneName;
    [Tooltip("Shown while the next scene loads. Empty = straight cut.")]
    [SerializeField] private LoadingScreen loadingScreen;
    [Tooltip("Before loading, the menu background's boat is wrecked (on the way to the first level). Cannot be skipped.")]
    [SerializeField] private bool shipwreckFirst;

    private bool isTransitioning;

    // The loading screen's video gets ready while the menu is up.
    private void Start() => LoadingScreen.Preload(loadingScreen);

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
        if (shipwreckFirst && MenuBackground.Current != null && MenuBackground.Current.TryGetComponent(out Shipwreck shipwreck))
            yield return shipwreck.Play();
        if (loadingScreen != null) LoadingScreen.Load(loadingScreen, nextSceneName);
        else SceneManager.LoadScene(nextSceneName);
    }
}
