using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitioner : Singleton<SceneTransitioner>
{
    public bool LevelFinished { get; private set; } = false;

    [SerializeField] private Animator anim;

    public const int MAIN_MENU_INDEX = 1;

    public const float FADE_ANIM_DURATION = 0.25f;


    public static int CurrBuildIndex = 0;

    public static bool IsFading { get; private set; } = false;

    private void Start()
    {
        ToMainMenu();
    }

    public void ToMainMenu()
    {
        LoadSceneWithIndex(MAIN_MENU_INDEX);
    }

    public void LoadSceneWithIndex(int _index)
    {
        StartCoroutine(LoadSceneRoutine(_index));
    }

    private IEnumerator LoadSceneRoutine(int _index)
    {
        if (IsFading)
        {
            Debug.LogWarning("Already fading!");
            yield break;
        }

        IsFading = true;

        anim.ResetTrigger("ToClear");
        anim.SetTrigger("ToBlack");

        yield return new WaitForSeconds(FADE_ANIM_DURATION);

        LevelFinished = false;
        SceneManager.LoadScene(_index);
    }

    public void OnSceneFinishedLoading()
    {
        CurrBuildIndex = SceneManager.GetActiveScene().buildIndex;

        anim.ResetTrigger("ToBlack");
        anim.SetTrigger("ToClear");

        StartCoroutine(AfterFadeInAnimationDone());
    }

    private IEnumerator AfterFadeInAnimationDone()
    {
        if (!IsFading)
            yield break;

        yield return new WaitForSeconds(FADE_ANIM_DURATION);

        IsFading = false;
    }
}
