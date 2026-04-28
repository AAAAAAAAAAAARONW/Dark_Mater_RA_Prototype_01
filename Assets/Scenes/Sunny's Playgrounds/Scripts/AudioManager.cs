using UnityEngine;
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    [Header("Audio Sources")]
    [SerializeField] AudioSource bgmSource;
    [SerializeField] AudioSource sfxSource;
    [Header("Default Clips")]
    [SerializeField] AudioClip startingBgm;
    [SerializeField] AudioClip darkMatterSfx;

    [Header("BGM Testing")]
    [SerializeField] AudioClip[] testBgms;
    [SerializeField] int currentBgmIndex = 0;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        if (startingBgm != null)
        {
            PlayBgm(startingBgm);
        }
        else if (testBgms != null && testBgms.Length > 0)
        {
            PlayBgm(testBgms[currentBgmIndex]);
        }
    }
    public void PlayBgm(AudioClip newBgm)
    {
        if (newBgm == null) return;
        bgmSource.clip = newBgm;
        bgmSource.loop = true;
        bgmSource.Play();
    }
    public void StopBgm()
    {
        bgmSource.Stop();
    }
    public void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip);
    }
    public void PlayDarkMatterSfx()
    {
        PlaySfx(darkMatterSfx);
    }

    public void NextTestBgm()
    {
        if (testBgms == null || testBgms.Length == 0) return;
        currentBgmIndex++;
        if (currentBgmIndex >= testBgms.Length)
            currentBgmIndex = 0;
        PlayBgm(testBgms[currentBgmIndex]);
    }
    public void PreviousTestBgm()
    {
        if (testBgms == null || testBgms.Length == 0) return;
        currentBgmIndex--;
        if (currentBgmIndex < 0)
            currentBgmIndex = testBgms.Length - 1;
        PlayBgm(testBgms[currentBgmIndex]);
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightBracket))
            NextTestBgm();
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            PreviousTestBgm();
    }
}