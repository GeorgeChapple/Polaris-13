using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// Made By: Jason Lodge
// Summary: Handles game settings.
// Loads saved settings from PlayerPrefs, applies them to game systems, and updates settings ui.
public class UI_SettingsManager : MonoBehaviour
{
    [Header("Audio Refs")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Audio UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Display UI")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("Gameplay UI")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Toggle invertYToggle;

    [Header("Audio Mixer Params")]
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam = "MusicVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";

    [Header("Defaults")]
    [SerializeField] private float defaultMasterVolume = 1f;
    [SerializeField] private float defaultMusicVolume = 1f;
    [SerializeField] private float defaultSfxVolume = 1f;
    [SerializeField] private float defaultMouseSensitivity = 1f;
    [SerializeField] private bool defaultFullscreen = true;
    [SerializeField] private bool defaultInvertY;

    private const string MasterVolumeKey = "Settings_MasterVolume";
    private const string MusicVolumeKey = "Settings_MusicVolume";
    private const string SfxVolumeKey = "Settings_SFXVolume";
    private const string FullscreenKey = "Settings_Fullscreen";
    private const string QualityKey = "Settings_Quality";
    private const string ResolutionWidthKey = "Settings_ResolutionWidth";
    private const string ResolutionHeightKey = "Settings_ResolutionHeight";
    private const string MouseSensitivityKey = "Settings_MouseSensitivity";
    private const string InvertYKey = "Settings_InvertY";

    private Resolution[] resolutions;
    private bool hasInit;

    public float MouseSensitivity => PlayerPrefs.GetFloat(MouseSensitivityKey, defaultMouseSensitivity);
    public bool InvertY => PlayerPrefs.GetInt(InvertYKey, defaultInvertY ? 1 : 0) == 1;

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
        LoadSettings();
    }

    private void Init()
    {
        if (hasInit) { return; }

        hasInit = true;

        SetupResolutionDropdown();
        SetupQualityDropdown();
    }

    private void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) { return; }

        resolutions = Screen.resolutions;

        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();

        int currentResolutionIndex = 0;

        int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);

        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution res = resolutions[i];

            string option = res.width + " x " + res.height + " @ " + res.refreshRateRatio.value.ToString("0") + "Hz";
            options.Add(option);

            if (res.width == savedWidth && res.height == savedHeight)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void SetupQualityDropdown()
    {
        if (qualityDropdown == null) { return; }

        qualityDropdown.ClearOptions();

        List<string> options = new List<string>();

        string[] qualityNames = QualitySettings.names;

        for (int i = 0; i < qualityNames.Length; i++)
        {
            options.Add(qualityNames[i]);
        }

        qualityDropdown.AddOptions(options);
        qualityDropdown.RefreshShownValue();
    }

    public void LoadSettings()
    {
        Init();

        float masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, defaultMasterVolume);
        float musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume);
        float sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, defaultSfxVolume);

        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, defaultFullscreen ? 1 : 0) == 1;
        int quality = PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());

        int width = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
        int height = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);

        float mouseSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, defaultMouseSensitivity);
        bool invertY = PlayerPrefs.GetInt(InvertYKey, defaultInvertY ? 1 : 0) == 1;

        ApplyMasterVolume(masterVolume);
        ApplyMusicVolume(musicVolume);
        ApplySfxVolume(sfxVolume);
        ApplyFullscreen(fullscreen);
        ApplyQuality(quality);
        ApplyResolution(width, height, fullscreen);
        ApplyMouseSensitivity(mouseSensitivity);
        ApplyInvertY(invertY);

        RefreshUI(masterVolume, musicVolume, sfxVolume, fullscreen, quality, width, height, mouseSensitivity, invertY);
    }

    private void RefreshUI(float masterVolume, float musicVolume, float sfxVolume, bool fullscreen, int quality, int width, int height, float mouseSensitivity, bool invertY)
    {
        if (masterVolumeSlider != null) { masterVolumeSlider.value = masterVolume; }
        if (musicVolumeSlider != null) { musicVolumeSlider.value = musicVolume; }
        if (sfxVolumeSlider != null) { sfxVolumeSlider.value = sfxVolume; }

        if (fullscreenToggle != null) { fullscreenToggle.isOn = fullscreen; }
        if (qualityDropdown != null) { qualityDropdown.value = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1); }

        if (mouseSensitivitySlider != null) { mouseSensitivitySlider.value = mouseSensitivity; }
        if (invertYToggle != null) { invertYToggle.isOn = invertY; }

        RefreshResolutionUI(width, height);
    }

    private void RefreshResolutionUI(int width, int height)
    {
        if (resolutionDropdown == null || resolutions == null) { return; }

        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution res = resolutions[i];

            if (res.width == width && res.height == height)
            {
                resolutionDropdown.value = i;
                resolutionDropdown.RefreshShownValue();
                return;
            }
        }
    }

    public void SetMasterVolume(float volume)
    {
        volume = Mathf.Clamp(volume, 0.0001f, 1f);

        PlayerPrefs.SetFloat(MasterVolumeKey, volume);
        PlayerPrefs.Save();

        ApplyMasterVolume(volume);
    }

    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp(volume, 0.0001f, 1f);

        PlayerPrefs.SetFloat(MusicVolumeKey, volume);
        PlayerPrefs.Save();

        ApplyMusicVolume(volume);
    }

    public void SetSfxVolume(float volume)
    {
        volume = Mathf.Clamp(volume, 0.0001f, 1f);

        PlayerPrefs.SetFloat(SfxVolumeKey, volume);
        PlayerPrefs.Save();

        ApplySfxVolume(volume);
    }

    public void SetFullscreen(bool fullscreen)
    {
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();

        ApplyFullscreen(fullscreen);

        int width = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
        int height = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);

        ApplyResolution(width, height, fullscreen);
    }

    public void SetQuality(int quality)
    {
        quality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);

        PlayerPrefs.SetInt(QualityKey, quality);
        PlayerPrefs.Save();

        ApplyQuality(quality);
    }

    public void SetResolution(int resolutionIndex)
    {
        if (resolutions == null || resolutions.Length == 0) { return; }
        if (resolutionIndex < 0 || resolutionIndex >= resolutions.Length) { return; }

        Resolution res = resolutions[resolutionIndex];

        PlayerPrefs.SetInt(ResolutionWidthKey, res.width);
        PlayerPrefs.SetInt(ResolutionHeightKey, res.height);
        PlayerPrefs.Save();

        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, defaultFullscreen ? 1 : 0) == 1;

        ApplyResolution(res.width, res.height, fullscreen);
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        sensitivity = Mathf.Max(0f, sensitivity);

        PlayerPrefs.SetFloat(MouseSensitivityKey, sensitivity);
        PlayerPrefs.Save();

        ApplyMouseSensitivity(sensitivity);
    }

    public void SetInvertY(bool invertY)
    {
        PlayerPrefs.SetInt(InvertYKey, invertY ? 1 : 0);
        PlayerPrefs.Save();

        ApplyInvertY(invertY);
    }

    private void ApplyMasterVolume(float volume)
    {
        if (audioMixer == null) { return; }

        audioMixer.SetFloat(masterVolumeParam, Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f);
    }

    private void ApplyMusicVolume(float volume)
    {
        if (audioMixer == null) { return; }

        audioMixer.SetFloat(musicVolumeParam, Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f);
    }

    private void ApplySfxVolume(float volume)
    {
        if (audioMixer == null) { return; }

        audioMixer.SetFloat(sfxVolumeParam, Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f);
    }

    private void ApplyFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
    }

    private void ApplyQuality(int quality)
    {
        quality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(quality);
    }

    private void ApplyResolution(int width, int height, bool fullscreen)
    {
        if (width <= 0 || height <= 0) { return; }

        Screen.SetResolution(width, height, fullscreen);
    }

    private void ApplyMouseSensitivity(float sensitivity)
    {
        CC_CameraController cameraController = FindLocalCameraController();
        if (cameraController == null) { return; }

        cameraController.SetLookSensitivity(sensitivity);
    }

    private void ApplyInvertY(bool invertY)
    {
        CC_CameraController cameraController = FindLocalCameraController();
        if (cameraController == null) { return; }

        cameraController.SetInvertY(invertY);
    }

    private CC_CameraController FindLocalCameraController()
    {
        CC_CameraController[] controllers = FindObjectsByType<CC_CameraController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            CC_CameraController controller = controllers[i];
            if (controller == null || controller.movement == null) { continue; }
            if (!controller.movement.IsLocallyControlled()) { continue; }

            return controller;
        }

        return null;
    }
}