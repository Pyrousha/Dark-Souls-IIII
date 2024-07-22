using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SaveData : Singleton<SaveData>
{
    public static SerializedSaveData CurrSaveData;

    public const string SAVE_KEY = "SaveData";
    public const int NUM_TOTAL_LEVELS = 12;

    [field: SerializeField] public string VersionNumText { get; private set; } = "Version 20xx";

    public void Start()
    {
        LoadSaveData();
    }

    public void LoadSaveData()
    {
        if (PlayerPrefs.HasKey(SAVE_KEY))
        {
            try
            {
                string saveJson = PlayerPrefs.GetString(SAVE_KEY);

                Debug.Log("Loaded Save Data:\n" + saveJson);

                CurrSaveData = JsonUtility.FromJson<SerializedSaveData>(saveJson);
            }
            catch (Exception)
            {
                if (CurrSaveData == null)
                {
                    Debug.LogError("Error when loading save data, try again?");
                }
                else
                {
                    SerializedSaveData newSave = new SerializedSaveData();
                    if (newSave.Version != CurrSaveData.Version)
                    {
                        Debug.LogError("Incompatible old save data, deleting...");
                        CurrSaveData = newSave;
                        //Save();
                    }
                }
            }
        }
        else
        {
            //Load default data and save
            CurrSaveData = new SerializedSaveData();
            Save();
        }
    }

    public void Save()
    {
        string saveJson = JsonUtility.ToJson(CurrSaveData);
        PlayerPrefs.SetString(SAVE_KEY, saveJson);
        PlayerPrefs.Save();

        Debug.Log("Saved Save Data:\nLength: " + saveJson.Length + "\n" + saveJson);
    }

    #region Debug Functions
    public void ResetSaveData()
    {
        CurrSaveData = new SerializedSaveData();

        PlayerPrefs.DeleteAll();

        SceneTransitioner.Instance.ToMainMenu();
    }
    #endregion
}

[Serializable]
public class SerializedSaveData
{
    public float Version = 0.125f;

    public float MusicVol = 0.3f;
    public float SfxVol = 0.5f;

    public float MouseSens = 64f / 255f;
    public float Fov = 90;

    public string ReboundControls = null;
}

#if UNITY_EDITOR
[CustomEditor(typeof(SaveData))]
public class SaveData_Inspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        SaveData data = target as SaveData;

        if (GUILayout.Button("Reset All Data"))
        {
            data.ResetSaveData();
        }
    }
}
#endif
