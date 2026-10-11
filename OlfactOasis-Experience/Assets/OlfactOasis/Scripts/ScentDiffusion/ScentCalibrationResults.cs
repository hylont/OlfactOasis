using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// Results of one calibration session (one participant), written as JSON to <persistentDataPath>/ScentCalibrationResults/<start date>.json.
// The file is rewritten at the end of each booth, so it always holds every scent calibrated so far.
//
// ScentResult and EvaluationResult mirror ScentData and ScentEvaluation, but write enums as uppercase text:
// JsonUtility writes enums as their index, which would point to another value as soon as an enum is reordered or extended.
[Serializable]
public class ScentCalibrationResults
{
    const string FOLDER_NAME = "ScentCalibrationResults";

    public string ParticipantId;
    public string StartDate;
    public List<ScentResult> Scents = new();

    readonly string _filePath;

    public ScentCalibrationResults(string participantId)
    {
        DateTime now = DateTime.Now;
        ParticipantId = participantId;
        StartDate = now.ToString("s"); //ISO 8601, e.g. 2026-10-11T14:03:22
        _filePath = Path.Combine(Application.persistentDataPath, FOLDER_NAME, $"{now:yyyyMMdd_HHmmss}.json");
    }

    public void AddScent(ScentData scentData) => Scents.Add(new ScentResult(scentData));

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
            File.WriteAllText(_filePath, JsonUtility.ToJson(this, true));
            LLogger.L($"Results saved to {_filePath}");
        }
        catch (Exception e)
        {
            LLogger.E($"Failed to save results to {_filePath} - {e}");
        }
    }

    [Serializable]
    public class ScentResult
    {
        public string Name;
        public int SlotIndex;
        public int DefaultVibrationFrequency;
        public List<EvaluationResult> Evaluations;
        public ScentDiffusionParameters MildParameters, OptimalParameters, StrongParameters;

        public ScentResult(ScentData scentData)
        {
            Name = ToUpperSnakeCase(scentData.Name);
            SlotIndex = scentData.SlotIndex;
            DefaultVibrationFrequency = scentData.DefaultVibrationFrequency;
            Evaluations = scentData.Evaluations.Select(evaluation => new EvaluationResult(evaluation)).ToList();
            MildParameters = scentData.MildParameters;
            OptimalParameters = scentData.OptimalParameters;
            StrongParameters = scentData.StrongParameters;
        }
    }

    [Serializable]
    public class EvaluationResult
    {
        public ScentDiffusionParameters Parameters;
        public string WasPerceived;
        public string WasPleasant;
        public List<Vector3> ResponseCurvePoints;
        public float ResponseMagnitude;

        public EvaluationResult(ScentEvaluation evaluation)
        {
            Parameters = evaluation.Parameters;
            WasPerceived = ToUpperSnakeCase(evaluation.WasPerceived);
            WasPleasant = ToUpperSnakeCase(evaluation.WasPleasant);
            ResponseCurvePoints = evaluation.ResponseCurvePoints;
            ResponseMagnitude = evaluation.ResponseMagnitude;
        }
    }

    // DarkChocolate -> DARK_CHOCOLATE, NeutralUndecided -> NEUTRAL_UNDECIDED
    static string ToUpperSnakeCase(Enum value) =>
        Regex.Replace(value.ToString(), "(?<=[a-z0-9])(?=[A-Z])", "_").ToUpperInvariant();
}
