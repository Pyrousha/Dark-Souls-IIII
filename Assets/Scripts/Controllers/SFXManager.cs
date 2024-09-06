using AYellowpaper.SerializedCollections;
using UnityEngine;

public class SFXManager : Singleton<SFXManager>
{
    [System.Serializable]
    public struct SFXReference
    {
        public AudioClip Clip;
        [Range(0f, 1f)] public float VolumeMultiplier;
    }

    public enum AudioTypeEnum
    {
        Player_OnHit,
        Player_Parry,
        Enemy_Swing
    }

    [SerializeField] private AudioSource source;
    [SerializeField] private SerializedDictionary<AudioTypeEnum, SFXReference> clips;

    public void Play(AudioTypeEnum type)
    {
        SFXReference clipToPlay = clips[type];

        //source.volume = SaveData.CurrSaveData.SfxVol * clipToPlay.VolumeMultiplier;
        source.volume = clipToPlay.VolumeMultiplier;
        source.PlayOneShot(clipToPlay.Clip);
    }
}
