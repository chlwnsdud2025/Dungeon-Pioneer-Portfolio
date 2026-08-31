#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 맵 프리팹 안의 ArrowTriggerTile(PressurePlate)과 ArrowTrap을 거리 기반으로 짝지어 연결합니다.
/// </summary>
public static class ArrowTrapAutoWire
{
    private const string MapsFolder = "Assets/Prefabs/Maps";
    private const float MaxPairDistanceXZ = 16f;

    [MenuItem("Tools/Dungeon/Wire Arrow Trigger Tiles")]
    public static void WireAllMapsFromMenu()
    {
        int wired = WireAllMapsInFolder(MapsFolder);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ArrowTrapAutoWire] 완료: {wired}개 발판 연결");
    }

    /// <summary>Unity batchmode: -executeMethod ArrowTrapAutoWire.BatchWire</summary>
    public static void BatchWire()
    {
        WireAllMapsFromMenu();
        EditorApplication.Exit(0);
    }

    public static int WireAllMapsInFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError($"[ArrowTrapAutoWire] 폴더를 찾을 수 없습니다: {folder}");
            return 0;
        }

        int total = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".prefab")) continue;
            total += WireMapPrefab(path);
        }

        return total;
    }

    public static int WireMapPrefab(string mapPrefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(mapPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[ArrowTrapAutoWire] 프리팹 로드 실패: {mapPrefabPath}");
            return 0;
        }

        try
        {
            int wired = ArrowTrapRuntimeWire.WireMap(root, onlyIfMissing: false);
            if (wired > 0)
                PrefabUtility.SaveAsPrefabAsset(root, mapPrefabPath);
            return wired;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int WireInHierarchy(GameObject root, string contextPath)
    {
        PressurePlate[] plates = root.GetComponentsInChildren<PressurePlate>(true);
        ArrowTrap[] traps = root.GetComponentsInChildren<ArrowTrap>(true);

        if (plates.Length == 0 || traps.Length == 0)
            return 0;

        List<PairCandidate> candidates = new List<PairCandidate>();
        foreach (PressurePlate plate in plates)
        {
            Vector3 platePos = plate.transform.position;
            foreach (ArrowTrap trap in traps)
            {
                float dist = DistanceXZ(platePos, trap.transform.position);
                if (dist <= MaxPairDistanceXZ)
                    candidates.Add(new PairCandidate(plate, trap, dist));
            }
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        HashSet<PressurePlate> usedPlates = new HashSet<PressurePlate>();
        HashSet<ArrowTrap> usedTraps = new HashSet<ArrowTrap>();
        int wired = 0;

        foreach (PairCandidate candidate in candidates)
        {
            if (usedPlates.Contains(candidate.Plate) || usedTraps.Contains(candidate.Trap))
                continue;

            candidate.Plate.arrowTraps = new[] { candidate.Trap };
            usedPlates.Add(candidate.Plate);
            usedTraps.Add(candidate.Trap);
            wired++;

            Debug.Log(
                $"[ArrowTrapAutoWire] {Path.GetFileName(contextPath)}: " +
                $"'{GetHierarchyPath(candidate.Plate.transform)}' → '{GetHierarchyPath(candidate.Trap.transform)}' " +
                $"(XZ 거리 {candidate.Distance:F1})",
                candidate.Plate);
        }

        foreach (PressurePlate plate in plates)
        {
            if (!usedPlates.Contains(plate))
            {
                Debug.LogWarning(
                    $"[ArrowTrapAutoWire] {Path.GetFileName(contextPath)}: 짝을 찾지 못한 발판 '{GetHierarchyPath(plate.transform)}' ({plate.transform.position})",
                    plate);
            }
        }

        foreach (ArrowTrap trap in traps)
        {
            if (!usedTraps.Contains(trap))
            {
                Debug.LogWarning(
                    $"[ArrowTrapAutoWire] {Path.GetFileName(contextPath)}: 짝을 찾지 못한 함정 '{GetHierarchyPath(trap.transform)}' ({trap.transform.position})",
                    trap);
            }
        }

        return wired;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static string GetHierarchyPath(Transform t)
    {
        List<string> names = new List<string>();
        while (t != null)
        {
            names.Add(t.name);
            t = t.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private readonly struct PairCandidate
    {
        public PressurePlate Plate { get; }
        public ArrowTrap Trap { get; }
        public float Distance { get; }

        public PairCandidate(PressurePlate plate, ArrowTrap trap, float distance)
        {
            Plate = plate;
            Trap = trap;
            Distance = distance;
        }
    }
}
#endif
