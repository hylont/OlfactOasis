using System;
using UnityEngine;

[Serializable]
public class ScentDiffusionParameters
{
    #region Default parameters values
    private const int DEFAULT_SLOT_INDEX = 1;
    private const int MIN_SLOT_INDEX = 1;

    private const float DEFAULT_STRENGTH = .5f;
    private const float MIN_STRENGTH = 0f;
    private const float MAX_STRENGTH = 1f;

    private const float DEFAULT_DURATION = 3f;
    private const float MIN_DURATION = .1f;

    private const int DEFAULT_FREQUENCY = 11000;
    private const int MIN_FREQUENCY = 100;
    #endregion

    [Tooltip("On numerous olfactive devices, various slots (or vials) can be triggered. Specify here the index of the slot where the vial is stored. (1 by default)")]
    [Min(1)] public int SlotIndex = DEFAULT_SLOT_INDEX;
    
    [Tooltip("Specify the normalized (from 0 to 1.0) strength of the diffusion. See it as the amount of scent particles emmited each second of diffusion. (0.5 by default)")]
    [Range(MIN_STRENGTH,MAX_STRENGTH)] 
    public float Strength = DEFAULT_STRENGTH;
    
    [Tooltip("The duration of a scent emission (3 by default)")]
    [Min(MIN_DURATION)] public float Duration = DEFAULT_DURATION;

    [Tooltip("The vibration frequency of the diffusion, in hertz. Used to handle thickest solutions. (11000 Hz by default)")]
    [Min(MIN_FREQUENCY)] public int Frequency = DEFAULT_FREQUENCY;

    public ScentDiffusionParameters(int slotIndex, float strength, float duration, int frequency = DEFAULT_FREQUENCY)
    {
        SlotIndex = slotIndex;
        if (SlotIndex < MIN_SLOT_INDEX) LLogger.W($"The scent's slot should be at least {MIN_SLOT_INDEX}");
        
        Strength = strength;
        if (Strength < MIN_STRENGTH || Strength > MAX_STRENGTH) LLogger.W($"The scent's strength should be between {MIN_STRENGTH} and {MAX_STRENGTH}.");
        
        Duration = duration;
        if (Duration < MIN_DURATION) LLogger.W($"The scent's duration should be more than {MIN_DURATION} seconds.");
        
        Frequency = frequency;
    }
}