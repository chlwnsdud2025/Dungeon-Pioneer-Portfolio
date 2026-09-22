using System.Collections.Generic;
using UnityEngine;

public static class ArrowTrapRuntimeWire
{
    private const float MaxPairDistanceXZ = 16f;

    public static int WireMap(GameObject mapRoot, bool onlyIfMissing = true)
    {
        if (mapRoot == null) return 0;

        PressurePlate[] plates = mapRoot.GetComponentsInChildren<PressurePlate>(true);
        ArrowTrap[] traps = mapRoot.GetComponentsInChildren<ArrowTrap>(true);
        if (plates.Length == 0 || traps.Length == 0) return 0;

        List<PairCandidate> candidates = new List<PairCandidate>();
        foreach (PressurePlate plate in plates)
        {
            if (onlyIfMissing && HasValidTrap(plate)) continue;

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
                $"[ArrowTrapRuntimeWire] '{candidate.Plate.name}' → '{candidate.Trap.name}' (XZ {candidate.Distance:F1})",
                candidate.Plate);
        }

        return wired;
    }

    private static bool HasValidTrap(PressurePlate plate)
    {
        if (plate.arrowTraps == null || plate.arrowTraps.Length == 0) return false;
        foreach (ArrowTrap trap in plate.arrowTraps)
        {
            if (trap != null && trap.gameObject.scene.IsValid()) return true;
        }
        return false;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
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
