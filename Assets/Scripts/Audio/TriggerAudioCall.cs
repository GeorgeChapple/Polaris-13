using UnityEngine;

// Made By: Jason Lodge
// Summary: Triggers audio on collision or trigger overlap.
public class TriggerAudioCall : MonoBehaviour
{
    [SerializeField] private bool network;
    [SerializeField] private AUD_SFX sfx;
    [SerializeField] private AUD_SFXNetwork sfxNet;

    private void OnTriggerEnter(Collider other)
    {
        if (network)
        {
            if (sfxNet == null) { return; }
            sfxNet.PlayRandomSound();
        }
        else
        {
            if (sfx == null) { return; }
            sfx.PlayRandomSound();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (network)
        {
            if (sfxNet == null) { return; }
            sfxNet.PlayRandomSound();
        }
        else
        {
            if (sfx == null) { return; }
            sfx.PlayRandomSound();
        }
    }
}