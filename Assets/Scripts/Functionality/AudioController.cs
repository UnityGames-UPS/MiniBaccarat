using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
internal class AudioController : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource gameSoundSource;
    // [SerializeField] private AudioSource uiSource;

    [Header("Background")]
    [SerializeField] private AudioClip bgMusic;

    [Header("Game Sounds")]
    [SerializeField] private AudioClip gameTie;
    [SerializeField] private AudioClip playerWins;
    [SerializeField] private AudioClip bankerWins;

    [Header("UI Sounds")]
    [SerializeField] private AudioClip uiButton;
    [SerializeField] private AudioClip chipSound;
    [SerializeField] private AudioClip cardPlaced;
    // [SerializeField] private AudioClip navigation;

    [Header("Sound Buttons")]
    [SerializeField] private Button SoundButton;
    [SerializeField] private Button MusicButton;

    private bool isGameMuted = false;
    private bool isMusicMuted = false;

    private void Start()
    {
        if (SoundButton)
        {
            SoundButton.onClick.RemoveAllListeners();
            SoundButton.onClick.AddListener(ToggleGameSound);
        }

        if (MusicButton)
        {
            MusicButton.onClick.RemoveAllListeners();
            MusicButton.onClick.AddListener(ToggleBackgroundMusic);
        }

        PlayBackground();
    }

    private void ToggleGameSound()
    {
        isGameMuted = !isGameMuted;
        MuteGame(isGameMuted);
    }

    private void ToggleBackgroundMusic()
    {
        isMusicMuted = !isMusicMuted;
        MuteBackground(isMusicMuted);
    }


    internal void PlayBackground()
    {
        if (!bgMusic) return;

        bgMusicSource.clip = bgMusic;
        bgMusicSource.loop = true;
        if (!bgMusicSource.isPlaying)
            bgMusicSource.Play();
    }

    internal void StopBackground()
    {
        bgMusicSource.Stop();
    }

    internal void PlayGameTie()
    {
        PlayGame(gameTie, false);
    }

    internal void PlayPlayerWins()
    {
        PlayGame(playerWins, false);
    }

    internal void PlayBankerWins()
    {
        PlayGame(bankerWins, false);
    }

    private void PlayGame(AudioClip clip, bool loop)
    {
        if (!clip) return;

        gameSoundSource.Stop();
        gameSoundSource.clip = clip;
        gameSoundSource.loop = loop;
        gameSoundSource.Play();
    }

    internal void StopGameAudio()
    {
        gameSoundSource.Stop();
        gameSoundSource.loop = false;
    }

    internal void PlayChip()
    {
        gameSoundSource.PlayOneShot(chipSound);
    }

    internal void PlayCardPlaced()
    {
        gameSoundSource.PlayOneShot(cardPlaced);
    }

    internal void PlayUIButton()
    {
        gameSoundSource.PlayOneShot(uiButton);
    }

    // internal void PlayNavigation()
    // {
    //     uiSource.PlayOneShot(navigation);
    // }

    internal void MuteAll(bool mute)
    {
        bgMusicSource.mute = mute;
        gameSoundSource.mute = mute;
        // uiSource.mute = mute;
    }


    internal void MuteBackground(bool mute) => bgMusicSource.mute = mute;
    internal void MuteGame(bool mute) => gameSoundSource.mute = mute;
    // internal void MuteUI(bool mute) => uiSource.mute = mute;
}
