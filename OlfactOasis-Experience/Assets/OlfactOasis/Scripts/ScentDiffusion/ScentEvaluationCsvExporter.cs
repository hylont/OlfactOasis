using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class ScentEvaluationCsvExporter
{
    const string CsvHeader = "ScentName,SlotIndex,Strength,Duration,Frequency,WasPerceived,WasPleasant,ResponseMagnitude,ResponseCurvePoints";

    public static string Save(ScentData scentData, string directory)
    {
        string participantId = ParticipantData.GetID();
        if (string.IsNullOrEmpty(participantId))
        {
            LLogger.E("Could not resolve a participant ID, aborting CSV export.");
            return null;
        }

        try
        {
            Directory.CreateDirectory(directory);
            string path = GetAvailablePath(directory, participantId);

            StringBuilder csv = new StringBuilder().AppendLine(CsvHeader);
            foreach (ScentEvaluation evaluation in scentData.Evaluations)
            {
                csv.AppendLine(ToCsvRow(scentData.Name, evaluation));
            }

            File.WriteAllText(path, csv.ToString());
            LLogger.L($"Saved {scentData.Evaluations.Count} evaluation(s) for '{scentData.Name}' to {path}");

            return path;
        }
        catch (Exception e)
        {
            LLogger.E($"Failed to save evaluations for '{scentData.Name}' - {e}");
            return null;
        }
    }

    // Starts from <id>.csv, incrementing the -<X> suffix by the existing file count until a free name is found.
    static string GetAvailablePath(string directory, string participantId)
    {
        string path = Path.Combine(directory, $"{participantId}.csv");
        if (!File.Exists(path)) return path;

        int suffix = Directory.GetFiles(directory, $"{participantId}*.csv").Length;
        do
        {
            path = Path.Combine(directory, $"{participantId}-{suffix}.csv");
            suffix++;
        }
        while (File.Exists(path));

        return path;
    }

    static string ToCsvRow(EScentName scentName, ScentEvaluation evaluation)
    {
        ScentDiffusionParameters parameters = evaluation.Parameters;
        string curvePoints = string.Join(";", evaluation.ResponseCurvePoints.Select(PointToString));

        string[] fields =
        {
            scentName.ToString(),
            parameters.SlotIndex.ToString(CultureInfo.InvariantCulture),
            parameters.Strength.ToString(CultureInfo.InvariantCulture),
            parameters.Duration.ToString(CultureInfo.InvariantCulture),
            parameters.Frequency.ToString(CultureInfo.InvariantCulture),
            evaluation.WasPerceived.ToString(),
            evaluation.WasPleasant.ToString(),
            evaluation.ResponseMagnitude.ToString(CultureInfo.InvariantCulture),
            curvePoints
        };

        return string.Join(",", fields);
    }

    // Invariant culture keeps the decimal separator as '.' - a French locale would otherwise emit commas and corrupt the CSV columns.
    static string PointToString(Vector3 point) =>
        $"{point.x.ToString(CultureInfo.InvariantCulture)}:{point.y.ToString(CultureInfo.InvariantCulture)}:{point.z.ToString(CultureInfo.InvariantCulture)}";

    public static void LaunchDataReader(string pythonExecutable, string scriptRelativePath, string csvPath, EScentName scentName)
    {
        string scriptPath = Path.GetFullPath(Path.Combine(Application.dataPath, scriptRelativePath));

        if (!File.Exists(scriptPath))
        {
            LLogger.E($"Python data reader script not found at {scriptPath}.");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add(csvPath);
            startInfo.ArgumentList.Add(scentName.ToString());

            Process.Start(startInfo);
        }
        catch (Exception e)
        {
            LLogger.E($"Failed to launch the Python data reader - {e}");
        }
    }
}
