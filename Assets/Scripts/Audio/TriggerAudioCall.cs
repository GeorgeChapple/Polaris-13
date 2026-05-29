using Unity.Cinemachine;
using UnityEngine;

// Made By: Jason Lodge
// Summary: Triggers audio on collision or trigger overlap.
public class TriggerAudioCall : MonoBehaviour
{
    [SerializeField] private AUD_SFX sfx;

    private void OnTriggerEnter(Collider other)
    {
        if (sfx == null) { return; }
        sfx.PlayRandomSound();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (sfx == null) { return; }
        sfx.PlayRandomSound();
    }
}